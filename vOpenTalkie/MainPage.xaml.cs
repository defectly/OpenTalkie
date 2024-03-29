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

    bool isStreaming = false;

    CancellationTokenSource cancelTokenSource;

    Android.Content.Intent intent;

    public static bool UseRNNoise;

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

        rnNoise.Toggled += RnNoise_Toggled;
    }

    private void RnNoise_Toggled(object? sender, ToggledEventArgs e) =>
        UseRNNoise = e.Value;

    private void RegisterUserInputEvents()
    {
        streamName.TextChanged += SaveData;
        bufferSize.TextChanged += SaveData;
        sampleRate.SelectedIndexChanged += SaveData;
        channelType.SelectedIndexChanged += SaveData;
        microphone.SelectedIndexChanged += SaveData;
        address.TextChanged += SaveData;
        port.TextChanged += SaveData;
    }

    private async Task SaveData()
    {
        Preferences.Set("StreamName", streamName.Text);
        Preferences.Set("BufferSize", bufferSize.Text);
        Preferences.Set("SampleRate", sampleRate.SelectedItem.ToString());
        Preferences.Set("ChannelType", channelType.SelectedItem.ToString());
        Preferences.Set("MicType", microphone.SelectedItem.ToString());
        Preferences.Set("IPAddress", address.Text);
        Preferences.Set("Port", port.Text);
    }

    private void SaveData(object sender, TextChangedEventArgs textChanged)
    {
        SaveData();
    }
    private void SaveData(object sender, EventArgs textChanged)
    {
        SaveData();
    }

    private void LoadData()
    {
        streamName.Text = Preferences.Get("StreamName", "Stream1");
        bufferSize.Text = Preferences.Get("BufferSize", "1024");
        sampleRate.SelectedItem = sampleRate.Items.FirstOrDefault(item => item == Preferences.Get("SampleRate", "48000"));
        channelType.SelectedItem = channelType.Items.FirstOrDefault(item => item == Preferences.Get("ChannelType", "Default"));
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

    private async void GetAndroidIPAddress(object sender, ConnectivityChangedEventArgs e)
    {
        GetAndroidIPAddress();
    }

    private void CreateSampleRateList()
    {
        sampleRate.Items.Add("6000");
        sampleRate.Items.Add("8000");
        sampleRate.Items.Add("11025");
        sampleRate.Items.Add("12000");
        sampleRate.Items.Add("16000");
        sampleRate.Items.Add("22050");
        sampleRate.Items.Add("24000");
        sampleRate.Items.Add("32000");
        sampleRate.Items.Add("44100");
        sampleRate.Items.Add("48000");
        sampleRate.Items.Add("64000");
        sampleRate.Items.Add("88200");
        sampleRate.Items.Add("96000");
        sampleRate.Items.Add("128000");
        sampleRate.Items.Add("176400");
        sampleRate.Items.Add("192000");
        sampleRate.Items.Add("256000");
        sampleRate.Items.Add("352800");
        sampleRate.Items.Add("384000");
        sampleRate.Items.Add("512000");
        sampleRate.Items.Add("705600");

        sampleRate.SelectedItem = sampleRate.Items.FirstOrDefault(item => item == "48000");
    }

    private void CreateChannelTypeList()
    {
        foreach (var source in Enum.GetNames<ChannelIn>())
            channelType.Items.Add(source.ToString());

        channelType.SelectedItem = channelType.Items.FirstOrDefault(item => item == "Default");

    }

    private void CreateMicInputsList()
    {
        foreach (var source in Enum.GetNames<AudioSource>())
            microphone.Items.Add(source.ToString());

        microphone.SelectedItem = microphone.Items.FirstOrDefault(item => item == "Default");
    }

    private void OnCounterClicked(object sender, EventArgs e)
    {
        isStreaming = isStreaming == true ? StopStream().Result : StartStream();
    }

    private async Task<bool> StopStream()
    {
        audioRecord.Stop();

        cancelTokenSource.Cancel();
        cancelTokenSource.Dispose();

        Android.App.Application.Context.StopService(intent);

        StartStreamBtn.Text = "Start stream";

        return false;
    }

    private bool StartStream()
    {
        if (!CheckMicrophonePermission().Result)
            return false;

        try
        {
            audioRecord = new AudioRecord(Enum.Parse<AudioSource>(microphone.SelectedItem.ToString()),
                int.Parse(sampleRate.SelectedItem.ToString()),
                Enum.Parse<ChannelIn>(channelType.SelectedItem.ToString()), Android.Media.Encoding.Pcm16bit, int.Parse(bufferSize.Text));

            audioRecord.StartRecording();
        }
        catch (Exception exception)
        when (exception is Java.Lang.IllegalArgumentException || exception is Java.Lang.IllegalStateException)
        {
            DisplayAlert("Error", "May be this format is unsupported", "Ok");
            return false;
        }

        Task.Run(() => Stream((cancelTokenSource = new CancellationTokenSource()).Token));

        StartStreamBtn.Text = "Stop stream";

        intent ??= new Android.Content.Intent(Android.App.Application.Context, typeof(ForegroundServiceDemo));
        Android.App.Application.Context.StartForegroundService(intent);

        return true;
    }

    private void Stream(CancellationToken token)
    {
        WaveFormat format = new(int.Parse(sampleRate.SelectedItem.ToString()), 16,
        (channelType.SelectedItem.ToString() == "Mono" || channelType.SelectedItem.ToString() == "Default") ? 1 : 2);

        var waveAudioRecord = new WaveAudioRecord(format, audioRecord);

        using var vbanSender = new VBANSender(waveAudioRecord.ToSampleProvider(), address.Text, int.Parse(port.Text), streamName.Text);

        StreamMic(vbanSender, token);
    }

    private void StreamMic(VBANSender vbanSender, CancellationToken token)
    {
        float[] vbanBuffer = new float[int.Parse(bufferSize.Text) / 4];

        while (true)
        {
            if (token.IsCancellationRequested)
                return;

            vbanSender.Read(vbanBuffer, 0, vbanBuffer.Length);
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

    public class WaveAudioRecord : IWaveProvider
    {
        private AudioRecord _audioRecord;
        private WaveFormat _waveFormat;
        public WaveFormat WaveFormat => _waveFormat;

        DoggyDenoiser _denoiser;

        public WaveAudioRecord(WaveFormat waveFormat, AudioRecord audioRecord)
        {
            _waveFormat = waveFormat;
            _audioRecord = audioRecord;

            _denoiser = new DoggyDenoiser();
        }


        public int Read(byte[] buffer, int offset, int count) =>
            MainPage.UseRNNoise == true ? ReadRNNoise(buffer, offset, count) : JustRead(buffer, offset, count);

        private int JustRead(byte[] buffer, int offset, int count) =>
            _audioRecord.Read(buffer, offset, count);

        private int ReadRNNoise(byte[] buffer, int offset, int count)
        {
            int length = _audioRecord.Read(buffer, offset, count);

            float[] rnBuffer = Convert16BitToFloat(buffer.Take(length).ToArray());

            int denoisedLength = _denoiser.Denoise(rnBuffer, false);

            buffer = ConvertFloatTo16Bit(rnBuffer.Take(denoisedLength).ToArray());

            return buffer.Length;
        }

        public static float[] Convert16BitToFloat(byte[] input)
        {
            // 16 bit input, so 2 bytes per sample
            int inputSamples = input.Length / 2;
            float[] output = new float[inputSamples];
            int outputIndex = 0;
            for (int n = 0; n < inputSamples; n++)
            {
                short sample = BitConverter.ToInt16(input, n * 2);
                output[outputIndex++] = sample / 32768f;
            }
            return output;
        }

        public static byte[] ConvertFloatTo16Bit(float[] samples)
        {
            int samplesCount = samples.Length;
            var pcm = new byte[samplesCount * 2];
            int sampleIndex = 0, pcmIndex = 0;

            while (sampleIndex < samplesCount)
            {
                var outsample = (short)(samples[sampleIndex] * short.MaxValue);
                pcm[pcmIndex] = (byte)(outsample & 0xff);
                pcm[pcmIndex + 1] = (byte)((outsample >> 8) & 0xff);

                sampleIndex++;
                pcmIndex += 2;
            }

            return pcm;
        }
    }
}
#endif