using CommunityToolkit.Mvvm.ComponentModel;
using OpenTalkie.Common.Enums;
using System.ComponentModel;
using System.Net.Sockets;

namespace OpenTalkie;

public partial class Endpoint : ObservableObject
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
    public Guid Id { get; set; }
    public uint FrameCount;

    public Endpoint(EndpointType type, string name, string hostname, int port, bool denoise)
    {
        Id = Guid.NewGuid();
        Type = type;
        Name = name.Length > 16 ? name.Substring(0, 16) : name;
        Hostname = hostname;
        Port = port;
        IsDenoiseEnabled = denoise;
    }

    public Endpoint()
    {
    }
}
