using Android.Media;
using NAudio.Wave;

namespace OpenTalkie.Platforms.Android;

public class ProvideHelper(AudioRecord audioRecord) : IWaveProvider, IDisposable
{
    public WaveFormat WaveFormat =>
        new(audioRecord.SampleRate, MapEncoding(audioRecord.Format.Encoding), audioRecord.Format.ChannelCount);

    public int Read(byte[] buffer, int offset, int count)
    {
        var read = audioRecord.Read(buffer, offset, count);
        return read;
    }

    private int MapEncoding(Encoding encoding)
    {
        return encoding switch
        {
            Encoding.Pcm8bit => 8,
            Encoding.Pcm16bit => 16,
            Encoding.Pcm24bitPacked => 24,
            Encoding.Pcm32bit => 32,
            _ => throw new NotSupportedException($"No such encoding supported: {encoding}")
        };
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
    }
}
