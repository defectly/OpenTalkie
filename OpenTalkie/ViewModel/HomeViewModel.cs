using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OpenTalkie.Common.Services;
using OpenTalkie.Platforms.Android.Common;
using System.Collections.ObjectModel;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace OpenTalkie.ViewModel;

public partial class HomeViewModel : ObservableObject
{
    [ObservableProperty]
    private string microphoneBroadcastButtonText;

    [ObservableProperty]
    private string playbackBroadcastButtonText;

    [ObservableProperty]
    private string receiverButtonText;

    [ObservableProperty]
    private ObservableCollection<string> networkAddresses;
    [ObservableProperty]
    private bool isPlaybackAvailable;

    // Indicates whether each service is currently active (for UI coloring)
    [ObservableProperty]
    private bool isMicrophoneActive;
    [ObservableProperty]
    private bool isPlaybackActive;
    [ObservableProperty]
    private bool isReceiverActive;

    public MicrophoneBroadcastService MicrophoneBroadcastService { get; set; }
    public PlaybackBroadcastService PlaybackBroadcastService { get; set; }
    public ReceiverService ReceiverService { get; set; }

    public HomeViewModel(MicrophoneBroadcastService microphoneBroadcastService,
        PlaybackBroadcastService playbackBroadcastService,
        ReceiverService receiverService)
    {
        if (OperatingSystem.IsAndroidVersionAtLeast(29))
            IsPlaybackAvailable = true;
        else
            IsPlaybackAvailable = false;

        MicrophoneBroadcastService = microphoneBroadcastService;
        PlaybackBroadcastService = playbackBroadcastService;
        ReceiverService = receiverService;
        OnMicrophoneServiceStateChange(microphoneBroadcastService.BroadcastState);
        OnPlaybackServiceStateChange(playbackBroadcastService.BroadcastState);
        OnReceiverStateChange(receiverService.ListeningState);
        NetworkAddresses = [];
        LoadNetworkAddresses();

        microphoneBroadcastService.BroadcastStateChanged += OnMicrophoneServiceStateChange;
        playbackBroadcastService.BroadcastStateChanged += OnPlaybackServiceStateChange;
        receiverService.ListeningStateChanged += OnReceiverStateChange;
    }

    private void OnMicrophoneServiceStateChange(bool isActive)
    {
        IsMicrophoneActive = isActive;
        if (isActive)
            MicrophoneBroadcastButtonText = "stop microphone service";
        else
            MicrophoneBroadcastButtonText = "start microphone service";
    }

    private void OnPlaybackServiceStateChange(bool isActive)
    {
        IsPlaybackActive = isActive;
        if (isActive)
            PlaybackBroadcastButtonText = "stop cast service";
        else
            PlaybackBroadcastButtonText = "start cast service";
    }

    private void OnReceiverStateChange(bool isActive)
    {
        IsReceiverActive = isActive;
        ReceiverButtonText = isActive ? "stop receiver service" : "start receiver service";
    }

    [RelayCommand]
    private async Task SwitchMicrophoneBroadcast() => await MicrophoneBroadcastService.Switch();

    [RelayCommand]
    private async Task SwitchPlaybackBroadcast()
    {
        if (!PlaybackBroadcastService.BroadcastState)
        {
            // Only request the required cast permission; microphone permission is not needed here.
            if (!await PlaybackBroadcastService.RequestPermissionAsync())
                return;
        }

        PlaybackBroadcastService.Switch();
    }

    [RelayCommand]
    private void RefreshNetworkAddresses()
    {
        LoadNetworkAddresses();
    }

    [RelayCommand]
    private void SwitchReceiver() => ReceiverService.Switch();

    private async Task<bool> CheckMicrophonePermissionAsync()
    {
        // Just request and return the permission status; do not show UI alert.
        bool permissionStatus = await PermissionManager.RequestMicrophonePermissionAsync();
        return permissionStatus;
    }

    private void LoadNetworkAddresses()
    {
        NetworkAddresses.Clear();

        try
        {
            NetworkInterface[] interfaces = NetworkInterface.GetAllNetworkInterfaces();

            for (int i = 0; i < interfaces.Length; i++)
            {
                var networkInterface = interfaces[i];
                if (networkInterface.OperationalStatus == OperationalStatus.Up)
                {
                    IPInterfaceProperties properties = networkInterface.GetIPProperties();
                    var unicastAddresses = properties.UnicastAddresses.ToList();
                    for (int j = 0; j < unicastAddresses.Count; j++)
                    {
                        var unicastAddress = unicastAddresses[j];
                        if (unicastAddress.Address.AddressFamily == AddressFamily.InterNetwork) // IPv4
                        {
                            NetworkAddresses.Add($"{networkInterface.Name}: {unicastAddress.Address}");
                        }
                    }
                }
            }

            if (NetworkAddresses.Count == 0)
            {
                NetworkAddresses.Add("No networks available");
            }
        }
        catch (Exception ex)
        {
            NetworkAddresses.Add($"Error: {ex.Message}");
        }
    }
}
