using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OpenTalkie.Common.Enums;
using OpenTalkie.Common.Services;
using System.Collections.ObjectModel;

namespace OpenTalkie.ViewModel;

public partial class PlaybackStreamsViewModel(PlaybackBroadcastService broadcastService) : ObservableObject, IQueryAttributable
{
    public ObservableCollection<Endpoint> Endpoints => broadcastService.Endpoints;

    // Receives the "NewEndpoint" parameter from AddStreamPage navigation result
    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("NewEndpoint", out var newEndpointObj) && newEndpointObj is Endpoint newEndpoint)
        {
            // Ensure not to add duplicates if navigated back multiple times, etc.
            if (newEndpoint != null && !Endpoints.Any(e => e.Id == newEndpoint.Id))
            {
                Endpoints.Add(newEndpoint);
            }
        }
    }

    [RelayCommand]
    private static async Task OpenSettings(Endpoint endpoint)
    {
        await Shell.Current
            .GoToAsync("StreamSettingsPage", new Dictionary<string, object> { { "Endpoint", endpoint } });
    }

    [RelayCommand]
    private void DeleteStream(Endpoint endpoint)
    {
        Endpoints.Remove(endpoint); // Assuming service handles persistence on collection change
    }

    [RelayCommand]
    private async Task AddStream()
    {
        var navigationParameters = new Dictionary<string, object>
        {
            { "StreamType", EndpointType.Playback } // Pass stream type to AddStreamPage
        };
        await Shell.Current.GoToAsync("AddStreamPage", navigationParameters);
    }
}
