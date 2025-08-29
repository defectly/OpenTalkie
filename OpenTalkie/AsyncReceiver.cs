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
        foreach (var ep in _endpoints)
            ep.PropertyChanged += OnEndpointPropertyChanged;
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
        foreach (var l in _listeners.Values)
            l.Dispose();
        _listeners.Clear();
    }

    private void OnEndpointsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (!_started) return;
        if (e.Action == NotifyCollectionChangedAction.Add && e.NewItems != null)
        {
            foreach (Endpoint ep in e.NewItems)
                ep.PropertyChanged += OnEndpointPropertyChanged;
        }
        if (e.Action == NotifyCollectionChangedAction.Remove && e.OldItems != null)
        {
            foreach (Endpoint ep in e.OldItems)
                ep.PropertyChanged -= OnEndpointPropertyChanged;
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

        foreach (var port in _listeners.Keys.ToList())
        {
            if (!enabledByPort.ContainsKey(port))
            {
                _listeners[port].Dispose();
                _listeners.Remove(port);
            }
        }

        foreach (var kv in enabledByPort)
        {
            var port = kv.Key;
            var eps = kv.Value;
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
        foreach (var ep in _endpoints)
            ep.PropertyChanged -= OnEndpointPropertyChanged;
        GC.SuppressFinalize(this);
    }

    private sealed class Listener : IDisposable
    {
        private readonly int _port;
        private List<Endpoint> _eps;
        private readonly Action<Endpoint, byte[], int, int, int> _onPayload;
        private UdpClient? _udp;
        private CancellationTokenSource? _cts;
        private readonly Denoiser _denoiser = new();

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

        public void UpdateEndpoints(List<Endpoint> endpoints) => _eps = endpoints;

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

                foreach (var ep in _eps)
                {
                    if (!ep.IsEnabled) continue;
                    if (!string.Equals(Normalize(ep.Name), name, StringComparison.Ordinal)) continue;

                    int header = 28;
                    int payloadBytes = buf.Length - header;
                    if (payloadBytes <= 0) continue;
                    int bps = bitsPerSample / 8;
                    if (payloadBytes < samplesPerFrame * channels * bps && payloadBytes % (channels * bps) != 0) continue;

                    var payload = new byte[payloadBytes];
                    Buffer.BlockCopy(buf, header, payload, 0, payloadBytes);

                    // Optional denoise when 16-bit PCM
                    if (ep.IsDenoiseEnabled && bitsPerSample == 16)
                    {
                        try { _denoiser.Denoise(payload, 0, payload.Length); } catch { }
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
            _denoiser.Dispose();
        }
    }
}
