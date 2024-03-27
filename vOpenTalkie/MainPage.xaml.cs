#if ANDROID
using Android.Media;
using Microsoft.Maui.Controls.PlatformConfiguration;
using NAudio.Wave;
using Android.OS;
using Android.Content;

namespace vOpenTalkie;

public partial class MainPage : ContentPage
{
    AudioRecord audioRecord;

    bool isStreaming = false;

    CancellationTokenSource cancelTokenSource;

    public MainPage()
    {
        InitializeComponent();
        CreateSampleRateList();
        CreateChannelTypeList();
        CreateMicInputsList();

        address.Text = "192.168.0.62";
        port.Text = "6980";

        Battery.BatteryOptimizationTurned += BatteryOptimizationTurned;
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

    private void BatteryOptimizationTurned()
    {
        batteryOptimizationBtn.IsVisible = false;
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
                Enum.Parse<ChannelIn>(channelType.SelectedItem.ToString()), Android.Media.Encoding.Pcm16bit, 1024);

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

        return true;
    }

    private void Stream(CancellationToken token)
    {
        var waveAudioRecord = new WaveAudioRecord(new WaveFormat(int.Parse(sampleRate.SelectedItem.ToString()), 16,
        (channelType.SelectedItem.ToString() == "Mono" || channelType.SelectedItem.ToString() == "Default") ? 1 : 2), audioRecord);
        
        using var vbanSender = new VBANSender(waveAudioRecord.ToSampleProvider(), address.Text, int.Parse(port.Text), streamName.Text);

        StreamMic(vbanSender, token);
    }

    private void StreamMic(VBANSender vbanSender, CancellationToken token)
    {
        float[] vbanBuffer = new float[1024];

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


        public WaveAudioRecord(WaveFormat waveFormat, AudioRecord audioRecord)
        {
            _waveFormat = waveFormat;
            _audioRecord = audioRecord;
        }


        public int Read(byte[] buffer, int offset, int count)
        {
            return _audioRecord.Read(buffer, offset, count);
        }

    }

    private void batteryOptimizationBtn_Clicked(object sender, EventArgs e)
    {
        Battery.BatteryOptimizationDialog?.Invoke();
    }
}
#endif