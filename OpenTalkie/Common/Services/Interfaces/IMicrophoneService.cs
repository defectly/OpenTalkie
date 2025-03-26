using NAudio.Wave;

namespace OpenTalkie.Common.Services;

public interface IMicrophoneService : IDisposable
{
    int BufferSize { get; set; }

    int Read(byte[] buffer, int offset, int count);
    Task<int> ReadAsync(byte[] buffer, int offset, int count);
    ISampleProvider ToSampleProvider();
    IWaveProvider ToWaveProvider();
    void Start();
    void Stop();
}