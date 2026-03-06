namespace OpenTalkie.Application.Abstractions.Services;

public interface IPlaybackService : IInputStream
{
    void Start();
    void Stop();
    int GetBufferSize();
    Task<bool> RequestPermissionAsync();
}
