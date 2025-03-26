using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace OpenTalkie.ViewModel;

public partial class HomeViewModel : ObservableObject
{
    [ObservableProperty]
    private string broadcastButtonText;
    public BroadcastService BroadcastService { get; set; }

    public HomeViewModel(BroadcastService broadcastService)
    {
        BroadcastService = broadcastService;
        BroadcastButtonText = "Start service";
    }

    [RelayCommand]
    private async Task SwitchBroadcast()
    {
        bool isPermissionGranted = await CheckMicrophonePermissionAsync();

        if (!isPermissionGranted)
            return;

        BroadcastService.Switch();

        if (BroadcastService.BroadcastState)
            BroadcastButtonText = "Stop service";
        else
            BroadcastButtonText = "Start service";
    }

    private static async Task<bool> CheckMicrophonePermissionAsync()
    {
        var permissionStatus = await Permissions.CheckStatusAsync<Permissions.Microphone>();
        if (permissionStatus == PermissionStatus.Granted)
            return true;

        permissionStatus = await Permissions.RequestAsync<Permissions.Microphone>();

        if (permissionStatus == PermissionStatus.Granted)
            return true;

        _ = Application.Current.MainPage
            .DisplayAlert("Mic permission", "Please, give mic permission to let this app work", "Ok")
            .ConfigureAwait(false);

        return false;
    }
}
