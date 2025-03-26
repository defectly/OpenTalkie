using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OpenTalkie.Common.Services;
using System.Collections.ObjectModel;

namespace OpenTalkie.ViewModel;

public partial class StreamsViewModel : ObservableObject
{
    private MicrophoneBroadcastService _broadcastService;
    public ObservableCollection<Endpoint> Endpoints => _broadcastService.Endpoints;

    public StreamsViewModel(MicrophoneBroadcastService broadcastService)
    {
        _broadcastService = broadcastService;
    }

    [RelayCommand]
    private async Task OpenSettings(Endpoint endpoint)
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
        var newEndpoint = new Endpoint("New Stream", "192.168.1.1", 1234);
        Endpoints.Add(newEndpoint);
    }
}
