namespace OpenTalkie.Application.Abstractions.Services;

public interface IAudioOutputService
{
    bool IsStarted { get; }

    void SetPrefferedAudioDevice(string prefferedDevice);
    void Start(int sampleRate, int channels);
    void Stop();
    void Write(byte[] buffer, int offset, int count);
}


