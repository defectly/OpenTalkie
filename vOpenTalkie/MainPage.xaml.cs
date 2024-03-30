#if ANDROID
using Android.Media;
using Microsoft.Maui.Controls.PlatformConfiguration;
using NAudio.Wave;
using System.Net;
using System.Net.Sockets;

namespace vOpenTalkie;

public partial class MainPage : ContentPage
{
    AudioRecord audioRecord;

    StreamManager streamManager;

    ForegroundBatteryService batteryService = new();

    bool dataChanged = false;

    public string isStreaming = "Start stream";

    public MainPage()
    {
        InitializeComponent();

        CreateSampleRateList();
        CreateChannelTypeList();
        CreateMicInputsList();
        
        LoadData();
        RegisterUserInputEvents();

        GetAndroidIPAddress();
        Connectivity.Current.ConnectivityChanged += GetAndroidIPAddress;

        denoise.Toggled += OnDenoiseToggle;
    }

    private void OnDenoiseToggle(object? sender, ToggledEventArgs e)
    {
        if (streamManager == null)
            return;

        if (e.Value)
        {
            StartStreamButtonClicked();
            StartStreamButtonClicked();
        }
        else
        {
            StartStreamButtonClicked();
            StartStreamButtonClicked();
        }

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

    private async Task DataChanged()
    {
        dataChanged = true;

        Preferences.Set("StreamName", streamName.Text);
        Preferences.Set("BufferSize", bufferSize.Text);
        Preferences.Set("SampleRate", SampleRate.SelectedItem.ToString());
        Preferences.Set("ChannelType", ChannelType.SelectedItem.ToString());
        Preferences.Set("MicType", microphone.SelectedItem.ToString());
        Preferences.Set("IPAddress", address.Text);
        Preferences.Set("Port", port.Text);
    }

    private void DataChanged(object sender, TextChangedEventArgs textChanged) => DataChanged();
    private void DataChanged(object sender, EventArgs textChanged) => DataChanged();

    private void LoadData()
    {
        streamName.Text = Preferences.Get("StreamName", "Stream1");
        bufferSize.Text = Preferences.Get("BufferSize", "1024");
        SampleRate.SelectedItem = SampleRate.Items.FirstOrDefault(item => item == Preferences.Get("SampleRate", "48000"));
        ChannelType.SelectedItem = ChannelType.Items.FirstOrDefault(item => item == Preferences.Get("ChannelType", "Default"));
        microphone.SelectedItem = microphone.Items.FirstOrDefault(item => item == Preferences.Get("MicType", "Default"));
        address.Text = Preferences.Get("IPAddress", "");
        port.Text = Preferences.Get("Port", "6980");
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

    private void CreateSampleRateList()
    {
        SampleRate.Items.Add("6000");
        SampleRate.Items.Add("8000");
        SampleRate.Items.Add("11025");
        SampleRate.Items.Add("12000");
        SampleRate.Items.Add("16000");
        SampleRate.Items.Add("22050");
        SampleRate.Items.Add("24000");
        SampleRate.Items.Add("32000");
        SampleRate.Items.Add("44100");
        SampleRate.Items.Add("48000");
        SampleRate.Items.Add("64000");
        SampleRate.Items.Add("88200");
        SampleRate.Items.Add("96000");
        SampleRate.Items.Add("128000");
        SampleRate.Items.Add("176400");
        SampleRate.Items.Add("192000");
        SampleRate.Items.Add("256000");
        SampleRate.Items.Add("352800");
        SampleRate.Items.Add("384000");
        SampleRate.Items.Add("512000");
        SampleRate.Items.Add("705600");

        SampleRate.SelectedItem = SampleRate.Items.FirstOrDefault(item => item == "48000");
    }

    private void CreateChannelTypeList()
    {
        foreach (var source in Enum.GetNames<ChannelIn>())
            ChannelType.Items.Add(source.ToString());

        ChannelType.SelectedItem = ChannelType.Items.FirstOrDefault(item => item == "Default");

    }

    private void CreateMicInputsList()
    {
        foreach (var source in Enum.GetNames<AudioSource>())
            microphone.Items.Add(source.ToString());

        microphone.SelectedItem = microphone.Items.FirstOrDefault(item => item == "Default");
    }

    private void OnCounterClicked(object sender, EventArgs e) =>
        StartStreamButtonClicked();

    private void StartStreamButtonClicked()
    {
        if (streamManager == null)
        {
            if (!CheckMicrophonePermission().Result)
                return;

            CreateStreamManager();

            if (streamManager == null)
                return;
        }

        if (streamManager.IsStreaming)
        {
            streamManager.StopStream();
            batteryService.Stop();

            StartStreamBtn.Text = "Start stream";
        }
        else
        {
            if (!CheckMicrophonePermission().Result)
                return;

            if (dataChanged)
            {
                CreateStreamManager();
                dataChanged = false;
            }

            TryStartStream(denoise.IsToggled);

            batteryService.Start();

            StartStreamBtn.Text = "Stop stream";
        }
    }

    private void CreateStreamManager()
    {
        try
        {
            CreateAudioRecord();
        }
        catch (Exception exception)
        when (exception is Java.Lang.IllegalArgumentException)
        {
            DisplayAlert("Error", exception.Message, "Ok");
            return;
        }

        WaveAudioRecord waveAudioRecord = new(audioRecord);

        streamManager = new StreamManager(waveAudioRecord, address.Text, int.Parse(port.Text), streamName.Text);
    }

    private void CreateAudioRecord()
    {
        audioRecord = new(Enum.Parse<AudioSource>(microphone.SelectedItem.ToString()),
        int.Parse(SampleRate.SelectedItem.ToString()),
        Enum.Parse<ChannelIn>(ChannelType.SelectedItem.ToString()),
        Encoding.Pcm16bit, int.Parse(bufferSize.Text));
    }

    private void TryStartStream(bool useDenoise)
    {
        try
        {
            streamManager.StartStream(useDenoise);
        }
        catch (Exception exception)
        when (exception is Java.Lang.IllegalArgumentException ||
        exception is Java.Lang.IllegalStateException ||
        exception is Java.Lang.IllegalArgumentException)
        {
            //"May be this format is unsupported"
            DisplayAlert("Error", exception.Message, "Ok");
        }
    }

    private async Task<bool> CheckMicrophonePermission()
    {
        if (await Permissions.CheckStatusAsync<Permissions.Microphone>() != PermissionStatus.Granted)
        {
            Permissions.RequestAsync<Permissions.Microphone>();
            DisplayAlert("Mic permission", "Please, give mic permission to let this app work", "Ok");

            return false;
        }

        return true;
    }
}
#endif