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

    private readonly AppShell _mainPage;
    public MicrophoneBroadcastService MicrophoneBroadcastService { get; set; }
    public PlaybackBroadcastService PlaybackBroadcastService { get; set; }
    public ReceiverService ReceiverService { get; set; }

    public HomeViewModel(AppShell mainPage, MicrophoneBroadcastService microphoneBroadcastService,
        PlaybackBroadcastService playbackBroadcastService,
        ReceiverService receiverService)
    {
        if (OperatingSystem.IsAndroidVersionAtLeast(29))
            IsPlaybackAvailable = true;
        else
            IsPlaybackAvailable = false;

        _mainPage = mainPage;
        MicrophoneBroadcastService = microphoneBroadcastService;
        PlaybackBroadcastService = playbackBroadcastService;
        ReceiverService = receiverService;
        OnMicrophoneServiceStateChange(microphoneBroadcastService.BroadcastState);
        PlaybackBroadcastButtonText = "Start playback service";
        ReceiverButtonText = "Start Receiver";
        NetworkAddresses = [];
        LoadNetworkAddresses();

        microphoneBroadcastService.BroadcastStateChanged += OnMicrophoneServiceStateChange;
        playbackBroadcastService.BroadcastStateChanged += OnPlaybackServiceStateChange;
        receiverService.ListeningStateChanged += OnReceiverStateChange;
    }

    private void OnMicrophoneServiceStateChange(bool isActive)
    {
        if (isActive)
            MicrophoneBroadcastButtonText = "Stop microphone service";
        else
            MicrophoneBroadcastButtonText = "Start microphone service";
    }

    private void OnPlaybackServiceStateChange(bool isActive)
    {
        if (isActive)
            PlaybackBroadcastButtonText = "Stop playback service";
        else
            PlaybackBroadcastButtonText = "Start playback service";
    }

    private void OnReceiverStateChange(bool isActive)
    {
        ReceiverButtonText = isActive ? "Stop Receiver" : "Start Receiver";
    }

    [RelayCommand]
    private async Task SwitchMicrophoneBroadcast() => await MicrophoneBroadcastService.Switch();

    [RelayCommand]
    private async Task SwitchPlaybackBroadcast()
    {
        if (!PlaybackBroadcastService.BroadcastState)
        {
            bool isPermissionGranted = await CheckMicrophonePermissionAsync();

            if (!isPermissionGranted)
                return;

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
        bool permissionStatus = await PermissionManager.RequestMicrophonePermissionAsync();

        if (permissionStatus)
            return true;

        _ = _mainPage
            .DisplayAlert("Mic permission", "Please, give mic permission to let this app work", "Ok")
            .ConfigureAwait(false);

        return false;
    }

    private void LoadNetworkAddresses()
    {
        NetworkAddresses.Clear();

        try
        {
            NetworkInterface[] interfaces = NetworkInterface.GetAllNetworkInterfaces();

            for (int i = 0; i < interfaces.Length; i++)
            {
                var ni = interfaces[i];
                if (ni.OperationalStatus == OperationalStatus.Up)
                {
                    IPInterfaceProperties properties = ni.GetIPProperties();
                    var unicast = properties.UnicastAddresses.ToList();
                    for (int j = 0; j < unicast.Count; j++)
                    {
                        var ip = unicast[j];
                        if (ip.Address.AddressFamily == AddressFamily.InterNetwork) // IPv4
                        {
                            NetworkAddresses.Add($"{ni.Name}: {ip.Address}");
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
