#if ANDROID
using Android.Media;
using OpenTalkie.Platforms.Android;
using System.Net;
using System.Net.Sockets;
using Encoding = Android.Media.Encoding;

namespace OpenTalkie;

public partial class MainPage : ContentPage
{
    AudioRecord audioRecord;

    StreamManager streamManager;
    StreamManager sysStreamManager;

    ForegroundMicrophoneService microphoneService = new();
    ForegroundMediaProjectionService mediaProjectionService = new();

    SystemAudioCapture systemAudioCapture = new();

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

        systemAudioCapture.SystemAudioCaptureCallback.Running += OnCapturePermissionChanged;

        if (!OperatingSystem.IsAndroidVersionAtLeast(29))
        {
            SysAudioStreamName.IsVisible = false;
            StreamSysAudioButton.IsVisible = false;
        }
    }


    private void OnStreamToggle(object? sender, ToggledEventArgs e)
    {
        //StartStreamBtn.Text = e.Value == true ? "Stop stream" : "Start stream";
        return;
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
        SysAudioStreamName.TextChanged += DataChanged;
    }

    private async Task DataChanged()
    {
        Preferences.Set("StreamName", streamName.Text);
        Preferences.Set("BufferSize", bufferSize.Text);
        Preferences.Set("SampleRate", SampleRate.SelectedItem.ToString());
        Preferences.Set("ChannelType", ChannelType.SelectedItem.ToString());
        Preferences.Set("MicType", microphone.SelectedItem.ToString());
        Preferences.Set("IPAddress", address.Text);
        Preferences.Set("Port", port.Text);
        Preferences.Set("SysAudioStreamName", SysAudioStreamName.Text);
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
        SysAudioStreamName.Text = Preferences.Get("SysAudioStreamName", "Stream2");

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
        foreach (var item in VBANConsts.VBAN_SRList.Order())
            SampleRate.Items.Add(item.ToString());

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

    private void CreateStreamManager()
    {
        try
        {
            CreateAudioRecord();
        }
        catch (Exception exception)
        {
            DisplayAlert("Error", exception.Message, "Ok");
            return;
        }

        WaveAudioRecord waveAudioRecord = new(audioRecord, int.Parse(bufferSize.Text));

        streamManager = new StreamManager(waveAudioRecord, address.Text, int.Parse(port.Text), streamName.Text);
        streamManager.StreamToggled += OnStreamToggle;
    }

    private void CreateAudioRecord()
    {
        audioRecord =
            new(Enum.Parse<AudioSource>(microphone.SelectedItem.ToString()),
                int.Parse(SampleRate.SelectedItem.ToString()),
                Enum.Parse<ChannelIn>(ChannelType.SelectedItem.ToString()),
                Encoding.Pcm16bit,
                int.Parse(bufferSize.Text));
    }

    private void CreateSysStreamManager()
    {
        try
        {
            if (OperatingSystem.IsAndroidVersionAtLeast(29))
                CreateSysAudioRecord();
            else
                CreateSysAudioRecord();
        }
        catch (Exception exception)
        {
            DisplayAlert("Error", exception.Message, "Ok");
            return;
        }

        WaveAudioRecord waveAudioRecord = new(audioRecord, int.Parse(bufferSize.Text));

        sysStreamManager = new StreamManager(waveAudioRecord, address.Text, int.Parse(port.Text), SysAudioStreamName.Text);
        sysStreamManager.StreamToggled += OnStreamToggle;
    }

    private void CreateSysAudioRecord()
    {
        var config = new AudioPlaybackCaptureConfiguration.Builder(SystemAudioCaptureCallback.MediaProjection)
            .AddMatchingUsage(AudioUsageKind.Media)
            .Build();

        var audioFormat = new AudioFormat.Builder()
            .SetEncoding(Encoding.Pcm16bit)
            .SetSampleRate(int.Parse(SampleRate.SelectedItem.ToString()))
            .SetChannelMask(ChannelOut.Stereo)
            .Build();

        audioRecord = new AudioRecord.Builder()
            .SetAudioPlaybackCaptureConfig(config)
            .SetAudioFormat(audioFormat)
            .Build();
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
    private void TryStartSysStream()
    {
        try
        {
            sysStreamManager.StartStream();
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

    private void OnMicStreamButtonClicked(object sender, EventArgs e)
    {
        var button = (ToggleButton)sender;

        if (button.IsToggled)
        {
            if (!CheckMicrophonePermission().Result)
            {
                button.IsToggled = false;
                button.Text = "start mic stream";
                return;
            }

            microphoneService.Start();

            CreateStreamManager();

            TryStartStream(denoise.IsToggled);

            button.Text = "stop mic stream";
        }
        else
        {
            streamManager.StopStream();
            microphoneService.Stop();

            button.Text = "start mic stream";
        }
    }
    private void OnSysAudioStreamButtonClicked(object sender, EventArgs e)
    {
        var button = (ToggleButton)sender;

        if (button.IsToggled)
        {
            if (!CheckMicrophonePermission().Result)
            {
                button.IsToggled = false;
                button.Text = "start apps audio stream";
                return;
            }

            button.IsToggled = false;
            mediaProjectionService.Start();
            systemAudioCapture.Start();
            return;
        }
        else
        {
            sysStreamManager.StopStream();
            systemAudioCapture.Stop();
            mediaProjectionService.Stop();
            StreamSysAudioButton.Text = "start apps audio stream";
        }
    }

    private void OnCapturePermissionChanged(bool obj)
    {
        if (!obj)
        {
            systemAudioCapture.Stop();
            mediaProjectionService.Stop();

            StreamSysAudioButton.IsToggled = false;
            StreamSysAudioButton.Text = "start apps audio stream";
            DisplayAlert("Permission not granted", "Please give stream permission to let this app work", "Ok");
            return;
        }

        CreateSysStreamManager();

        TryStartSysStream();

        StreamSysAudioButton.Text = "stop apps audio stream";
        StreamSysAudioButton.IsToggled = true;
    }
}
#endif