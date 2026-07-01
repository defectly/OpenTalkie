using OpenTalkie.Domain.Rules;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Net.Sockets;
using Microsoft.Extensions.Logging.Abstractions;

namespace OpenTalkie.Infrastructure.Streaming;

internal sealed class EndpointRuntimeRegistry : IDisposable
{
    private readonly ObservableCollection<Endpoint> _endpoints;
    private readonly Dictionary<Guid, EndpointRuntime> _runtimes = new();
    private readonly ILogger<EndpointRuntimeRegistry> _logger;
    private readonly ILoggerFactory _loggerFactory;

    public EndpointRuntimeRegistry(ObservableCollection<Endpoint> endpoints, ILoggerFactory? loggerFactory = null)
    {
        _loggerFactory = loggerFactory ?? NullLoggerFactory.Instance;
        _logger = _loggerFactory.CreateLogger<EndpointRuntimeRegistry>();
        _endpoints = endpoints ?? throw new ArgumentNullException(nameof(endpoints));

        _endpoints.CollectionChanged += OnEndpointsCollectionChanged;
        Connectivity.ConnectivityChanged += OnConnectivityChanged;

        for (var i = 0; i < _endpoints.Count; i++)
            RegisterEndpoint(_endpoints[i]);

        if (_logger.IsEnabled(LogLevel.Debug))
            _logger.LogDebug("Endpoint runtime registry initialized with {EndpointCount} endpoint(s).", _endpoints.Count);
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
            runtimes[i].Dispose();

        _runtimes.Clear();
        _logger.LogDebug("Endpoint runtime registry disposed.");
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

        var runtime = new EndpointRuntime(_loggerFactory.CreateLogger<EndpointRuntime>());
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
                RegisterEndpoint((Endpoint)e.NewItems[i]!);
        }

        if (e.Action == NotifyCollectionChangedAction.Remove && e.OldItems != null)
        {
            for (var i = 0; i < e.OldItems.Count; i++)
                UnregisterEndpoint((Endpoint)e.OldItems[i]!);
        }

        if (e.Action == NotifyCollectionChangedAction.Replace && e.OldItems != null && e.NewItems != null)
        {
            for (var i = 0; i < e.OldItems.Count; i++)
                UnregisterEndpoint((Endpoint)e.OldItems[i]!);

            for (var i = 0; i < e.NewItems.Count; i++)
                RegisterEndpoint((Endpoint)e.NewItems[i]!);
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
                RegisterEndpoint(_endpoints[i]);
        }
    }

    private void OnConnectivityChanged(object? sender, ConnectivityChangedEventArgs e)
    {
        if (e.NetworkAccess == NetworkAccess.None)
        {
            _logger.LogInformation("Network disconnected; sender runtimes will reconnect when network returns.");
            return;
        }

        if (_logger.IsEnabled(LogLevel.Information))
            _logger.LogInformation("Network changed to {NetworkAccess}; reconnecting sender runtimes.", e.NetworkAccess);

        for (var i = 0; i < _endpoints.Count; i++)
        {
            var endpoint = _endpoints[i];

            if (_runtimes.TryGetValue(endpoint.Id, out var runtime))
                runtime.Reconnect(endpoint.Hostname, endpoint.Port);
        }
    }
}

internal sealed class EndpointRuntime : IDisposable
{
    private readonly ILogger<EndpointRuntime> _logger;
    public UdpClient? Client { get; private set; }
    public uint FrameCount { get; set; }
    public byte[] NameBytes16 { get; } = new byte[VbanStreamName16.MaxBytes];
    private string? _hostname;
    private int _port;

    public EndpointRuntime(ILogger<EndpointRuntime> logger)
    {
        _logger = logger;
    }

    public void UpdateName(string? name)
    {
        VbanStreamName16.Fill(NameBytes16, name);
    }

    public void Reconnect(string? hostname, int port)
    {
        if (string.Equals(_hostname, hostname, StringComparison.OrdinalIgnoreCase) && _port == port && Client != null)
            return;

        Client?.Dispose();
        Client = null;
        _hostname = hostname;
        _port = port;

        if (string.IsNullOrWhiteSpace(hostname) || port <= 0 || port > 65535)
        {
            if (_logger.IsEnabled(LogLevel.Debug))
                _logger.LogDebug("Endpoint runtime disconnected because target {Host}:{Port} is incomplete or invalid.", hostname, port);

            return;
        }

        try
        {
            Client = new UdpClient(hostname, port);
            ConfigureUdpClient(Client, _logger);

            if (_logger.IsEnabled(LogLevel.Information))
                _logger.LogInformation("Endpoint UDP client connected to {Host}:{Port}.", hostname, port);
        }
        catch (SocketException ex)
        {
            _logger.LogError(ex, "Failed to initialize endpoint UDP client for {Host}:{Port}.", hostname, port);
            Client = null;
        }
    }

    public void Dispose()
    {
        Client?.Dispose();
        Client = null;
        _logger.LogDebug("Endpoint runtime disposed.");
    }

    private static void ConfigureUdpClient(UdpClient client, ILogger logger)
    {
        try
        {
            var sock = client.Client;
            sock.SendBufferSize = Math.Max(sock.SendBufferSize, 1 << 20);
            sock.ReceiveBufferSize = Math.Max(sock.ReceiveBufferSize, 1 << 20);
            sock.DontFragment = true;
            try { sock.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.TypeOfService, 0xB8); } catch (Exception ex) { logger.LogDebug(ex, "Could not set UDP type-of-service option."); }
            try { sock.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true); } catch (Exception ex) { logger.LogDebug(ex, "Could not set UDP reuse-address option."); }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to configure endpoint UDP client socket.");
        }
    }
}
