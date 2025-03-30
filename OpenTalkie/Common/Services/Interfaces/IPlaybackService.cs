namespace OpenTalkie.Common.Services.Interfaces;

public interface IPlaybackService : IInputStream
{
    void Start();
    void Stop();
    int GetBufferSize();
    Task<bool> RequestPermissionAsync();
}