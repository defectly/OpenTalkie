using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OpenTalkie.Common.Services;
using OpenTalkie.Common.Services.Interfaces;

namespace OpenTalkie.ViewModel;

public partial class HomeViewModel : ObservableObject
{
    [ObservableProperty]
    private string microphoneBroadcastButtonText;
    [ObservableProperty]
    private string playbackBroadcastButtonText;
    public MicrophoneBroadcastService MicrophoneBroadcastService { get; set; }
    public PlaybackBroadcastService PlaybackBroadcastService { get; set; }

    public HomeViewModel(MicrophoneBroadcastService microphoneBroadcastService,
        PlaybackBroadcastService playbackBroadcastService)
    {
        MicrophoneBroadcastService = microphoneBroadcastService;
        PlaybackBroadcastService = playbackBroadcastService;
        microphoneBroadcastButtonText = "Start service";
        playbackBroadcastButtonText = "Start service";
    }

    [RelayCommand]
    private async Task SwitchMicrophoneBroadcast()
    {
        bool isPermissionGranted = await CheckMicrophonePermissionAsync();

        if (!isPermissionGranted)
            return;

        MicrophoneBroadcastService.Switch();

        if (MicrophoneBroadcastService.BroadcastState)
            MicrophoneBroadcastButtonText = "Stop service";
        else
            MicrophoneBroadcastButtonText = "Start service";
    }

    [RelayCommand]
    private async Task SwitchPlaybackBroadcast()
    {
        bool isPermissionGranted = await CheckMicrophonePermissionAsync();

        if (!isPermissionGranted)
            return;

        if (!await PlaybackBroadcastService.RequestPermissionAsync())
            return;

        PlaybackBroadcastService.Switch();

        if (PlaybackBroadcastService.BroadcastState)
            PlaybackBroadcastButtonText = "Stop service";
        else
            PlaybackBroadcastButtonText = "Start service";
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
