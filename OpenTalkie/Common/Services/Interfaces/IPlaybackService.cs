using NAudio.Wave;

namespace OpenTalkie.Common.Services.Interfaces;

public interface IPlaybackService
{
    int Read(byte[] buffer, int offset, int count);
    Task<int> ReadAsync(byte[] buffer, int offset, int count);
    ISampleProvider ToSampleProvider();
    IWaveProvider ToWaveProvider();
    void Start();
    void Stop();
    int GetBufferSize();
    Task<bool> RequestPermissionAsync();
}