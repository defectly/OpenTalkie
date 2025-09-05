using AutoMapper;
using OpenTalkie.Common.Dto;
using OpenTalkie.Common.Enums;
using OpenTalkie.Common.Repositories.Interfaces;
using OpenTalkie.Common.Services.Interfaces;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Threading;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
using System.Runtime.Intrinsics.Arm;

namespace OpenTalkie.Common.Services;

public class ReceiverService
{
    private const bool StrictLowLatency = true; // disable micro-waiting to shave ~1ms
    private readonly IMapper _mapper;
    private readonly IEndpointRepository _endpointRepository;
    private readonly IAudioOutputService _audioOutput;
    private AsyncReceiver? _receiver;
    private CancellationTokenSource? _mixCts;
    private Task? _mixTask;
    private readonly System.Collections.Concurrent.ConcurrentQueue<(Endpoint ep, byte[] payload, WaveFormat wf)> _incoming = new();
    private readonly SemaphoreSlim _incomingSignal = new(0);
    private CancellationTokenSource? _procCts;
    private Task? _procTask;
    private readonly Dictionary<Guid, StreamBuffer> _buffers = new();
    private volatile StreamBuffer[] _activeBuffers = Array.Empty<StreamBuffer>();
    private readonly Dictionary<Guid, DenoiseCtx> _denoisers = new();
    private readonly object _outputLock = new();
    private int _currentSampleRate;
    private int _currentChannels;
    private const int TargetSampleRate = 48000;
    private const int TargetChannels = 2;
    private const int MixChunkSamples = 240; // per channel (~5ms at 48k)
    private const int BytesPerSample = 2; // 16-bit
    private const int BytesPerChunk = MixChunkSamples * TargetChannels * BytesPerSample;
    // Jitter buffer size is computed per stream from its selected Quality
    private volatile float _globalVolume = 1f;

    public ObservableCollection<Endpoint> Endpoints { get; }
    public bool ListeningState { get; private set; }
    public Action<bool>? ListeningStateChanged;

    public ReceiverService(IEndpointRepository endpointRepository, IMapper mapper, IAudioOutputService audioOutput, OpenTalkie.Common.Repositories.Interfaces.IReceiverRepository receiverRepository)
    {
        _endpointRepository = endpointRepository;
        _mapper = mapper;
        _audioOutput = audioOutput;

        // Initialize global volume and subscribe for changes
        _globalVolume = receiverRepository.GetSelectedVolume();
        receiverRepository.VolumeChanged += (v) => _globalVolume = v;

        Endpoints = mapper.Map<ObservableCollection<Endpoint>>(
            _endpointRepository.List().Where(e => e.Type == EndpointType.Receiver));
        Endpoints.CollectionChanged += EndpointsCollectionChanged;
        for (int i = 0; i < Endpoints.Count; i++)
            Endpoints[i].PropertyChanged += EndpointPropertyChanged;

        ListeningStateChanged += OnListeningStateChange;
    }

    public void Start()
    {
        if (ListeningState) return;
        _receiver ??= new AsyncReceiver(Endpoints);
        _receiver.FrameReceived += OnFrameReceived;
        _receiver.Start();
        // Ensure Android keeps our process alive while receiving audio
#if ANDROID
        try
        {
            OpenTalkie.Platforms.Android.Common.Services.Receiver.ReceiverForegroundServiceManager.StartForegroundService();
        }
        catch { }
#endif
        // Start output once at target format and begin mixer loop
        EnsureOutput(TargetSampleRate, TargetChannels);
        _mixCts = new CancellationTokenSource();
        _mixTask = Task.Run(() => MixLoopAsync(_mixCts.Token));
        // Start frame processing worker (decouples UDP receive from conversion)
        _procCts = new CancellationTokenSource();
        _procTask = Task.Run(() => ProcessIncomingLoopAsync(_procCts.Token));
        ListeningStateChanged?.Invoke(true);
    }

    public void Stop()
    {
        if (!ListeningState) return;
        _receiver!.FrameReceived -= OnFrameReceived;
        _receiver.Stop();
        _receiver = null;
        _mixCts?.Cancel();
        try { _mixTask?.Wait(500); } catch { }
        _audioOutput.Stop();
        _procCts?.Cancel();
        try { _procTask?.Wait(500); } catch { }
        lock (_buffers)
        {
            _buffers.Clear();
        }
        // Dispose denoise contexts
        var dvals = _denoisers.Values.ToList();
        for (int i = 0; i < dvals.Count; i++) try { dvals[i].Dn.Dispose(); } catch { }
        _denoisers.Clear();
        Volatile.Write(ref _activeBuffers, Array.Empty<StreamBuffer>());
#if ANDROID
        try
        {
            OpenTalkie.Platforms.Android.Common.Services.Receiver.ReceiverForegroundServiceManager.StopForegroundService();
        }
        catch { }
#endif
        ListeningStateChanged?.Invoke(false);
    }

    public void Switch()
    {
        if (ListeningState) Stop(); else Start();
    }

    private void OnFrameReceived(Endpoint ep, byte[] payload, WaveFormat wf)
    {
        // Offload heavy conversion to background processor to avoid blocking UDP receive loop
        _incoming.Enqueue((ep, payload, wf));
        _incomingSignal.Release();
    }

    private static int ComputeMaxQueuedChunks(OpenTalkie.VBAN.VBanQuality quality)
    {
        // Interpret VBanQuality as reference samples per channel at 48kHz
        // 512,1024,2048,4096,8192 -> base chunks: ceil(val/480)
        int referenceSamples = (int)quality;
        int chunks = (int)Math.Ceiling(referenceSamples / (double)MixChunkSamples);
        // Ensure a small safety window to ride out micro-jitter without prebuffering
        if (chunks < 6) chunks = 6;
        if (chunks > 64) chunks = 64; // cap for sanity
        return chunks;
    }

    private static int ComputeMinReadyChunks(OpenTalkie.VBAN.VBanQuality quality) => 1; // avoid added latency

    private void EnsureOutput(int sampleRate, int channels)
    {
        lock (_outputLock)
        {
            if (!_audioOutput.IsStarted)
            {
                _audioOutput.Start(sampleRate, channels);
                _currentSampleRate = sampleRate;
                _currentChannels = channels <= 1 ? 1 : 2;
                return;
            }
            // Do not restart to minimize glitches; keep current output format.
        }
    }

    private async Task ProcessIncomingLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try { await _incomingSignal.WaitAsync(ct); }
            catch (OperationCanceledException) { break; }
            catch { continue; }

            // Drain as much as possible to batch work
            for (int i = 0; i < 32; i++)
            {
                if (!_incoming.TryDequeue(out var item)) break;
                var ep = item.ep;
                var wf = item.wf;
                var payload = item.payload;

                // Optional RNNoise on receive (off UDP thread)
                if (ep.IsDenoiseEnabled)
                {
                    if (!_denoisers.TryGetValue(ep.Id, out var dctx))
                    {
                        dctx = new DenoiseCtx();
                        _denoisers[ep.Id] = dctx;
                    }
                    var mono16 = ConvertToMono16ForDn(payload, wf.BitsPerSample, wf.Channels);
                    var res48 = ResampleTo48kForDn(mono16, wf.SampleRate, dctx);
                    if (res48.Length > 0)
                    {
                        int frameBytes = 480 * 2;
                        int need = dctx.PendingCount + res48.Length;
                        if (dctx.Pending.Length < need)
                        {
                            int newCap = Math.Max(need, dctx.Pending.Length == 0 ? 4096 : dctx.Pending.Length * 2);
                            var nb = new byte[newCap];
                            if (dctx.PendingCount > 0) Buffer.BlockCopy(dctx.Pending, 0, nb, 0, dctx.PendingCount);
                            dctx.Pending = nb;
                        }
                        Buffer.BlockCopy(res48, 0, dctx.Pending, dctx.PendingCount, res48.Length);
                        dctx.PendingCount += res48.Length;

                        int frames = dctx.PendingCount / frameBytes;
                        if (frames > 0)
                        {
                            int outLen = frames * frameBytes;
                            var outBufMono = new byte[outLen];
                            for (int fi = 0; fi < frames; fi++)
                            {
                                int off = fi * frameBytes;
                                dctx.Dn.Denoise(dctx.Pending, off, frameBytes, false);
                                Buffer.BlockCopy(dctx.Pending, off, outBufMono, off, frameBytes);
                            }
                            int remain = dctx.PendingCount - outLen;
                            if (remain > 0) Buffer.BlockCopy(dctx.Pending, outLen, dctx.Pending, 0, remain);
                            dctx.PendingCount = remain;

                            // Upmix mono->stereo for mixing
                            int framesOut = outLen / 2; // samples mono
                            byte[] stereo = new byte[framesOut * 4];
                            var srcS = MemoryMarshal.Cast<byte, short>(outBufMono.AsSpan());
                            var dstS = MemoryMarshal.Cast<byte, short>(stereo.AsSpan());
                            for (int f = 0; f < framesOut; f++) { short s = srcS[f]; int o = f * 2; dstS[o] = s; dstS[o + 1] = s; }

                            bool added2 = false;
                            lock (_buffers)
                            {
                                if (!_buffers.TryGetValue(ep.Id, out var buf2))
                                {
                                    buf2 = new StreamBuffer(BytesPerChunk, ComputeMaxQueuedChunks(ep.Quality), ComputeMinReadyChunks(ep.Quality)) { Volume = ep.Volume };
                                    _buffers[ep.Id] = buf2;
                                    added2 = true;
                                }
                                _buffers[ep.Id].Enqueue(stereo);
                                if (added2)
                                {
                                    var snap2 = _buffers.Values.ToArray();
                                    Volatile.Write(ref _activeBuffers, snap2);
                                }
                            }
                            continue; // handled via denoise path
                        }
                        else
                        {
                            continue; // wait for full denoise frame
                        }
                    }
                    else
                    {
                        continue; // no samples from conversion yet
                    }
                }

                // Convert to target format with fast-path for 48k/16-bit
                byte[] pcm16;
                if (wf.BitsPerSample == 16 && wf.SampleRate == TargetSampleRate)
                {
                    if (wf.Channels == 2)
                    {
                        // Exact target format; enqueue as-is (volume applied at mix time)
                        pcm16 = payload;
                    }
                    else if (wf.Channels == 1)
                    {
                        // Upmix mono to stereo cheaply
                        int frames = payload.Length / 2;
                        pcm16 = new byte[frames * 4];
                        var src = MemoryMarshal.Cast<byte, short>(payload.AsSpan());
                        var dst = MemoryMarshal.Cast<byte, short>(pcm16.AsSpan());
                        for (int f = 0; f < frames; f++)
                        {
                            short s = src[f];
                            int o = f * 2;
                            dst[o] = s; dst[o + 1] = s;
                        }
                    }
                    else
                    {
                        // Take first two channels
                        pcm16 = ConvertToPcm16Interleaved(payload, 16, wf.Channels, TargetChannels);
                    }
                }
                else
                {
                    // Generic path
                    pcm16 = ConvertToPcm16Interleaved(payload, wf.BitsPerSample, wf.Channels, TargetChannels);
                    if (wf.SampleRate != TargetSampleRate)
                    {
                        pcm16 = ResamplePcm16Interleaved(pcm16, wf.SampleRate, TargetSampleRate, TargetChannels);
                    }
                }

                bool added = false;
                lock (_buffers)
                {
                    if (!_buffers.TryGetValue(ep.Id, out var buf))
                    {
                        buf = new StreamBuffer(BytesPerChunk, ComputeMaxQueuedChunks(ep.Quality), ComputeMinReadyChunks(ep.Quality));
                        buf.Volume = ep.Volume;
                        _buffers[ep.Id] = buf;
                        added = true;
                    }
                    _buffers[ep.Id].Enqueue(pcm16);
                    if (added)
                    {
                        var snap = _buffers.Values.ToArray();
                        Volatile.Write(ref _activeBuffers, snap);
                    }
                }
            }
        }
    }

    // ---------- Receive-side denoise helpers (moved off UDP thread) ----------
    private sealed class DenoiseCtx
    {
        public OpenTalkie.RNNoise.Denoiser Dn = new();
        public byte[] Pending = Array.Empty<byte>();
        public int PendingCount;
        public short[] ResIn = Array.Empty<short>();
        public int ResInCount;
        public double ResPos;
        public int LastInRate = 48000;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static short[] ConvertToMono16ForDn(byte[] input, int bits, int channels)
    {
        int inBps = bits / 8;
        if (inBps <= 0 || channels <= 0) return Array.Empty<short>();
        int frames = input.Length / (inBps * channels);
        if (frames <= 0) return Array.Empty<short>();
        var mono = new short[frames];
        for (int f = 0; f < frames; f++)
        {
            int baseIdx = f * inBps * channels;
            int acc = 0;
            for (int ch = 0; ch < channels; ch++)
            {
                int s = ReadSampleAsInt(input, baseIdx + ch * inBps, bits);
                short s16 = ToInt16(s, bits);
                acc += s16;
            }
            mono[f] = (short)(acc / channels);
        }
        return mono;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static byte[] ResampleTo48kForDn(short[] mono16, int inRate, DenoiseCtx ctx)
    {
        if (mono16.Length == 0) return Array.Empty<byte>();
        if (inRate <= 0) return Array.Empty<byte>();
        if (ctx.LastInRate != inRate)
        {
            ctx.LastInRate = inRate;
            ctx.ResInCount = 0;
            ctx.ResPos = 0;
        }
        int need = ctx.ResInCount + mono16.Length;
        if (ctx.ResIn.Length < need)
        {
            int newCap = Math.Max(need, ctx.ResIn.Length == 0 ? mono16.Length * 2 : ctx.ResIn.Length * 2);
            var nb = new short[newCap];
            if (ctx.ResInCount > 0) Array.Copy(ctx.ResIn, 0, nb, 0, ctx.ResInCount);
            ctx.ResIn = nb;
        }
        Array.Copy(mono16, 0, ctx.ResIn, ctx.ResInCount, mono16.Length);
        ctx.ResInCount += mono16.Length;

        double step = (double)inRate / 48000.0;
        if (step <= 0) step = 1.0;

        int maxOut = 0;
        if (ctx.ResInCount > 1 && ctx.ResPos < ctx.ResInCount - 1)
        {
            maxOut = (int)Math.Floor(((ctx.ResInCount - 1) - ctx.ResPos) / step);
            if (maxOut < 0) maxOut = 0;
        }
        if (maxOut == 0) return Array.Empty<byte>();

        var outShorts = new short[maxOut];
        double pos = ctx.ResPos;
        for (int k = 0; k < maxOut; k++)
        {
            int i0 = (int)pos;
            double frac = pos - i0;
            short s0 = ctx.ResIn[i0];
            short s1 = ctx.ResIn[i0 + 1];
            int interp = s0 + (int)((s1 - s0) * frac);
            outShorts[k] = (short)interp;
            pos += step;
        }
        ctx.ResPos = pos;

        int consumed = Math.Max(0, (int)ctx.ResPos);
        if (consumed > 0)
        {
            int keep = ctx.ResInCount - consumed;
            if (keep > 0) Array.Copy(ctx.ResIn, consumed, ctx.ResIn, 0, keep);
            ctx.ResInCount = keep;
            ctx.ResPos -= consumed;
        }

        var outBytes = new byte[outShorts.Length * 2];
        Buffer.BlockCopy(outShorts, 0, outBytes, 0, outBytes.Length);
        return outBytes;
    }

    private static byte[] ResamplePcm16Interleaved(byte[] input, int inRate, int outRate, int channels)
    {
        if (inRate <= 0 || outRate <= 0 || channels <= 0) return input;
        if (inRate == outRate) return input;
        int inFrames = input.Length / (2 * channels);
        if (inFrames <= 1) return input;
        int outFrames = (int)((long)inFrames * outRate / inRate);
        short[] inShorts = new short[inFrames * channels];
        Buffer.BlockCopy(input, 0, inShorts, 0, input.Length);
        short[] outShorts = new short[outFrames * channels];

        double step = (double)inRate / outRate;
        double pos = 0.0;
        for (int of = 0; of < outFrames; of++)
        {
            int i0 = (int)pos;
            double frac = pos - i0;
            int i1 = i0 + 1;
            if (i1 >= inFrames) i1 = inFrames - 1;
            int inBase0 = i0 * channels;
            int inBase1 = i1 * channels;
            int outBase = of * channels;
            for (int ch = 0; ch < channels; ch++)
            {
                int s0 = inShorts[inBase0 + ch];
                int s1 = inShorts[inBase1 + ch];
                int interp = s0 + (int)((s1 - s0) * frac);
                outShorts[outBase + ch] = (short)interp;
            }
            pos += step;
        }
        byte[] outBytes = new byte[outShorts.Length * 2];
        Buffer.BlockCopy(outShorts, 0, outBytes, 0, outBytes.Length);
        return outBytes;
    }

    private async Task MixLoopAsync(CancellationToken ct)
    {
        int currChunkBytes = BytesPerChunk;
        byte[] mixBytes = new byte[currChunkBytes];
        short[] mixShorts = new short[currChunkBytes / 2];
        byte[] temp = new byte[currChunkBytes];

        while (!ct.IsCancellationRequested)
        {
            var buffers = Volatile.Read(ref _activeBuffers);

            // Pick a chunk size aligned to VBAN frame sizes to avoid splitting too often
            int chosen = DetermineMixChunkBytes(buffers, currChunkBytes);
            if (chosen != currChunkBytes)
            {
                currChunkBytes = chosen;
                mixBytes = new byte[currChunkBytes];
                mixShorts = new short[currChunkBytes / 2];
                temp = new byte[currChunkBytes];
            }

            Array.Clear(mixShorts, 0, mixShorts.Length);
            int activeSources = 0;
            for (int i = 0; i < buffers.Length; i++)
            {
                var buf = buffers[i];
                int read = buf.Read(temp.AsSpan(0, currChunkBytes));
                if (read <= 0) continue;
                if (read < currChunkBytes)
                {
                    if (!StrictLowLatency)
                    {
                        // optional micro-wait to accumulate remainder (up to ~1ms)
                        var startTicks = System.Diagnostics.Stopwatch.GetTimestamp();
                        long freq = System.Diagnostics.Stopwatch.Frequency;
                        while (read < currChunkBytes)
                        {
                            Thread.SpinWait(2000);
                            int add = buf.Read(temp.AsSpan(read, currChunkBytes - read));
                            if (add > 0)
                            {
                                read += add;
                                continue;
                            }
                            long elapsedTicks = System.Diagnostics.Stopwatch.GetTimestamp() - startTicks;
                            if ((elapsedTicks * 1000) / freq >= 1)
                                break;
                        }
                    }
                    if (read < currChunkBytes)
                    {
                        Array.Clear(temp, read, currChunkBytes - read);
                    }
                }
                // Apply per-stream volume at mix time, then sum with saturation
                {
                    var dst = mixShorts.AsSpan();
                    var tmpSpan = temp.AsSpan(0, currChunkBytes);
                    if (Math.Abs(buf.Volume - 1f) > 0.0001f)
                    {
                        ApplyVolume16(tmpSpan, buf.Volume);
                    }
                    var src = MemoryMarshal.Cast<byte, short>(tmpSpan);

                    if (Sse2.IsSupported)
                    {
                        unsafe
                        {
                            fixed (short* dstPtr = dst)
                            fixed (short* srcPtr = src)
                            {
                                int samples = dst.Length;
                                int v = (samples / 8) * 8; // 8 x int16 per 128-bit vector
                                for (int s = 0; s < v; s += 8)
                                {
                                    var a = Sse2.LoadVector128(dstPtr + s);
                                    var b = Sse2.LoadVector128(srcPtr + s);
                                    var sum = Sse2.AddSaturate(a, b);
                                    Sse2.Store(dstPtr + s, sum);
                                }
                                for (int s = v; s < samples; s++)
                                {
                                    int sum = dstPtr[s] + srcPtr[s];
                                    if (sum > short.MaxValue) sum = short.MaxValue; else if (sum < short.MinValue) sum = short.MinValue;
                                    dstPtr[s] = (short)sum;
                                }
                            }
                        }
                    }
                    else if (AdvSimd.IsSupported || Vector.IsHardwareAccelerated)
                    {
                        int samples = dst.Length;
                        int vsz = Vector<short>.Count;
                        var minI = new Vector<int>(short.MinValue);
                        var maxI = new Vector<int>(short.MaxValue);
                        int v = (samples / vsz) * vsz;
                        for (int s = 0; s < v; s += vsz)
                        {
                            var vd = new Vector<short>(dst.Slice(s));
                            var vs = new Vector<short>(src.Slice(s));
                            Vector.Widen(vd, out Vector<int> dLo, out Vector<int> dHi);
                            Vector.Widen(vs, out Vector<int> sLo, out Vector<int> sHi);
                            var lo = dLo + sLo;
                            var hi = dHi + sHi;
                            lo = Vector.Min(Vector.Max(lo, minI), maxI);
                            hi = Vector.Min(Vector.Max(hi, minI), maxI);
                            var packed = Vector.Narrow(lo, hi);
                            packed.CopyTo(dst.Slice(s));
                        }
                        for (int s = v; s < samples; s++)
                        {
                            int sum = dst[s] + src[s];
                            if (sum > short.MaxValue) sum = short.MaxValue; else if (sum < short.MinValue) sum = short.MinValue;
                            dst[s] = (short)sum;
                        }
                    }
                    else
                    {
                        for (int s = 0; s < dst.Length; s++)
                        {
                            int sum = dst[s] + src[s];
                            if (sum > short.MaxValue) sum = short.MaxValue; else if (sum < short.MinValue) sum = short.MinValue;
                            dst[s] = (short)sum;
                        }
                    }
                }
                activeSources++;
            }

            if (activeSources == 0)
            {
                // No data ready; avoid pushing zeros. Yield very briefly.
                if (StrictLowLatency)
                {
                    Thread.SpinWait(5000);
                }
                else
                {
                    Thread.Sleep(1);
                }
                continue;
            }

            // serialize mixShorts to bytes and apply global volume
            Buffer.BlockCopy(mixShorts, 0, mixBytes, 0, currChunkBytes);
            if (Math.Abs(_globalVolume - 1f) > 0.0001f)
            {
                ApplyVolume16(mixBytes.AsSpan(0, currChunkBytes), _globalVolume);
            }
            _audioOutput.Write(mixBytes, 0, currChunkBytes);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int DetermineMixChunkBytes(StreamBuffer[] buffers, int fallback)
    {
        int min = int.MaxValue;
        for (int i = 0; i < buffers.Length; i++)
        {
            int fb = buffers[i].GetFrameBytes();
            if (fb > 0 && fb < min) min = fb;
        }
        if (min == int.MaxValue) return fallback;
        if ((min % 4) != 0) min -= (min % 4);
        if (min < 4) min = 4;
        return min;
    }

    private sealed class StreamBuffer
    {
        private readonly Queue<byte[]> _queue = new();
        private int _offset = 0;
        private readonly object _lock = new();
        private int _queuedBytes = 0;
        private readonly int _bytesPerChunk;
        private int _maxQueuedChunks;
        private int _minReadyChunks;
        public float Volume = 1f;
        private int _frameBytes;

        public StreamBuffer(int bytesPerChunk, int maxQueuedChunks, int minReadyChunks = 1)
        {
            _bytesPerChunk = bytesPerChunk;
            _maxQueuedChunks = Math.Max(1, maxQueuedChunks);
            _minReadyChunks = Math.Max(1, Math.Min(_maxQueuedChunks, minReadyChunks));
        }

        public void UpdateMaxQueuedChunks(int maxQueuedChunks)
        {
            lock (_lock)
            {
                _maxQueuedChunks = Math.Max(1, maxQueuedChunks);
                if (_minReadyChunks > _maxQueuedChunks)
                    _minReadyChunks = _maxQueuedChunks;
                TrimIfNeeded();
            }
        }

        public void UpdateMinReadyChunks(int minReadyChunks)
        {
            lock (_lock)
            {
                _minReadyChunks = Math.Max(1, Math.Min(_maxQueuedChunks, minReadyChunks));
            }
        }

        public void Enqueue(byte[] data)
        {
            lock (_lock)
            {
                _queue.Enqueue(data);
                _queuedBytes += data.Length;
                _frameBytes = data.Length;
                // Trim oldest data if queue grows too large to limit latency
                while (_queuedBytes > (_bytesPerChunk * _maxQueuedChunks) && _queue.Count > 0)
                {
                    var dropped = _queue.Dequeue();
                    _queuedBytes -= dropped.Length;
                    // reset offset if queue was just one element partially read
                    if (_offset > 0)
                    {
                        // if we dropped the element under read, reset offset to 0
                        _offset = 0;
                    }
                }
            }
        }

        private void TrimIfNeeded()
        {
            while (_queuedBytes > (_bytesPerChunk * _maxQueuedChunks) && _queue.Count > 0)
            {
                var dropped = _queue.Dequeue();
                _queuedBytes -= dropped.Length;
                if (_offset > 0) _offset = 0;
            }
        }

        public int Read(Span<byte> destination)
        {
            int copied = 0;
            lock (_lock)
            {
                // No prebuffering to avoid extra latency
                while (copied < destination.Length && _queue.Count > 0)
                {
                    var current = _queue.Peek();
                    int avail = current.Length - _offset;
                    int toCopy = Math.Min(avail, destination.Length - copied);
                    current.AsSpan(_offset, toCopy).CopyTo(destination.Slice(copied, toCopy));
                    copied += toCopy;
                    _offset += toCopy;
                    if (_offset >= current.Length)
                    {
                        _queue.Dequeue();
                        _queuedBytes -= current.Length;
                        _offset = 0;
                    }
                }
            }
            return copied;
        }

        public int GetFrameBytes()
        {
            lock (_lock) { return _frameBytes; }
        }
    }

    private static byte[] ConvertToPcm16Interleaved(byte[] input, int inBits, int inCh, int outCh)
    {
        // Fast path: 16-bit PCM already
        if (inBits == 16)
        {
            if (inCh == outCh)
                return input; // already correct

            int frames = input.Length / (inCh * 2);
            byte[] outBuf = new byte[frames * outCh * 2];
            var inShorts = MemoryMarshal.Cast<byte, short>(input.AsSpan());
            var outShorts = MemoryMarshal.Cast<byte, short>(outBuf.AsSpan());

            if (outCh == 1)
            {
                // Downmix to mono: average first two channels (or duplicate mono)
                for (int f = 0; f < frames; f++)
                {
                    int baseIn = f * inCh;
                    short l = inShorts[baseIn];
                    short r = inCh >= 2 ? inShorts[baseIn + 1] : l;
                    outShorts[f] = (short)((l + r) / 2);
                }
            }
            else // outCh == 2
            {
                if (inCh == 1)
                {
                    // Upmix mono to stereo
                    for (int f = 0; f < frames; f++)
                    {
                        short s = inShorts[f];
                        int outBase = f * 2;
                        outShorts[outBase] = s;
                        outShorts[outBase + 1] = s;
                    }
                }
                else
                {
                    // Take first two channels from multichannel
                    for (int f = 0; f < frames; f++)
                    {
                        int baseIn = f * inCh;
                        int outBase = f * 2;
                        outShorts[outBase] = inShorts[baseIn];
                        outShorts[outBase + 1] = inShorts[baseIn + 1];
                    }
                }
            }
            return outBuf;
        }

        // Convert any integer format to 16-bit mono/stereo
        int inBps = inBits / 8;
        int framesCount = input.Length / (inCh * inBps);
        short[] tmpOut = new short[framesCount * outCh];

        for (int f = 0; f < framesCount; f++)
        {
            // Read first two channels (or one) as 32-bit intermediate
            int baseIdx = f * inCh * inBps;

            int s0 = ReadSampleAsInt(input, baseIdx + 0 * inBps, inBits);
            int s1 = inCh >= 2 ? ReadSampleAsInt(input, baseIdx + 1 * inBps, inBits) : s0;

            short l = ToInt16(s0, inBits);
            short r = ToInt16(s1, inBits);

            if (outCh == 1)
            {
                // mono: average
                int m = (l + r) / 2;
                tmpOut[f] = (short)m;
            }
            else
            {
                int outBase = f * 2;
                tmpOut[outBase] = l;
                tmpOut[outBase + 1] = r;
            }
        }

        // Serialize to bytes LE
        byte[] outBytes = new byte[tmpOut.Length * 2];
        Buffer.BlockCopy(tmpOut, 0, outBytes, 0, outBytes.Length);
        return outBytes;
    }

    private static int ReadSampleAsInt(byte[] buffer, int offset, int bits)
    {
        return bits switch
        {
            // 8-bit PCM: assume signed [-128..127]
            8 => (sbyte)buffer[offset],
            // 16-bit PCM LE
            16 => BitConverter.ToInt16(buffer, offset),
            // 24-bit PCM LE: sign-extend to 32-bit
            24 =>
            (
                (buffer[offset] | (buffer[offset + 1] << 8) | (buffer[offset + 2] << 16))
                | ((buffer[offset + 2] & 0x80) != 0 ? unchecked((int)0xFF000000) : 0)
            ),
            // 32-bit PCM LE
            32 => BitConverter.ToInt32(buffer, offset),
            _ => 0
        };
    }

    private static void ApplyVolume16(Span<byte> buffer, float gain)
    {
        if (Math.Abs(gain - 1f) < 0.0001f) return;
        int q15 = (int)Math.Round(gain * 32768.0f);
        int bias = (q15 >= 0 ? 16384 : -16384);
        var s16 = MemoryMarshal.Cast<byte, short>(buffer);

        if (Vector.IsHardwareAccelerated && s16.Length >= Vector<short>.Count)
        {
            int vszShort = Vector<short>.Count;
            int vszInt = Vector<int>.Count;
            var qVec = new Vector<int>(q15);
            var biasVec = new Vector<int>(bias);
            var minI = new Vector<int>(short.MinValue);
            var maxI = new Vector<int>(short.MaxValue);
            int v = (s16.Length / vszShort) * vszShort;
            for (int i = 0; i < v; i += vszShort)
            {
                var vS = new Vector<short>(s16.Slice(i));
                Vector.Widen(vS, out Vector<int> lo, out Vector<int> hi);
                lo = ((lo * qVec) + biasVec) >> 15;
                hi = ((hi * qVec) + biasVec) >> 15;
                lo = Vector.Min(Vector.Max(lo, minI), maxI);
                hi = Vector.Min(Vector.Max(hi, minI), maxI);
                var packed = Vector.Narrow(lo, hi);
                packed.CopyTo(s16.Slice(i));
            }
            // tail
            for (int i = v; i < s16.Length; i++)
            {
                int scaled = (s16[i] * q15 + bias) >> 15;
                if (scaled > short.MaxValue) scaled = short.MaxValue; else if (scaled < short.MinValue) scaled = short.MinValue;
                s16[i] = (short)scaled;
            }
        }
        else
        {
            for (int i = 0; i < s16.Length; i++)
            {
                int scaled = (s16[i] * q15 + bias) >> 15;
                if (scaled > short.MaxValue) scaled = short.MaxValue; else if (scaled < short.MinValue) scaled = short.MinValue;
                s16[i] = (short)scaled;
            }
        }
    }

    private static short ToInt16(int sample, int inBits)
    {
        return inBits switch
        {
            8 => (short)(sample << 8), // expand to 16-bit
            16 => (short)sample,
            24 => (short)(sample >> 8),
            32 => (short)(sample >> 16),
            _ => 0
        };
    }

    private void OnListeningStateChange(bool active) => ListeningState = active;

    private void EndpointsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.Action == NotifyCollectionChangedAction.Remove && e.OldItems != null)
        {
            for (int i = 0; i < e.OldItems.Count; i++)
            {
                var endpoint = (Endpoint)e.OldItems[i]!;
                _ = _endpointRepository.RemoveAsync(endpoint.Id);
            }
        }
        else if (e.Action == NotifyCollectionChangedAction.Add && e.NewItems != null)
        {
            for (int i = 0; i < e.NewItems.Count; i++)
            {
                var endpoint = (Endpoint)e.NewItems[i]!;
                endpoint.PropertyChanged += EndpointPropertyChanged;
                var dto = _mapper.Map<EndpointDto>(endpoint);
                _ = _endpointRepository.CreateAsync(dto);
            }
        }
    }

    private void EndpointPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is not Endpoint endpoint) return;
        var dto = _mapper.Map<EndpointDto>(endpoint);
        _ = _endpointRepository.UpdateAsync(dto);
        if (e.PropertyName == nameof(Endpoint.Quality))
        {
            lock (_buffers)
            {
                if (_buffers.TryGetValue(endpoint.Id, out var buf))
                {
                    buf.UpdateMaxQueuedChunks(ComputeMaxQueuedChunks(endpoint.Quality));
                    buf.UpdateMinReadyChunks(ComputeMinReadyChunks(endpoint.Quality));
                }
            }
        }
        else if (e.PropertyName == nameof(Endpoint.Volume))
        {
            lock (_buffers)
            {
                if (_buffers.TryGetValue(endpoint.Id, out var buf))
                {
                    buf.Volume = endpoint.Volume;
                }
            }
        }
        else if (e.PropertyName == nameof(Endpoint.IsDenoiseEnabled) && !endpoint.IsDenoiseEnabled)
        {
            // Clear denoise accumulators when turning off
            if (_denoisers.TryGetValue(endpoint.Id, out var d))
            {
                d.PendingCount = 0; d.ResInCount = 0; d.ResPos = 0;
            }
        }
    }
}
