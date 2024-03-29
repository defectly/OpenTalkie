using Android.Media;
using NAudio.Wave;

namespace vOpenTalkie;

internal class StreamManager
{
    public bool IsStreaming = false;
    public Action OnStreamToggle;

    CancellationTokenSource _cancelTokenSource;

    public WaveAudioRecord WaveAudioRecord;

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

    public void EnableRNNoise() => WaveAudioRecord.RNNoise = true;
    public void DisableRNNoise() => WaveAudioRecord.RNNoise = false;

    public void StopStream()
    {
        WaveAudioRecord.Stop();

        _cancelTokenSource.Cancel();
        _cancelTokenSource.Dispose();

        IsStreaming = false;
        OnStreamToggle?.Invoke();
    }
    public void StartStream()
    {
        WaveAudioRecord.Start();

        Task.Run(() => Stream());

        IsStreaming = true;
        OnStreamToggle?.Invoke();
    }

    private void Stream()
    {
        using var vbanSender = new VBANSender(WaveAudioRecord.ToSampleProvider(), Hostname, Port, StreamName);

        StreamMic(vbanSender, (_cancelTokenSource = new CancellationTokenSource()).Token);
    }

    private void StreamMic(VBANSender vbanSender, CancellationToken token)
    {
        float[] vbanBuffer = new float[BufferSize / 4];

        while (true)
        {
            if (token.IsCancellationRequested)
                return;

            vbanSender.Read(vbanBuffer, 0, vbanBuffer.Length);
        }
    }
}
