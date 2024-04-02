#if ANDROID
using NAudio.Wave;

namespace vOpenTalkie;

internal class StreamManager
{
    public bool IsStreaming = false;
    public WaveAudioRecord WaveAudioRecord;
    public ISampleProvider SampleAudioRecord => WaveAudioRecord.ToSampleProvider();
    public EventHandler<ToggledEventArgs> StreamToggled;

    CancellationTokenSource _cancelTokenSource;
    int BufferSize => WaveAudioRecord.BufferSize;
    string Hostname;
    Task currentStream;
    int Port;

    string StreamName;

    public StreamManager(WaveAudioRecord waveAudioRecord, string hostname, int port, string streamName)
    {
        StreamName = streamName;
        Hostname = hostname;
        Port = port;

        WaveAudioRecord = waveAudioRecord;
    }

    public void StopStream()
    {
        if (!IsStreaming)
            return;

        if (currentStream != null && currentStream.Status == TaskStatus.RanToCompletion)
            return;

        WaveAudioRecord.Stop();

        _cancelTokenSource?.Cancel();
        _cancelTokenSource?.Dispose();

        IsStreaming = false;
        StreamToggled?.Invoke(this, new ToggledEventArgs(false));
    }

    public void StartStream(bool useDenoise = false)
    {
        if (IsStreaming)
            return;

        if (currentStream != null && currentStream.Status == TaskStatus.Running)
            return;

        WaveAudioRecord.Start();

        currentStream = Task.Run(() => Stream(useDenoise));
        //Stream(useDenoise);
        IsStreaming = true;
        StreamToggled?.Invoke(this, new ToggledEventArgs(true));
    }

    private void Stream(bool useDenoise)
    {
        ISampleProvider sampleProvider;

        if (useDenoise)
            sampleProvider = new DenoisedSampleProvider(SampleAudioRecord);
        else
            sampleProvider = SampleAudioRecord;

        if (WaveAudioRecord.WaveFormat.Channels < 2)
            sampleProvider = sampleProvider.ToStereo();


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