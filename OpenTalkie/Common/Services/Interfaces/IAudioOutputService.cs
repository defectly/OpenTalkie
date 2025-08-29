namespace OpenTalkie.Common.Services.Interfaces;

public interface IAudioOutputService
{
    bool IsStarted { get; }
    void Start(int sampleRate, int channels);
    void Stop();
    void Write(byte[] buffer, int offset, int count);
}

