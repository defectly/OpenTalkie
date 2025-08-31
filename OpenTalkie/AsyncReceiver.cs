using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Net;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using OpenTalkie.VBAN;
using OpenTalkie.RNNoise;

namespace OpenTalkie;

public class AsyncReceiver : IDisposable
{
    private readonly ObservableCollection<Endpoint> _endpoints;
    private readonly Dictionary<int, Listener> _listeners = new();
    private bool _started;

    public AsyncReceiver(ObservableCollection<Endpoint> endpoints)
    {
        _endpoints = endpoints ?? throw new ArgumentNullException(nameof(endpoints));
        _endpoints.CollectionChanged += OnEndpointsCollectionChanged;
        for (int i = 0; i < _endpoints.Count; i++)
            _endpoints[i].PropertyChanged += OnEndpointPropertyChanged;
    }

    public event Action<Endpoint, byte[], WaveFormat>? FrameReceived;

    public void Start()
    {
        if (_started) return;
        _started = true;
        RebuildListeners();
    }

    public void Stop()
    {
        _started = false;
        var __vals = _listeners.Values.ToList();
        for (int __i = 0; __i < __vals.Count; __i++)
            __vals[__i].Dispose();
        _listeners.Clear();
    }

    private void OnEndpointsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (!_started) return;
        if (e.Action == NotifyCollectionChangedAction.Add && e.NewItems != null)
        {
            for (int i = 0; i < e.NewItems.Count; i++)
                ((Endpoint)e.NewItems[i]!).PropertyChanged += OnEndpointPropertyChanged;
        }
        if (e.Action == NotifyCollectionChangedAction.Remove && e.OldItems != null)
        {
            for (int i = 0; i < e.OldItems.Count; i++)
                ((Endpoint)e.OldItems[i]!).PropertyChanged -= OnEndpointPropertyChanged;
        }
        RebuildListeners();
    }

    private void OnEndpointPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (!_started) return;
        if (e.PropertyName == nameof(Endpoint.Port) ||
            e.PropertyName == nameof(Endpoint.IsEnabled) ||
            e.PropertyName == nameof(Endpoint.Hostname) ||
            e.PropertyName == nameof(Endpoint.Name))
        {
            RebuildListeners();
        }
    }

    private void RebuildListeners()
    {
        var enabledByPort = _endpoints
            .Where(e => e.IsEnabled)
            .GroupBy(e => e.Port)
            .ToDictionary(g => g.Key, g => g.ToList());

        var __keys = _listeners.Keys.ToList();
        for (int __i = 0; __i < __keys.Count; __i++)
        {
            var port = __keys[__i];
            if (!enabledByPort.ContainsKey(port))
            {
                _listeners[port].Dispose();
                _listeners.Remove(port);
            }
        }

        var __kvps = enabledByPort.ToArray();
        for (int __i = 0; __i < __kvps.Length; __i++)
        {
            var port = __kvps[__i].Key;
            var eps = __kvps[__i].Value;
            if (_listeners.TryGetValue(port, out var listener))
            {
                listener.UpdateEndpoints(eps);
            }
            else
            {
                try
                {
                    var l = new Listener(port, eps, OnVbanPayload);
                    l.Start();
                    _listeners.Add(port, l);
                }
                catch
                {
                    // ignore bind errors
                }
            }
        }
    }

    private void OnVbanPayload(Endpoint ep, byte[] payload, int channels, int bitsPerSample, int sampleRate)
    {
        FrameReceived?.Invoke(ep, payload, new WaveFormat(bitsPerSample, channels, sampleRate));
    }

    public void Dispose()
    {
        Stop();
        _endpoints.CollectionChanged -= OnEndpointsCollectionChanged;
        for (int i = 0; i < _endpoints.Count; i++)
            _endpoints[i].PropertyChanged -= OnEndpointPropertyChanged;
        GC.SuppressFinalize(this);
    }

    private sealed class Listener : IDisposable
    {
        private readonly int _port;
        private List<Endpoint> _eps;
        private readonly Action<Endpoint, byte[], int, int, int> _onPayload;
        private UdpClient? _udp;
        private CancellationTokenSource? _cts;
        // Keep RNNoise + resampler state per endpoint
        private sealed class DenoiseCtx
        {
            public Denoiser Dn = new();
            public byte[] Pending = Array.Empty<byte>(); // 48k mono 16-bit pending bytes for 480-sample frames
            public int PendingCount;
            public short[] ResIn = Array.Empty<short>(); // mono 16-bit input samples for resampler
            public int ResInCount;
            public double ResPos; // fractional position in ResIn
            public int LastInRate = 48000;
            public void ClearResampler()
            {
                ResInCount = 0;
                ResPos = 0;
            }
            public void ClearPending()
            {
                PendingCount = 0;
            }
        }

        private readonly Dictionary<Guid, DenoiseCtx> _denoisers = new();

        public Listener(int port, List<Endpoint> endpoints, Action<Endpoint, byte[], int, int, int> onPayload)
        {
            _port = port;
            _eps = endpoints;
            _onPayload = onPayload;
        }

        public void Start()
        {
            if (_udp != null) return;
            _udp = new UdpClient(new IPEndPoint(IPAddress.Any, _port));
            _cts = new CancellationTokenSource();
            _ = Task.Run(() => LoopAsync(_cts.Token));
        }

        public void UpdateEndpoints(List<Endpoint> endpoints)
        {
            _eps = endpoints;
            // Clean up denoisers for endpoints that were removed
            var active = new HashSet<Guid>(endpoints.Select(e => e.Id));
            var __dkeys = _denoisers.Keys.ToList();
            for (int i = 0; i < __dkeys.Count; i++)
            {
                var key = __dkeys[i];
                if (!active.Contains(key))
                {
                    _denoisers[key].Dn.Dispose();
                    _denoisers.Remove(key);
                }
            }
        }

        private async Task LoopAsync(CancellationToken ct)
        {
            if (_udp == null) return;
            while (!ct.IsCancellationRequested)
            {
                UdpReceiveResult res;
                try { res = await _udp.ReceiveAsync(ct); }
                catch (OperationCanceledException) { break; }
                catch { continue; }

                var buf = res.Buffer;
                if (buf.Length < 28) continue;
                if (buf[0] != (byte)'V' || buf[1] != (byte)'B' || buf[2] != (byte)'A' || buf[3] != (byte)'N') continue;

                byte b4 = buf[4];
                if (((VBanProtocol)(b4 & 0xE0)) != VBanProtocol.VBAN_PROTOCOL_AUDIO) continue;

                int srIdx = b4 & 0x1F;
                if (srIdx < 0 || srIdx >= VBANConsts.SAMPLERATES.Length) continue;
                int sampleRate = VBANConsts.SAMPLERATES[srIdx];

                int samplesPerFrame = (buf[5] & 0xFF) + 1;
                int channels = (buf[6] & 0xFF) + 1;

                byte b7 = buf[7];
                if (((VBanCodec)(b7 & 0xE0)) != VBanCodec.VBAN_CODEC_PCM) continue;
                int bitsPerSample = (b7 & 0x1F) switch
                {
                    (int)VBanBitResolution.VBAN_BITFMT_8_INT => 8,
                    (int)VBanBitResolution.VBAN_BITFMT_16_INT => 16,
                    (int)VBanBitResolution.VBAN_BITFMT_24_INT => 24,
                    (int)VBanBitResolution.VBAN_BITFMT_32_INT => 32,
                    _ => 0
                };
                if (bitsPerSample == 0) continue;

                string name = GetStreamName(buf);

                var remoteAddress = res.RemoteEndPoint.Address;
                for (int __ei = 0; __ei < _eps.Count; __ei++)
                {
                    var ep = _eps[__ei];
                    if (!ep.IsEnabled) continue;
                    if (!string.Equals(Normalize(ep.Name), name, StringComparison.Ordinal)) continue;
                    // If endpoint hostname is an IP, require sender IP to match
                    if (!string.IsNullOrWhiteSpace(ep.Hostname) && System.Net.IPAddress.TryParse(ep.Hostname, out var expectedIp))
                    {
                        if (!remoteAddress.Equals(expectedIp))
                            continue;
                    }

                    int header = 28;
                    int payloadBytes = buf.Length - header;
                    if (payloadBytes <= 0) continue;
                    int bps = bitsPerSample / 8;
                    if (payloadBytes < samplesPerFrame * channels * bps && payloadBytes % (channels * bps) != 0) continue;

                    var payload = new byte[payloadBytes];
                    Buffer.BlockCopy(buf, header, payload, 0, payloadBytes);

                    if (ep.IsDenoiseEnabled)
                    {
                        // Convert any format to 48kHz, mono, 16-bit for RNNoise, then denoise
                        if (!_denoisers.TryGetValue(ep.Id, out var ctx))
                        {
                            ctx = new DenoiseCtx();
                            _denoisers[ep.Id] = ctx;
                        }

                        // 1) Convert to mono 16-bit at source rate
                        var mono16 = ConvertToMono16(payload, bitsPerSample, channels);

                        // 2) Resample to 48k mono 16-bit
                        var res48 = ResampleTo48k(mono16, sampleRate, ctx);

                        // 3) Accumulate to 480-sample blocks and denoise
                        int frameBytes = 480 * 2;
                        if (res48.Length > 0)
                        {
                            int addBytes = res48.Length; // res48 already in bytes (16-bit LE)
                            int need = ctx.PendingCount + addBytes;
                            if (ctx.Pending.Length < need)
                            {
                                int newCap = Math.Max(need, ctx.Pending.Length == 0 ? 4096 : ctx.Pending.Length * 2);
                                var nb = new byte[newCap];
                                if (ctx.PendingCount > 0) Buffer.BlockCopy(ctx.Pending, 0, nb, 0, ctx.PendingCount);
                                ctx.Pending = nb;
                            }
                            Buffer.BlockCopy(res48, 0, ctx.Pending, ctx.PendingCount, addBytes);
                            ctx.PendingCount += addBytes;

                            int frames = ctx.PendingCount / frameBytes;
                            if (frames > 0)
                            {
                                int outLen = frames * frameBytes;
                                var outBuf = new byte[outLen];
                                for (int i = 0; i < frames; i++)
                                {
                                    int off = i * frameBytes;
                                    // Denoise in place on pending buffer to avoid per-frame allocations
                                    ctx.Dn.Denoise(ctx.Pending, off, frameBytes, false);
                                    Buffer.BlockCopy(ctx.Pending, off, outBuf, off, frameBytes);
                                }

                                int remain = ctx.PendingCount - outLen;
                                if (remain > 0) Buffer.BlockCopy(ctx.Pending, outLen, ctx.Pending, 0, remain);
                                ctx.PendingCount = remain;

                                try { _onPayload(ep, outBuf, 1, 16, 48000); } catch { }
                                continue;
                            }
                            else
                            {
                                continue; // not enough for a full denoise frame yet
                            }
                        }
                        else
                        {
                            continue; // no samples after conversion/resample yet
                        }
                    }

                    // Denoise disabled -> clear any accumulators and pass original
                    if (_denoisers.TryGetValue(ep.Id, out var ctx2))
                    {
                        ctx2.ClearPending();
                        ctx2.ClearResampler();
                    }
                    try { _onPayload(ep, payload, channels, bitsPerSample, sampleRate); }
                    catch { }
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static string GetStreamName(byte[] b)
        {
            Span<byte> s = b.AsSpan(8, 16);
            int len = s.IndexOf((byte)0);
            if (len < 0) len = 16;
            return System.Text.Encoding.ASCII.GetString(s.Slice(0, len));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static string Normalize(string name)
        {
            if (string.IsNullOrEmpty(name)) return string.Empty;
            return name.Length <= 16 ? name : name.Substring(0, 16);
        }

        public void Dispose()
        {
            _cts?.Cancel();
            _cts?.Dispose();
            _udp?.Dispose();
            var __dvals = _denoisers.Values.ToList();
            for (int i = 0; i < __dvals.Count; i++) __dvals[i].Dn.Dispose();
            _denoisers.Clear();
        }

        // ---------- Conversion helpers ----------
        private static short[] ConvertToMono16(byte[] input, int bits, int channels)
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

        private static int ReadSampleAsInt(byte[] buffer, int offset, int bits)
        {
            return bits switch
            {
                // 8-bit PCM is typically unsigned; convert to signed centered at 0
                8 => (int)buffer[offset] - 128,
                16 => BitConverter.ToInt16(buffer, offset),
                24 =>
                    ((buffer[offset] | (buffer[offset + 1] << 8) | (buffer[offset + 2] << 16))
                    | ((buffer[offset + 2] & 0x80) != 0 ? unchecked((int)0xFF000000) : 0)),
                32 => BitConverter.ToInt32(buffer, offset),
                _ => 0
            };
        }

        private static short ToInt16(int sample, int inBits)
        {
            return inBits switch
            {
                8 => (short)(sample << 8),
                16 => (short)sample,
                24 => (short)(sample >> 8),
                32 => (short)(sample >> 16),
                _ => 0
            };
        }

        private static byte[] ResampleTo48k(short[] mono16, int inRate, DenoiseCtx ctx)
        {
            if (mono16.Length == 0) return Array.Empty<byte>();
            if (inRate <= 0) return Array.Empty<byte>();

            if (ctx.LastInRate != inRate)
            {
                ctx.LastInRate = inRate;
                ctx.ClearResampler();
            }

            // Append input to resampler buffer
            int need = ctx.ResInCount + mono16.Length;
            if (ctx.ResIn.Length < need)
            {
                int newCap = Math.Max(need, ctx.ResIn.Length == 0 ? mono16.Length * 2 : ctx.ResIn.Length * 2);
                var nb = new short[newCap];
                if (ctx.ResInCount > 0)
                    Array.Copy(ctx.ResIn, 0, nb, 0, ctx.ResInCount);
                ctx.ResIn = nb;
            }
            Array.Copy(mono16, 0, ctx.ResIn, ctx.ResInCount, mono16.Length);
            ctx.ResInCount += mono16.Length;

            double step = (double)inRate / 48000.0;
            if (step <= 0) step = 1.0;
            var outList = new List<short>(ctx.ResInCount);

            // Generate while two points are available for interpolation
            while (ctx.ResPos + 1.0 < ctx.ResInCount)
            {
                int i0 = (int)ctx.ResPos;
                double frac = ctx.ResPos - i0;
                short s0 = ctx.ResIn[i0];
                short s1 = ctx.ResIn[i0 + 1];
                int interp = s0 + (int)((s1 - s0) * frac);
                outList.Add((short)interp);
                ctx.ResPos += step;
            }

            // Discard consumed input samples
            int consumed = Math.Max(0, (int)ctx.ResPos);
            if (consumed > 0)
            {
                int keep = ctx.ResInCount - consumed;
                if (keep > 0)
                    Array.Copy(ctx.ResIn, consumed, ctx.ResIn, 0, keep);
                ctx.ResInCount = keep;
                ctx.ResPos -= consumed;
            }

            if (outList.Count == 0) return Array.Empty<byte>();

            // Convert to bytes LE
            var outBytes = new byte[outList.Count * 2];
            Buffer.BlockCopy(outList.ToArray(), 0, outBytes, 0, outBytes.Length);
            return outBytes;
        }
    }
}
