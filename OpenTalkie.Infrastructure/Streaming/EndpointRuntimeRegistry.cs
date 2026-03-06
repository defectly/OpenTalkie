using OpenTalkie.Domain.Rules;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Net.Sockets;

namespace OpenTalkie.Infrastructure.Streaming;

internal sealed class EndpointRuntimeRegistry : IDisposable
{
    private readonly ObservableCollection<Endpoint> _endpoints;
    private readonly Dictionary<Guid, EndpointRuntime> _runtimes = new();

    public EndpointRuntimeRegistry(ObservableCollection<Endpoint> endpoints)
    {
        _endpoints = endpoints ?? throw new ArgumentNullException(nameof(endpoints));

        _endpoints.CollectionChanged += OnEndpointsCollectionChanged;
        Connectivity.ConnectivityChanged += OnConnectivityChanged;

        for (var i = 0; i < _endpoints.Count; i++)
        {
            RegisterEndpoint(_endpoints[i]);
        }
    }

    public EndpointRuntime GetRuntime(Endpoint endpoint)
    {
        if (_runtimes.TryGetValue(endpoint.Id, out var runtime))
        {
            runtime.UpdateName(endpoint.Name);
            runtime.Reconnect(endpoint.Hostname, endpoint.Port);
            return runtime;
        }

        RegisterEndpoint(endpoint);
        return _runtimes[endpoint.Id];
    }

    public void Dispose()
    {
        _endpoints.CollectionChanged -= OnEndpointsCollectionChanged;
        Connectivity.ConnectivityChanged -= OnConnectivityChanged;

        var runtimes = _runtimes.Values.ToArray();
        for (var i = 0; i < runtimes.Length; i++)
        {
            runtimes[i].Dispose();
        }

        _runtimes.Clear();
    }

    private void RegisterEndpoint(Endpoint endpoint)
    {
        if (_runtimes.ContainsKey(endpoint.Id))
        {
            var existing = _runtimes[endpoint.Id];
            existing.UpdateName(endpoint.Name);
            existing.Reconnect(endpoint.Hostname, endpoint.Port);
            return;
        }

        var runtime = new EndpointRuntime();
        runtime.UpdateName(endpoint.Name);
        runtime.Reconnect(endpoint.Hostname, endpoint.Port);
        _runtimes[endpoint.Id] = runtime;
    }

    private void UnregisterEndpoint(Endpoint endpoint)
    {
        if (_runtimes.TryGetValue(endpoint.Id, out var runtime))
        {
            runtime.Dispose();
            _runtimes.Remove(endpoint.Id);
        }
    }

    private void OnEndpointsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.Action == NotifyCollectionChangedAction.Add && e.NewItems != null)
        {
            for (var i = 0; i < e.NewItems.Count; i++)
            {
                RegisterEndpoint((Endpoint)e.NewItems[i]!);
            }
        }

        if (e.Action == NotifyCollectionChangedAction.Remove && e.OldItems != null)
        {
            for (var i = 0; i < e.OldItems.Count; i++)
            {
                UnregisterEndpoint((Endpoint)e.OldItems[i]!);
            }
        }

        if (e.Action == NotifyCollectionChangedAction.Replace && e.OldItems != null && e.NewItems != null)
        {
            for (var i = 0; i < e.OldItems.Count; i++)
            {
                UnregisterEndpoint((Endpoint)e.OldItems[i]!);
            }

            for (var i = 0; i < e.NewItems.Count; i++)
            {
                RegisterEndpoint((Endpoint)e.NewItems[i]!);
            }
        }

        if (e.Action == NotifyCollectionChangedAction.Reset)
        {
            var endpointsById = _endpoints.ToDictionary(ep => ep.Id);
            var runtimeKeys = _runtimes.Keys.ToArray();
            for (var i = 0; i < runtimeKeys.Length; i++)
            {
                var id = runtimeKeys[i];
                if (!endpointsById.ContainsKey(id))
                {
                    _runtimes[id].Dispose();
                    _runtimes.Remove(id);
                }
            }

            for (var i = 0; i < _endpoints.Count; i++)
            {
                RegisterEndpoint(_endpoints[i]);
            }
        }
    }

    private void OnConnectivityChanged(object? sender, ConnectivityChangedEventArgs e)
    {
        if (e.NetworkAccess == NetworkAccess.None)
        {
            return;
        }

        for (var i = 0; i < _endpoints.Count; i++)
        {
            var endpoint = _endpoints[i];
            if (_runtimes.TryGetValue(endpoint.Id, out var runtime))
            {
                runtime.Reconnect(endpoint.Hostname, endpoint.Port);
            }
        }
    }
}

internal sealed class EndpointRuntime : IDisposable
{
    public UdpClient? Client { get; private set; }
    public uint FrameCount { get; set; }
    public byte[] NameBytes16 { get; } = new byte[VbanStreamName16.MaxBytes];
    private string? _hostname;
    private int _port;

    public void UpdateName(string? name)
    {
        VbanStreamName16.Fill(NameBytes16, name);
    }

    public void Reconnect(string? hostname, int port)
    {
        if (string.Equals(_hostname, hostname, StringComparison.OrdinalIgnoreCase) && _port == port && Client != null)
        {
            return;
        }

        Client?.Dispose();
        Client = null;
        _hostname = hostname;
        _port = port;

        if (string.IsNullOrWhiteSpace(hostname) || port <= 0 || port > 65535)
        {
            return;
        }

        try
        {
            Client = new UdpClient(hostname, port);
            ConfigureUdpClient(Client);
        }
        catch (SocketException ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to initialize endpoint UDP client for {hostname}:{port}: {ex.Message}");
            Client = null;
        }
    }

    public void Dispose()
    {
        Client?.Dispose();
        Client = null;
    }

    private static void ConfigureUdpClient(UdpClient client)
    {
        try
        {
            var sock = client.Client;
            sock.SendBufferSize = Math.Max(sock.SendBufferSize, 1 << 20);
            sock.ReceiveBufferSize = Math.Max(sock.ReceiveBufferSize, 1 << 20);
            sock.DontFragment = true;
            try { sock.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.TypeOfService, 0xB8); } catch { }
            try { sock.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true); } catch { }
        }
        catch
        {
        }
    }
}
