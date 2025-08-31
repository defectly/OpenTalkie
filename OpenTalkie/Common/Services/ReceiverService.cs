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
    private readonly IMapper _mapper;
    private readonly IEndpointRepository _endpointRepository;
    private readonly IAudioOutputService _audioOutput;
    private AsyncReceiver? _receiver;
    private CancellationTokenSource? _mixCts;
    private Task? _mixTask;
    private readonly Dictionary<Guid, StreamBuffer> _buffers = new();
    private volatile StreamBuffer[] _activeBuffers = Array.Empty<StreamBuffer>();
    private readonly object _outputLock = new();
    private int _currentSampleRate;
    private int _currentChannels;
    private const int TargetSampleRate = 48000;
    private const int TargetChannels = 2;
    private const int MixChunkSamples = 480; // per channel (~10ms at 48k)
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
        // Start output once at target format and begin mixer loop
        EnsureOutput(TargetSampleRate, TargetChannels);
        _mixCts = new CancellationTokenSource();
        _mixTask = Task.Run(() => MixLoopAsync(_mixCts.Token));
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
        lock (_buffers)
        {
            _buffers.Clear();
        }
        Volatile.Write(ref _activeBuffers, Array.Empty<StreamBuffer>());
        ListeningStateChanged?.Invoke(false);
    }

    public void Switch()
    {
        if (ListeningState) Stop(); else Start();
    }

    private void OnFrameReceived(Endpoint ep, byte[] payload, WaveFormat wf)
    {
        // Convert to target format and enqueue for mixing
        byte[] pcm16 = ConvertToPcm16Interleaved(payload, wf.BitsPerSample, wf.Channels, TargetChannels);
        if (wf.SampleRate != TargetSampleRate)
        {
            pcm16 = ResamplePcm16Interleaved(pcm16, wf.SampleRate, TargetSampleRate, TargetChannels);
        }
        if (ep.Volume != 1f)
        {
            ApplyVolume16(pcm16.AsSpan(), ep.Volume);
        }
        bool added = false;
        lock (_buffers)
        {
            if (!_buffers.TryGetValue(ep.Id, out var buf))
            {
                buf = new StreamBuffer(BytesPerChunk, ComputeMaxQueuedChunks(ep.Quality));
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

    private static int ComputeMaxQueuedChunks(OpenTalkie.VBAN.VBanQuality quality)
    {
        // Interpret VBanQuality as reference samples per channel at 48kHz
        int referenceSamples = (int)quality; // 512, 1024, 2048, 4096, 8192
        int chunks = (int)Math.Ceiling(referenceSamples / (double)MixChunkSamples);
        if (chunks < 1) chunks = 1;
        if (chunks > 64) chunks = 64; // cap for sanity
        return chunks;
    }

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
        byte[] mixBytes = new byte[BytesPerChunk];
        short[] mixShorts = new short[MixChunkSamples * TargetChannels];
        byte[] temp = new byte[BytesPerChunk];

        while (!ct.IsCancellationRequested)
        {
            Array.Clear(mixShorts, 0, mixShorts.Length);
            int activeSources = 0;
            var buffers = Volatile.Read(ref _activeBuffers);
            for (int i = 0; i < buffers.Length; i++)
            {
                var buf = buffers[i];
                int read = buf.Read(temp);
                if (read <= 0) continue;
                if (read < BytesPerChunk)
                {
                    // pad missing with zeros
                    Array.Clear(temp, read, BytesPerChunk - read);
                }
                // sum into mixShorts; prefer SIMD saturating add when available
                {
                    var dst = mixShorts.AsSpan();
                    var src = MemoryMarshal.Cast<byte, short>(temp.AsSpan(0, BytesPerChunk));

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

            // serialize mixShorts to bytes and apply global volume
            Buffer.BlockCopy(mixShorts, 0, mixBytes, 0, mixBytes.Length);
            if (Math.Abs(_globalVolume - 1f) > 0.0001f)
            {
                ApplyVolume16(mixBytes.AsSpan(), _globalVolume);
            }
            _audioOutput.Write(mixBytes, 0, mixBytes.Length);
        }
    }

    private sealed class StreamBuffer
    {
        private readonly Queue<byte[]> _queue = new();
        private int _offset = 0;
        private readonly object _lock = new();
        private int _queuedBytes = 0;
        private readonly int _bytesPerChunk;
        private int _maxQueuedChunks;

        public StreamBuffer(int bytesPerChunk, int maxQueuedChunks)
        {
            _bytesPerChunk = bytesPerChunk;
            _maxQueuedChunks = Math.Max(1, maxQueuedChunks);
        }

        public void UpdateMaxQueuedChunks(int maxQueuedChunks)
        {
            lock (_lock)
            {
                _maxQueuedChunks = Math.Max(1, maxQueuedChunks);
                TrimIfNeeded();
            }
        }

        public void Enqueue(byte[] data)
        {
            lock (_lock)
            {
                _queue.Enqueue(data);
                _queuedBytes += data.Length;
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

            if (outCh == 1)
            {
                // Downmix to mono: average first two channels (or duplicate mono)
                for (int f = 0; f < frames; f++)
                {
                    int inBase = f * inCh * 2;
                    short l = BitConverter.ToInt16(input, inBase);
                    short r = inCh >= 2 ? BitConverter.ToInt16(input, inBase + 2) : l;
                    short m = (short)((l + r) / 2);
                    Buffer.BlockCopy(BitConverter.GetBytes(m), 0, outBuf, f * 2, 2);
                }
            }
            else // outCh == 2
            {
                if (inCh == 1)
                {
                    // Upmix mono to stereo
                    for (int f = 0; f < frames; f++)
                    {
                        short s = BitConverter.ToInt16(input, f * 2);
                        int outBase = f * 4;
                        Buffer.BlockCopy(BitConverter.GetBytes(s), 0, outBuf, outBase, 2);
                        Buffer.BlockCopy(BitConverter.GetBytes(s), 0, outBuf, outBase + 2, 2);
                    }
                }
                else
                {
                    // Take first two channels from multichannel
                    for (int f = 0; f < frames; f++)
                    {
                        int inBase = f * inCh * 2;
                        int outBase = f * 4;
                        outBuf[outBase + 0] = input[inBase + 0];
                        outBuf[outBase + 1] = input[inBase + 1];
                        outBuf[outBase + 2] = input[inBase + 2];
                        outBuf[outBase + 3] = input[inBase + 3];
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
                }
            }
        }
    }
}
