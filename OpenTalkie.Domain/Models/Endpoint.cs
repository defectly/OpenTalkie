using OpenTalkie.Domain.Enums;
using OpenTalkie.Domain.VBAN;

namespace OpenTalkie.Domain.Models;

public sealed class Endpoint
{
    public Guid Id { get; set; }
    public EndpointType Type { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Hostname { get; set; } = string.Empty;
    public int Port { get; set; }
    public bool IsEnabled { get; set; }
    public bool IsDenoiseEnabled { get; set; }
    public bool AllowMobileData { get; set; }
    public float Volume { get; set; } = 1f;
    public VBanQuality Quality { get; set; } = VBanQuality.VBAN_QUALITY_FAST;

    public Endpoint()
    {
    }

    public Endpoint(EndpointType type, string name, string hostname, int port, bool denoise, bool allowMobileData)
    {
        Id = Guid.NewGuid();
        Type = type;
        Name = name;
        Hostname = hostname;
        Port = port;
        IsDenoiseEnabled = denoise;
        AllowMobileData = allowMobileData;
    }
}
