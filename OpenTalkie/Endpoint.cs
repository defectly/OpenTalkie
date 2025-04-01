using CommunityToolkit.Mvvm.ComponentModel;
using OpenTalkie.Common.Enums;
using System.ComponentModel;
using System.Net.Sockets;
using Microsoft.Maui.Networking;

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
        UdpClient = new(Hostname, Port);
        this.PropertyChanged += DestinationChanged;
        Connectivity.ConnectivityChanged += OnConnectivityChanged;
        IsDenoiseEnabled = denoise;
    }

    private void OnConnectivityChanged(object? sender, ConnectivityChangedEventArgs e)
    {
        if (e.NetworkAccess != NetworkAccess.None)
        {
            UdpClient?.Dispose();
            UdpClient = new(Hostname, Port);
        }
    }

    public Endpoint(Guid id, EndpointType type, string name, string hostname, int port)
    {
        Id = id;
        Type = type;
        Name = name.Length > 16 ? name.Substring(0, 16) : name;
        Hostname = hostname;
        Port = port;
        UdpClient = new(Hostname, Port);
        this.PropertyChanged += DestinationChanged;
    }

    public void DestinationChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != "Hostname" && e.PropertyName != "Port")
            return;

        UdpClient?.Dispose();
        UdpClient = new(Hostname, Port);
    }

    public void Dispose()
    {
        UdpClient.Dispose();

        GC.SuppressFinalize(this);
    }
}
