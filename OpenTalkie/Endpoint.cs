using CommunityToolkit.Mvvm.ComponentModel;
using System.ComponentModel;
using System.Net.Sockets;

namespace OpenTalkie;

public partial class Endpoint : ObservableObject, IDisposable
{
    public Guid Id { get; set; }
    [ObservableProperty]
    private string name;
    [ObservableProperty]
    private string hostname;
    [ObservableProperty]
    private int port;
    [ObservableProperty]
    private bool isEnabled;
    public UdpClient UdpClient { get; private set; }

    public Endpoint(string name, string hostname, int port)
    {
        Id = Guid.NewGuid();
        Name = name.Length > 16 ? name.Substring(0, 16) : name;
        Hostname = hostname;
        Port = port;
        UdpClient = new(Hostname, Port);
        this.PropertyChanged += DestinationChanged;
    }   

    public Endpoint(Guid id, string name, string hostname, int port)
    {
        Id = id;
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

        UdpClient = new(Hostname, Port);
    }

    public void Dispose()
    {
        UdpClient.Dispose();

        GC.SuppressFinalize(this);
    }
}
