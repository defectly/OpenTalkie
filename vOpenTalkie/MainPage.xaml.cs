#if ANDROID
using Android.Media;
using NAudio.Wave;

namespace vOpenTalkie;

public partial class MainPage : ContentPage
{
    AudioRecord audioRecord;

    bool isStreaming = false;

    CancellationTokenSource cancelTokenSource;

    int packetCounter = 0;

    public MainPage()
    {
        InitializeComponent();
        CreateChannelTypeList();
        CreateMicInputsList();

        address.Text = "192.168.0.62";
        port.Text = "6980";
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
        isStreaming = isStreaming == true ? StopStream().Result : StartStream().Result;
    }

    private async Task<bool> StopStream()
    {
        audioRecord.Stop();

        cancelTokenSource.Cancel();
        cancelTokenSource.Dispose();

        StartStreamBtn.Text = "Start stream";

        return false;
    }

    private async Task<bool> StartStream()
    {
        if (!CheckMicrophonePermission().Result)
            return false;

        try
        {
            audioRecord = new AudioRecord(Enum.Parse<AudioSource>(microphone.SelectedItem.ToString()), 48000,
                Enum.Parse<ChannelIn>(channelType.SelectedItem.ToString()), Android.Media.Encoding.Pcm16bit, 1024);
        }
        catch (Java.Lang.IllegalArgumentException exception)
        {
            DisplayAlert("Error", "May be this format is unsupported", "Ok");
            return false;
        }

        audioRecord.StartRecording();

        Task.Run(() => Stream((cancelTokenSource = new CancellationTokenSource()).Token));

        StartStreamBtn.Text = "Stop stream";

        return true;
    }

    private async Task Stream(CancellationToken token)
    {
        var waveAudioRecord = new WaveAudioRecord(new WaveFormat(48000, 16,
        (channelType.SelectedItem.ToString() == "Mono" || channelType.SelectedItem.ToString() == "Default") ? 1 : 2), audioRecord);

        //string mainDir = FileSystem.Current.AppDataDirectory;
        //var musicSource = new WaveFileReader(Path.Combine(mainDir, "emoboy.wav"));

        //var sampleProvider = musicSource.ToSampleProvider();

        using var vbanSender = new VBANSender(waveAudioRecord.ToSampleProvider(), address.Text, int.Parse(port.Text), "defectly");
        
        StreamMic(vbanSender, token);
    }

    private async Task StreamMic(VBANSender vbanSender, CancellationToken token)
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
        var permissionStatus = await Permissions.CheckStatusAsync<Permissions.Microphone>();

        if (permissionStatus == PermissionStatus.Granted)
            return true;

        if (Permissions.RequestAsync<Permissions.Microphone>().Result != PermissionStatus.Granted)
        {
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
}
#endif