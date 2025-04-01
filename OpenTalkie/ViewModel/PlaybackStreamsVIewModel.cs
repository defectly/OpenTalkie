using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OpenTalkie.Common.Enums;
using OpenTalkie.Common.Services;
using System.Collections.ObjectModel;

namespace OpenTalkie.ViewModel;

public partial class PlaybackStreamsViewModel(PlaybackBroadcastService broadcastService) : ObservableObject
{
    public ObservableCollection<Endpoint> Endpoints => broadcastService.Endpoints;

    [RelayCommand]
    private static async Task OpenSettings(Endpoint endpoint)
    {
        await Shell.Current
            .GoToAsync("StreamSettingsPage", new Dictionary<string, object> { { "Endpoint", endpoint } });
    }

    [RelayCommand]
    private void DeleteStream(Endpoint endpoint)
    {
        Endpoints.Remove(endpoint);
    }

    [RelayCommand]
    private void AddStream()
    {
        var newEndpoint = new Endpoint(EndpointType.Playback, "Stream2", "192.168.1.1", 6980, false);
        Endpoints.Add(newEndpoint);
    }
}
