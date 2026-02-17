using CommunityToolkit.Mvvm.ComponentModel;
using OpenTalkie.Common.Enums;
using OpenTalkie.VBAN;
using System.ComponentModel;
using System.Net.Sockets;

namespace OpenTalkie;

public partial class Endpoint : ObservableObject, IDisposable
{
    [ObservableProperty]
    private EndpointType type;
    [ObservableProperty]
    private string name;
    [ObservableProperty]
    private string hostname;
    [ObservableProperty]
    private int port;
    [ObservableProperty]
    private bool isEnabled;
    [ObservableProperty]
    private bool isDenoiseEnabled;
    [ObservableProperty]
    private bool allowMobileData;
    [ObservableProperty]
    private float volume = 1f;
    [ObservableProperty]
    private VBanQuality quality = VBanQuality.VBAN_QUALITY_FAST;
    public UdpClient UdpClient { get; private set; }
    public Guid Id { get; set; }
    public uint FrameCount;
    internal readonly byte[] NameBytes16 = new byte[16];

    public Endpoint(EndpointType type, string name, string hostname, int port, bool denoise, bool allowMobileData)
    {
        Id = Guid.NewGuid();
        Type = type;
        Name = name.Length > 16 ? name.Substring(0, 16) : name;
        Hostname = hostname;
        Port = port;
        try
        {
            UdpClient = new(Hostname, Port);
            ConfigureUdpClient(UdpClient);
        }
        catch (SocketException ex)
        {
            System.Diagnostics.Debug.WriteLine($"Endpoint Constructor: Failed to resolve hostname '{Hostname}' or create UDP client. UdpClient will be null. Error: {ex.Message}"); // Keep debug for now
            UdpClient = null;
        }
        this.PropertyChanged += DestinationChanged;
        Connectivity.ConnectivityChanged += OnConnectivityChanged;
        IsDenoiseEnabled = denoise;
        AllowMobileData = allowMobileData;
        Quality = VBanQuality.VBAN_QUALITY_FAST;
        UpdateNameBytes();
    }

    public Endpoint()
    {
        this.PropertyChanged += DestinationChanged;
        Connectivity.ConnectivityChanged += OnConnectivityChanged;
        Quality = VBanQuality.VBAN_QUALITY_FAST;
        UpdateNameBytes();
    }

    private void OnConnectivityChanged(object? sender, ConnectivityChangedEventArgs e)
    {
        if (e.NetworkAccess != NetworkAccess.None) // typically means connection is back
        {
            UdpClient?.Dispose();
            try
            {
                UdpClient = new(Hostname, Port);
                ConfigureUdpClient(UdpClient);
            }
            catch (SocketException ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error re-initializing UdpClient on connectivity change: {ex.Message} for {Hostname}:{Port}"); // Keep debug for now
                UdpClient = null;
            }
        }
    }

    public void DestinationChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != "Hostname" && e.PropertyName != "Port")
            return;

        // Basic check before attempting UdpClient creation
        if (string.IsNullOrWhiteSpace(Hostname) || Port <= 0 || Port > 65535)
        {
            UdpClient?.Dispose();
            UdpClient = null;
            return;
        }

        UdpClient?.Dispose();
        try
        {
            UdpClient = new(Hostname, Port);
            ConfigureUdpClient(UdpClient);
        }
        catch (SocketException ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error re-initializing UdpClient on destination change: {ex.Message} for {Hostname}:{Port}"); // Keep debug for now
            UdpClient = null;
        }
    }

    public void Dispose()
    {
        UdpClient.Dispose();

        GC.SuppressFinalize(this);
    }

    private static void ConfigureUdpClient(UdpClient client)
    {
        try
        {
            var sock = client.Client;
            // Larger buffers to avoid drops under burst
            sock.SendBufferSize = Math.Max(sock.SendBufferSize, 1 << 20);
            sock.ReceiveBufferSize = Math.Max(sock.ReceiveBufferSize, 1 << 20);
            // Prefer not fragmenting VBAN packets (IPv4)
            sock.DontFragment = true;
            // Hint QoS: Expedited Forwarding (DSCP 46 -> TOS 0xB8)
            try { sock.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.TypeOfService, 0xB8); } catch { }
            // Allow quick rebinding if needed
            try { sock.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true); } catch { }
        }
        catch { }
    }

    private void UpdateNameBytes()
    {
        var span = NameBytes16.AsSpan();
        span.Clear();
        var n = Name ?? string.Empty;
        int len = Math.Min(16, n.Length);
        for (int i = 0; i < len; i++) span[i] = (byte)n[i];
    }

    partial void OnNameChanged(string value) => UpdateNameBytes();
}
