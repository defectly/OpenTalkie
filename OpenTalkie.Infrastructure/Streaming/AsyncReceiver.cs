using OpenTalkie.Domain.Rules;
using OpenTalkie.Domain.VBAN;
using OpenTalkie.Infrastructure.RNNoise;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Net;
using System.Net.Sockets;
using System.Runtime.CompilerServices;

namespace OpenTalkie.Infrastructure.Streaming;

public class AsyncReceiver : IDisposable
{
    private readonly ObservableCollection<Endpoint> _endpoints;
    private readonly Dictionary<int, Listener> _listeners = new();
    private bool _started;

    public AsyncReceiver(ObservableCollection<Endpoint> endpoints)
    {
        _endpoints = endpoints ?? throw new ArgumentNullException(nameof(endpoints));
        _endpoints.CollectionChanged += OnEndpointsCollectionChanged;
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
        var values = _listeners.Values.ToList();
        for (int i = 0; i < values.Count; i++)
            values[i].Dispose();
        _listeners.Clear();
    }

    private void OnEndpointsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (!_started) return;
        RebuildListeners();
    }

    private void RebuildListeners()
    {
        var enabledByPort = _endpoints
            .Where(e => e.IsEnabled)
            .GroupBy(e => e.Port)
            .ToDictionary(g => g.Key, g => g.ToList());

        var keys = _listeners.Keys.ToList();
        for (int i = 0; i < keys.Count; i++)
        {
            var port = keys[i];
            if (!enabledByPort.ContainsKey(port))
            {
                _listeners[port].Dispose();
                _listeners.Remove(port);
            }
        }

        var endpointGroups = enabledByPort.ToArray();
        for (int i = 0; i < endpointGroups.Length; i++)
        {
            var port = endpointGroups[i].Key;
            var eps = endpointGroups[i].Value;
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
        GC.SuppressFinalize(this);
    }

    private sealed class Listener : IDisposable
    {
        private readonly int _port;
        private List<Endpoint> _eps;
        private readonly Action<Endpoint, byte[], int, int, int> _onPayload;
        private UdpClient? _udp;
        private CancellationTokenSource? _cts;
        private readonly Dictionary<Guid, DenoiseCtx> _denoisers = new();
        private readonly Dictionary<Guid, uint> _lastFrame = new();
        private readonly Dictionary<Guid, HeaderSig> _lastHeader = new();

        private sealed class DenoiseCtx
        {
            public Denoiser Dn = new();
            public byte[] Pending = Array.Empty<byte>();
            public int PendingCount;
            public short[] ResIn = Array.Empty<short>();
            public int ResInCount;
            public double ResPos;
            public int LastInRate = 48000;
            public void ClearResampler() { ResInCount = 0; ResPos = 0; }
            public void ClearPending() { PendingCount = 0; }
        }

        private readonly struct HeaderSig
        {
            public readonly int SampleRate;
            public readonly int Channels;
            public readonly int BitsPerSample;
            public readonly int SamplesPerFrame;
            public HeaderSig(int sr, int ch, int bps, int spf)
            {
                SampleRate = sr; Channels = ch; BitsPerSample = bps; SamplesPerFrame = spf;
            }
        }

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
            try
            {
                var sock = _udp.Client;
                sock.ReceiveBufferSize = Math.Max(sock.ReceiveBufferSize, 1 << 20);
                try { sock.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true); } catch { }
            }
            catch { }
            _cts = new CancellationTokenSource();
            _ = Task.Run(() => LoopAsync(_cts.Token));
        }

        public void UpdateEndpoints(List<Endpoint> endpoints)
        {
            _eps = endpoints;
            var active = new HashSet<Guid>(endpoints.Select(e => e.Id));
            var denoiserKeys = _denoisers.Keys.ToList();
            for (int i = 0; i < denoiserKeys.Count; i++)
            {
                var key = denoiserKeys[i];
                if (!active.Contains(key))
                {
                    _denoisers[key].Dn.Dispose();
                    _denoisers.Remove(key);
                }
            }

            var frameKeys = _lastFrame.Keys.ToList();
            for (int i = 0; i < frameKeys.Count; i++)
            {
                var key = frameKeys[i];
                if (!active.Contains(key))
                {
                    _lastFrame.Remove(key);
                }
            }

            var headerKeys = _lastHeader.Keys.ToList();
            for (int i = 0; i < headerKeys.Count; i++)
            {
                var key = headerKeys[i];
                if (!active.Contains(key))
                {
                    _lastHeader.Remove(key);
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
                ReadOnlySpan<byte> packetStreamName = buf.AsSpan(8, 16);

                var remoteAddress = res.RemoteEndPoint.Address;
                for (int endpointIndex = 0; endpointIndex < _eps.Count; endpointIndex++)
                {
                    var ep = _eps[endpointIndex];
                    if (!ep.IsEnabled) continue;
                    if (!StreamNameEquals(packetStreamName, ep)) continue;
                    if (!string.IsNullOrWhiteSpace(ep.Hostname) && IPAddress.TryParse(ep.Hostname, out var expectedIp))
                    {
                        if (!remoteAddress.Equals(expectedIp))
                            continue;
                    }

                    int header = 28;
                    uint frame = BitConverter.ToUInt32(buf, 24);
                    bool headerChanged = false;
                    var currHdr = new HeaderSig(sampleRate, channels, bitsPerSample, samplesPerFrame);
                    if (_lastHeader.TryGetValue(ep.Id, out var prevHdr))
                    {
                        if (prevHdr.SampleRate != currHdr.SampleRate ||
                            prevHdr.Channels != currHdr.Channels ||
                            prevHdr.BitsPerSample != currHdr.BitsPerSample ||
                            prevHdr.SamplesPerFrame != currHdr.SamplesPerFrame)
                        {
                            headerChanged = true;
                        }
                    }

                    if (headerChanged)
                    {
                        _lastHeader[ep.Id] = currHdr;
                        _lastFrame[ep.Id] = frame;
                    }
                    else
                    {
                        _lastHeader[ep.Id] = currHdr;
                        if (_lastFrame.TryGetValue(ep.Id, out var last))
                        {
                            int diff = unchecked((int)(frame - last));
                            if (diff <= 0)
                            {
                                if (last > 10000 && frame < 1000)
                                {
                                    _lastFrame[ep.Id] = frame;
                                }
                                else
                                {
                                    continue;
                                }
                            }
                            else
                            {
                                _lastFrame[ep.Id] = frame;
                            }
                        }
                        else
                        {
                            _lastFrame[ep.Id] = frame;
                        }
                    }
                    int payloadBytes = buf.Length - header;
                    if (payloadBytes <= 0) continue;
                    int bps = bitsPerSample / 8;
                    if ((payloadBytes % (channels * bps)) != 0) continue;

                    var payload = new byte[payloadBytes];
                    Buffer.BlockCopy(buf, header, payload, 0, payloadBytes);

                    if (headerChanged && _denoisers.TryGetValue(ep.Id, out var ctx2))
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
        private static bool StreamNameEquals(ReadOnlySpan<byte> packetStreamName, Endpoint endpoint)
        {
            return VbanStreamName16.EqualsPacketName(packetStreamName, endpoint.Name);
        }

        public void Dispose()
        {
            _cts?.Cancel();
            _cts?.Dispose();
            _udp?.Dispose();
            var denoisers = _denoisers.Values.ToList();
            for (int i = 0; i < denoisers.Count; i++) denoisers[i].Dn.Dispose();
            _denoisers.Clear();
        }
    }
}
