using CommunityToolkit.Mvvm.ComponentModel;
using OpenTalkie.Domain.Enums;
using OpenTalkie.Domain.Models;
using OpenTalkie.Domain.VBAN;

namespace OpenTalkie.Presentation.ViewModels;

public partial class StreamEndpointItemViewModel : ObservableObject
{
    public Guid EndpointId { get; private set; }
    public EndpointType Type { get; private set; }

    [ObservableProperty]
    public partial string Name { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string Hostname { get; set; } = string.Empty;

    [ObservableProperty]
    public partial int Port { get; set; }

    [ObservableProperty]
    public partial bool IsEnabled { get; set; }

    [ObservableProperty]
    public partial bool IsDenoiseEnabled { get; set; }

    [ObservableProperty]
    public partial bool AllowMobileData { get; set; }

    [ObservableProperty]
    public partial float Volume { get; set; }

    [ObservableProperty]
    public partial VBanQuality Quality { get; set; }

    public static StreamEndpointItemViewModel FromEndpoint(Endpoint endpoint)
    {
        var item = new StreamEndpointItemViewModel();
        item.Apply(endpoint);
        return item;
    }

    public void Apply(Endpoint endpoint)
    {
        EndpointId = endpoint.Id;
        Type = endpoint.Type;
        Name = endpoint.Name;
        Hostname = endpoint.Hostname;
        Port = endpoint.Port;
        IsEnabled = endpoint.IsEnabled;
        IsDenoiseEnabled = endpoint.IsDenoiseEnabled;
        AllowMobileData = endpoint.AllowMobileData;
        Volume = endpoint.Volume;
        Quality = endpoint.Quality;
    }
}
