using NAudio.Wave;

namespace OpenTalkie.Common.Services.Interfaces;

public interface IPlaybackService : IInputStream
{
    int Read(byte[] buffer, int offset, int count);
    ISampleProvider ToSampleProvider();
    IWaveProvider ToWaveProvider();
    void Start();
    void Stop();
    int GetBufferSize();
    Task<bool> RequestPermissionAsync();
}