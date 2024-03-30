#if ANDROID
using Android.Media;
using NAudio.Wave;

namespace vOpenTalkie;

public class WaveAudioRecord : IWaveProvider
{
    public WaveFormat WaveFormat => _waveFormat;
    public int BufferSize => _audioRecord.BufferSizeInFrames;
    private WaveFormat _waveFormat;
    private AudioRecord _audioRecord;

    public WaveAudioRecord(AudioRecord audioRecord)
    {
        _audioRecord = audioRecord;
        _waveFormat = new WaveFormat(_audioRecord.SampleRate, 16, audioRecord.ChannelCount);
    }

    public int Read(byte[] buffer, int offset, int count) =>
        _audioRecord.Read(buffer, offset, count);

    public void Stop() => _audioRecord.Stop();
    public void Start() => _audioRecord.StartRecording();
}
#endif