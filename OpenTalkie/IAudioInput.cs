using NAudio.Wave;

namespace OpenTalkie;

public interface IAudioInput : IDisposable
{
    public int BufferSize { get; set; }

    void SetAndroidOptions(int audioSource, int sampleRate, int channel, int encoding, int bufferSize);
    Task<int> ReadAsync(byte[] buffer, int offset, int count);
    IWaveProvider ToWaveProvider();
    ISampleProvider ToSampleProvider();
}
