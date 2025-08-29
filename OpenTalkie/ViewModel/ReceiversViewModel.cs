using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OpenTalkie.Common.Enums;
using OpenTalkie.Common.Services;
using System.Collections.ObjectModel;

namespace OpenTalkie.ViewModel;

public partial class ReceiversViewModel(ReceiverService receiverService) : ObservableObject, IQueryAttributable
{
    public ObservableCollection<Endpoint> Endpoints => receiverService.Endpoints;

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("NewEndpoint", out var newEndpointObj) && newEndpointObj is Endpoint newEndpoint)
        {
            if (newEndpoint != null && !Endpoints.Any(e => e.Id == newEndpoint.Id))
                Endpoints.Add(newEndpoint);
        }
    }

    [RelayCommand]
    private static async Task OpenSettings(Endpoint endpoint)
    {
        await Shell.Current.GoToAsync("StreamSettingsPage", new Dictionary<string, object> { { "Endpoint", endpoint } });
    }

    [RelayCommand]
    private void DeleteStream(Endpoint endpoint)
    {
        Endpoints.Remove(endpoint);
    }

    [RelayCommand]
    private async Task AddStream()
    {
        var navigationParameters = new Dictionary<string, object>
        {
            { "StreamType", EndpointType.Receiver }
        };
        await Shell.Current.GoToAsync("AddStreamPage", navigationParameters);
    }
}

