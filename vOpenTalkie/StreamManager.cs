#if ANDROID
using Android.Media;
using NAudio.Wave;

namespace vOpenTalkie;

internal class StreamManager
{
    public bool IsStreaming = false;
    public Action OnStreamToggle;

    CancellationTokenSource _cancelTokenSource;

    public WaveAudioRecord WaveAudioRecord;
    public ISampleProvider SampleAudioRecord => WaveAudioRecord.ToSampleProvider();

    int BufferSize => WaveAudioRecord.BufferSize;

    string Hostname;

    int Port;

    string StreamName;

    public StreamManager(WaveAudioRecord waveAudioRecord, string hostname, int port, string streamName)
    {
        StreamName = streamName;
        Hostname = hostname;
        Port = port;

        WaveAudioRecord = waveAudioRecord;
    }

    public void DenoiseOn()
    {
        StopStream();
        StartStream(useDenoise: true);
    }

    public void DenoiseOff()
    {
        StopStream();
        StartStream();
    }

    public void StopStream()
    {
        if (!IsStreaming)
            return;

        WaveAudioRecord.Stop();

        _cancelTokenSource.Cancel();
        _cancelTokenSource.Dispose();

        IsStreaming = false;
        OnStreamToggle?.Invoke();
    }

    public void StartStream(bool useDenoise = false)
    {
        if (IsStreaming)
            return;

        WaveAudioRecord.Start();

        Task.Run(() => Stream(useDenoise));

        IsStreaming = true;
        OnStreamToggle?.Invoke();
    }

    private void Stream(bool useDenoise)
    {
        ISampleProvider sampleProvider;

        if (useDenoise)
            sampleProvider = new DenoisedSampleProvider(SampleAudioRecord);
        else
            sampleProvider = SampleAudioRecord;

        using var vbanSender = new VBANSender(sampleProvider, Hostname, Port, StreamName);

        Send(vbanSender, (_cancelTokenSource = new CancellationTokenSource()).Token);
    }

    private void Send(VBANSender vbanSender, CancellationToken token)
    {
        float[] vbanBuffer = new float[BufferSize / 2];

        while (true)
        {
            if (token.IsCancellationRequested)
                return;

            vbanSender.Read(vbanBuffer, 0, vbanBuffer.Length);
        }
    }
}
#endif