#if ANDROID
using Android.Media;
using Microsoft.Maui.Controls.Internals;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;

namespace vOpenTalkie;

public partial class MainPage : ContentPage
{
    AudioRecord audioRecord;

    StreamManager streamManager;

    ForegroundBatteryService batteryService = new();

    bool dataChanged = false;

    public MainPage()
    {
        InitializeComponent();

        BindingContext = this;

        CreateSampleRateList();
        CreateChannelTypeList();
        CreateMicInputsList();

        LoadData();
        RegisterUserInputEvents();

        GetAndroidIPAddress();
        Connectivity.Current.ConnectivityChanged += GetAndroidIPAddress;

        denoise.Toggled += OnDenoiseToggle;

        TalkieUI.JSInvokeTarget = new MyJSInvokeTarget(this);

    }

    public string GetPort()
    {
        return port.Text;
    }
    public bool GetStreamState()
    {
        return streamManager.IsStreaming;
    }

    private async void OnHybridWebViewRawMessageReceived(object sender, HybridWebView.HybridWebViewRawMessageReceivedEventArgs e)
    {
        await Dispatcher.DispatchAsync(async () =>
        {
            string[] data = e.Message.Split(";");

            switch (data[0])
            {
                case "hostname":
                    address.Text = data[1];
                    DataChanged();
                    break;
                case "port":
                    port.Text = data[1];
                    DataChanged();
                    break;
                case "denoise":
                    OnDenoiseToggle(this, new ToggledEventArgs(bool.Parse(data[1])));
                    denoise.IsToggled = bool.Parse(data[1]);
                    break;
                case "stream":
                    OnStartStreamBtnClick(this, new EventArgs());
                    break;
            }

            //await DisplayAlert("JavaScript message", e.Message, "OK");
        });
    }

    private void OnStreamToggle(object? sender, ToggledEventArgs e) =>
        StartStreamBtn.Text = e.Value == true ? "Stop stream" : "Start stream";

    private void OnDenoiseToggle(object? sender, ToggledEventArgs e)
    {
        if (streamManager == null || !streamManager.IsStreaming)
            return;

        streamManager.StopStream();
        streamManager.StartStream(useDenoise: e.Value);
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

    private void OnStartStreamBtnClick(object sender, EventArgs e) =>
        OnStartStreamButtonClick();

    public bool OnStartStreamButtonClick()
    {
        if (streamManager == null)
        {
            if (!CheckMicrophonePermission().Result)
                return false;

            CreateStreamManager();

            if (streamManager == null)
                return false;
        }

        if (streamManager.IsStreaming)
        {
            streamManager.StopStream();
            batteryService.Stop();

            return false;
        }
        else
        {
            if (!CheckMicrophonePermission().Result)
                return false;

            if (dataChanged)
            {
                CreateStreamManager();
                dataChanged = false;
            }

            TryStartStream(denoise.IsToggled);

            batteryService.Start();

            return true;
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

        WaveAudioRecord waveAudioRecord = new(audioRecord, int.Parse(bufferSize.Text));

        streamManager = new StreamManager(waveAudioRecord, address.Text, int.Parse(port.Text), streamName.Text);
        streamManager.StreamToggled += OnStreamToggle;
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

    internal string GetHostname()
    {
        return address.Text;
    }

    internal bool GetDenoiseState()
    {
        return denoise.IsToggled;
    }
}

class MyJSInvokeTarget
{
    private readonly MainPage _mainPage;

    public MyJSInvokeTarget(MainPage mainPage)
    {
        _mainPage = mainPage;
    }

    public string FillAttribute(string attribute)
    {
        switch(attribute)
        {
            case "hostname":
                return _mainPage.GetHostname();
            case "port":
                return _mainPage.GetPort();

            default:
                return null;
        }
    }

    public bool GetDenoiseState()
    {
        return _mainPage.GetDenoiseState();
    }
    public bool GetStreamState()
    {
        return _mainPage.GetStreamState();
    }

    public bool ToggleStream()
    {
       return _mainPage.OnStartStreamButtonClick();
    }
}
#endif