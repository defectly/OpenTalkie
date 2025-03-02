﻿using OpenTalkie.Repositories;
using System.Net;
using System.Net.Sockets;

namespace OpenTalkie;

public partial class MainPage : ContentPage
{
    private readonly BroadcastService _broadcastService;
    private readonly EndpointRepository _endpointRepository;
    private readonly IMicrophoneService _microphoneService;
    private readonly IMicrophoneRepository _parameterRepository;

    public MainPage(BroadcastService broadcastService, EndpointRepository endpointRepository,
        IMicrophoneService microphoneService, IMicrophoneRepository parameterRepository)
    {
        InitializeComponent();

        _broadcastService = broadcastService;
        _endpointRepository = endpointRepository;
        _microphoneService = microphoneService;
        _parameterRepository = parameterRepository;

        LoadPreferences();
        RegisterUserInputEvents();

        GetAndroidIPAddress();
        Connectivity.Current.ConnectivityChanged += GetAndroidIPAddress;

    }

    public void SwitchBroadcaster()
    {
        _broadcastService.Switch();
    }

    public void AddEndpoint(Endpoint endpoint)
    {
        _broadcastService.Endpoints.Add(endpoint);
    }

    private void RegisterUserInputEvents()
    {
        streamName.TextChanged += DataChanged;
        bufferSize.TextChanged += DataChanged;
        SampleRate.SelectedIndexChanged += DataChanged;
        ChannelType.SelectedIndexChanged += DataChanged;
        microphone.SelectedIndexChanged += DataChanged;
        address.TextChanged += DataChanged;
        port.TextChanged += DataChanged;
    }

    private async Task SavePreferences()
    {
        Preferences.Set("MicrophoneBufferSize", int.Parse(bufferSize.Text));
        Preferences.Set("MicrophoneSampleRate", int.Parse(SampleRate.SelectedItem.ToString()));
        Preferences.Set("MicrophoneChannel", 1);
        Preferences.Set("MicrophoneSource", 0);
    }

    private void DataChanged(object sender, TextChangedEventArgs textChanged) => SavePreferences();
    private void DataChanged(object sender, EventArgs textChanged) => SavePreferences();

    private void LoadPreferences()
    {
        LoadEndpointPreferences();
    }

    private void LoadEndpointPreferences()
    {
        _endpointRepository.Add(new ("defectly", "192.168.1.100", 6980));

        streamName.Text = _endpointRepository.Endpoints[0].Name;
        bufferSize.Text = _microphoneService.BufferSize.ToString();
        address.Text = _endpointRepository.Endpoints[0].Hostname;
        port.Text = _endpointRepository.Endpoints[0].Port.ToString();
    }

    private void GetAndroidIPAddress()
    {
        try
        {
            using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, 0);
            socket.Connect("8.8.8.8", 65530);

            IPEndPoint endPoint = socket.LocalEndPoint as IPEndPoint;
            myIPAddress.Text = endPoint.Address.ToString();
        }
        catch { }
    }

    private async void GetAndroidIPAddress(object sender, ConnectivityChangedEventArgs e) =>
        GetAndroidIPAddress();

    private async Task<bool> CheckMicrophonePermissionAsync()
    {
        var permissionStatus = await Permissions.CheckStatusAsync<Permissions.Microphone>();
        if (permissionStatus == PermissionStatus.Granted)
            return true;

        permissionStatus = await Permissions.RequestAsync<Permissions.Microphone>();

        if (permissionStatus == PermissionStatus.Granted)
            return true;

        _ = DisplayAlert("Mic permission", "Please, give mic permission to let this app work", "Ok")
            .ConfigureAwait(false);

        return false;
    }

    private async void OnMicStreamButtonClicked(object sender, EventArgs e)
    {
        var button = (ToggleButton)sender;

        if (!_broadcastService.BroadcastState)
        {
            bool isPermissionGranted = await CheckMicrophonePermissionAsync();

            if (!isPermissionGranted)
                return;

            _endpointRepository.Endpoints.ForEach(e => e.StreamState = true);
            _broadcastService.Switch();
            button.Text = "stop";
        }
        else
        {
            _broadcastService.Switch();
            button.Text = "start";
        }
    }
}
