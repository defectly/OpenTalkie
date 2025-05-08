using CommunityToolkit.Mvvm.ComponentModel;
using OpenTalkie.Common.Enums;
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
    public UdpClient UdpClient { get; private set; }
    public Guid Id { get; set; }
    public uint FrameCount;

    public Endpoint(EndpointType type, string name, string hostname, int port, bool denoise)
    {
        Id = Guid.NewGuid();
        Type = type;
        Name = name.Length > 16 ? name.Substring(0, 16) : name;
        Hostname = hostname;
        Port = port;
        try
        {
            UdpClient = new(Hostname, Port);
        }
        catch (SocketException ex)
        {
            System.Diagnostics.Debug.WriteLine($"Endpoint Constructor: Failed to resolve hostname '{Hostname}' or create UDP client. UdpClient will be null. Error: {ex.Message}"); // Keep debug for now
            UdpClient = null;
        }
        this.PropertyChanged += DestinationChanged;
        Connectivity.ConnectivityChanged += OnConnectivityChanged;
        IsDenoiseEnabled = denoise;
    }

    public Endpoint()
    {
        this.PropertyChanged += DestinationChanged;
        Connectivity.ConnectivityChanged += OnConnectivityChanged;
    }

    private void OnConnectivityChanged(object? sender, ConnectivityChangedEventArgs e)
    {
        if (e.NetworkAccess != NetworkAccess.None) // typically means connection is back
        {
            UdpClient?.Dispose();
            try
            {
                UdpClient = new(Hostname, Port);
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
}
