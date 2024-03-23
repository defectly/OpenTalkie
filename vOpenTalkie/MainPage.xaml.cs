#if ANDROID
using Android.Content;
using Android.Media;
using Android.Net.Rtp;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using System.Net;
using System.Net.Sockets;
using Vban;
using Vban.Model;

namespace vOpenTalkie;

public partial class MainPage : ContentPage
{
    AudioRecord audioRecord = new AudioRecord(AudioSource.Mic, 48000, ChannelIn.Mono, Encoding.Pcm16bit, 512);

    bool isStreaming = false;

    CancellationTokenSource cancelTokenSource;

    public MainPage()
    {
        InitializeComponent();

        address.Text = "192.168.0.130";
        port.Text = "6980";
    }

    private void OnCounterClicked(object sender, EventArgs e)
    {
        isStreaming = isStreaming == true ? StopStream().Result : StartStream().Result;

        //isStreaming = false ? StopStream().Result : StartStream().Result;

        //DisplayAlert("Success", "Stream started", "Ok");

        //count++;

        //if (count == 1)
        //    StartStreamBtn.Text = $"Clicked {count} time";
        //else
        //    StartStreamBtn.Text = $"Clicked {count} times";

        //SemanticScreenReader.Announce(StartStreamBtn.Text);

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
        if (!await CheckMicrophonePermission())
            return false;

        audioRecord.StartRecording();

        Stream((cancelTokenSource = new CancellationTokenSource()).Token);

        StartStreamBtn.Text = "Stop stream";

        return true;
    }

    private async Task<bool> CheckMicrophonePermission()
    {
        var permissionStatus = await Permissions.CheckStatusAsync<Permissions.Microphone>();

        if (permissionStatus == PermissionStatus.Granted)
            return true;

        if (Permissions.RequestAsync<Permissions.Microphone>().Result != PermissionStatus.Granted)
        {
            await DisplayAlert("Mic permission", "Please, give mic permission to let this app work", "Ok");
            return false;
        }

        return true;
    }

    private async Task Stream(CancellationToken token)
    {
        IPAddress streamAddress = IPAddress.Parse(address.Text);
        int streamPort = int.Parse(port.Text);

        //var factory = CreateFactory();

        //var vbanStream = new VBANOutputStream<AudioFrame>(CreateFactory(), streamAddress, streamPort);

        byte[] buffer = new byte[1436];
        int length;


        //string mainDir = FileSystem.Current.AppDataDirectory;
        //var musicSource = new WaveFileReader(Path.Combine(mainDir, "emoboy.wav"));

        var udpClient = new UdpClient(AddressFamily.InterNetwork);

        int counter = 0;

        byte[] packetHead;

        while ((length = await audioRecord.ReadAsync(buffer, 0, buffer.Length)) > 0)
        {
            if (token.IsCancellationRequested)
                return;

            packetHead = new VBANPacketHead<AudioFrame>(
                protocol: 0,
                sampleRateIndex: 3,
                samples: 255,
                channel: 1,
                format: 1,
                codec: VBAN.Codec.PCM,
                streamName: "defectly",
                frameCounter: counter++).Bytes;

            var combinedPacket = packetHead.Concat(buffer).ToArray();
            var combinedLength = length + packetHead.Length;

            udpClient.Send(combinedPacket, combinedLength, new IPEndPoint(IPAddress.Parse(address.Text), int.Parse(port.Text)));
        }
    }

    //private VBANPacket<AudioFrame>.Factory<AudioFrame, AudioFrame> CreateFactory()
    //{
    //    //var packetHead = new VBANPacketHead<AudioFrame>(
    //    //    protocol: 0,
    //    //    sampleRateIndex: 3,
    //    //    samples: 8,
    //    //    channel: 1,
    //    //    format: 1,
    //    //    codec: VBAN.Codec.PCM,
    //    //    streamName: "defectly",
    //    //    frameCounter: 0);

    //    var headBuilder = VBANPacketHead<AudioFrame>.Factory<AudioFrame, AudioFrame>.CreateBuilder(VBAN.Protocol<AudioFrame>.Audio);

    //    headBuilder.Samples = 8;
    //    headBuilder.Channel = 1;
    //    headBuilder.StreamName = "defectly";

    //    var headFactory = headBuilder.Build();

    //    var builder = VBANPacket<AudioFrame>.Factory<AudioFrame, AudioFrame>.CreateBuilder(VBAN.Protocol<AudioFrame>.Audio);

    //    builder.HeadFactory = headFactory;

    //    var factory = builder.Build();

    //    return factory;
    //}
}
#endif