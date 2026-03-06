using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Mediator;
using Microsoft.Maui.ApplicationModel;
using OpenTalkie.Application.Abstractions.Services;
using OpenTalkie.Application.Home.Commands;
using OpenTalkie.Application.Streams;
using OpenTalkie.Domain.Models;
using OpenTalkie.Presentation.Abstractions.Services;
using System.Collections.ObjectModel;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace OpenTalkie.Presentation.ViewModels;

public partial class HomeViewModel : ObservableObject
{
    [ObservableProperty]
    public partial string MicrophoneBroadcastButtonText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string PlaybackBroadcastButtonText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string ReceiverButtonText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial ObservableCollection<string> NetworkAddresses { get; set; } = [];

    [ObservableProperty]
    public partial bool IsPlaybackAvailable { get; set; }

    [ObservableProperty]
    public partial bool IsMicrophoneActive { get; set; }

    [ObservableProperty]
    public partial bool IsPlaybackActive { get; set; }

    [ObservableProperty]
    public partial bool IsReceiverActive { get; set; }

    private readonly IMediator _mediator;
    private readonly IMicrophoneBroadcastService _microphoneBroadcastService;
    private readonly IPlaybackBroadcastService _playbackBroadcastService;
    private readonly IReceiverService _receiverService;
    private readonly IUserDialogService _dialogService;

    public HomeViewModel(
        IMediator mediator,
        IMicrophoneBroadcastService microphoneBroadcastService,
        IPlaybackBroadcastService playbackBroadcastService,
        IReceiverService receiverService,
        IPlatformCapabilitiesService platformCapabilitiesService,
        IUserDialogService dialogService)
    {
        _mediator = mediator;
        _microphoneBroadcastService = microphoneBroadcastService;
        _playbackBroadcastService = playbackBroadcastService;
        _receiverService = receiverService;
        _dialogService = dialogService;

        IsPlaybackAvailable = platformCapabilitiesService.GetCapabilities().IsPlaybackCaptureSupported;

        OnMicrophoneServiceStateChange(_microphoneBroadcastService.Status);
        OnPlaybackServiceStateChange(_playbackBroadcastService.Status);
        OnReceiverStateChange(_receiverService.Status);

        LoadNetworkAddresses();

        _microphoneBroadcastService.StatusChanged += OnMicrophoneServiceStateChange;
        _playbackBroadcastService.StatusChanged += OnPlaybackServiceStateChange;
        _receiverService.StatusChanged += OnReceiverStateChange;
    }

    private void OnMicrophoneServiceStateChange(StreamSessionStatus status)
    {
        ApplyOnMainThread(() =>
        {
            IsMicrophoneActive = status.Phase == StreamSessionPhase.Running;
            MicrophoneBroadcastButtonText = status.Phase switch
            {
                StreamSessionPhase.Starting => "starting microphone service",
                StreamSessionPhase.Stopping => "stopping microphone service",
                StreamSessionPhase.Running => "stop microphone service",
                _ => "start microphone service"
            };
        });
    }

    private void OnPlaybackServiceStateChange(StreamSessionStatus status)
    {
        ApplyOnMainThread(() =>
        {
            IsPlaybackActive = status.Phase == StreamSessionPhase.Running;
            PlaybackBroadcastButtonText = status.Phase switch
            {
                StreamSessionPhase.Starting => "starting cast service",
                StreamSessionPhase.Stopping => "stopping cast service",
                StreamSessionPhase.Running => "stop cast service",
                _ => "start cast service"
            };
        });
    }

    private void OnReceiverStateChange(StreamSessionStatus status)
    {
        ApplyOnMainThread(() =>
        {
            IsReceiverActive = status.Phase == StreamSessionPhase.Running;
            ReceiverButtonText = status.Phase switch
            {
                StreamSessionPhase.Starting => "starting receiver service",
                StreamSessionPhase.Stopping => "stopping receiver service",
                StreamSessionPhase.Running => "stop receiver service",
                _ => "start receiver service"
            };
        });
    }

    [RelayCommand]
    private async Task SwitchMicrophoneBroadcast()
    {
        var result = await _mediator.Send(new SwitchMicrophoneBroadcastCommand());
        await ShowErrorIfFailedAsync(result, _dialogService);
    }

    [RelayCommand]
    private async Task SwitchPlaybackBroadcast()
    {
        var result = await _mediator.Send(new SwitchPlaybackBroadcastCommand());
        await ShowErrorIfFailedAsync(result, _dialogService);
    }

    [RelayCommand]
    private void RefreshNetworkAddresses()
    {
        LoadNetworkAddresses();
    }

    [RelayCommand]
    private async Task SwitchReceiver()
    {
        var result = await _mediator.Send(new SwitchReceiverCommand());
        await ShowErrorIfFailedAsync(result, _dialogService);
    }

    private static async Task ShowErrorIfFailedAsync(OperationResult result, IUserDialogService dialogService)
    {
        if (result.IsSuccess || string.IsNullOrWhiteSpace(result.ErrorMessage))
        {
            return;
        }

        await dialogService.ShowErrorAsync(result.ErrorMessage);
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
                        if (unicastAddress.Address.AddressFamily == AddressFamily.InterNetwork)
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

    private static void ApplyOnMainThread(Action action)
    {
        if (MainThread.IsMainThread)
        {
            action();
            return;
        }

        MainThread.BeginInvokeOnMainThread(action);
    }
}

