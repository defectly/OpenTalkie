﻿using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OpenTalkie.Common.Services;
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
    private ObservableCollection<string> networkAddresses;

    private readonly AppShell mainPage;
    public MicrophoneBroadcastService MicrophoneBroadcastService { get; set; }
    public PlaybackBroadcastService PlaybackBroadcastService { get; set; }

    public HomeViewModel(AppShell mainPage, MicrophoneBroadcastService microphoneBroadcastService,
        PlaybackBroadcastService playbackBroadcastService)
    {
        this.mainPage = mainPage;
        MicrophoneBroadcastService = microphoneBroadcastService;
        PlaybackBroadcastService = playbackBroadcastService;
        MicrophoneBroadcastButtonText = "Start microphone service";
        PlaybackBroadcastButtonText = "Start playback service";
        NetworkAddresses = new ObservableCollection<string>();
        LoadNetworkAddresses();
    }

    [RelayCommand]
    private async Task SwitchMicrophoneBroadcast()
    {
        bool isPermissionGranted = await CheckMicrophonePermissionAsync();

        if (!isPermissionGranted)
            return;

        MicrophoneBroadcastService.Switch();

        if (MicrophoneBroadcastService.BroadcastState)
            MicrophoneBroadcastButtonText = "Stop microphone service";
        else
            MicrophoneBroadcastButtonText = "Start microphone service";
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
            PlaybackBroadcastButtonText = "Stop playback service";
        else
            PlaybackBroadcastButtonText = "Start playback service";
    }

    [RelayCommand]
    private void RefreshNetworkAddresses()
    {
        LoadNetworkAddresses();
    }

    private async Task<bool> CheckMicrophonePermissionAsync()
    {
        var permissionStatus = await Permissions.CheckStatusAsync<Permissions.Microphone>();
        if (permissionStatus == PermissionStatus.Granted)
            return true;

        permissionStatus = await Permissions.RequestAsync<Permissions.Microphone>();

        if (permissionStatus == PermissionStatus.Granted)
            return true;

        _ = mainPage
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

            foreach (NetworkInterface ni in interfaces)
            {
                if (ni.OperationalStatus == OperationalStatus.Up)
                {
                    IPInterfaceProperties properties = ni.GetIPProperties();
                    foreach (UnicastIPAddressInformation ip in properties.UnicastAddresses)
                    {
                        if (ip.Address.AddressFamily == AddressFamily.InterNetwork) // IPv4
                        {
                            NetworkAddresses.Add($"{ni.Name}: {ip.Address}");
                        }
                    }
                }
            }

            if (NetworkAddresses.Count == 0)
            {
                NetworkAddresses.Add("Нет активных сетей");
            }
        }
        catch (Exception ex)
        {
            NetworkAddresses.Add($"Ошибка: {ex.Message}");
        }
    }
}