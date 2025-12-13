using OpenTalkie.Common.Repositories.Interfaces;
using OpenTalkie.Common.Services.Interfaces;

namespace OpenTalkie.Platforms.Android.Common.Services.Microphone;

public class MicrophoneCapturingService : IMicrophoneCapturingService
{
    public Action<bool>? OnServiceStateChange { get; set; }

    public MicrophoneCapturingService(IMicrophoneRepository microphoneRepository)
    {
        microphoneRepository.PrefferedAudioInputDeviceChanged += MicrophoneAudioRecord.SetPrefferedAudioDevice;
    }

    public async Task StartAsync()
    {
        await MicrophoneForegroundServiceManager.StartForegroundServiceAsync();

        try
        {
            MicrophoneAudioRecord.Start();
        }
        catch
        {
            // If starting AudioRecord failed (e.g., incompatible format),
            // ensure the foreground service is stopped to avoid a lingering notification/service.
            MicrophoneForegroundServiceManager.StopForegroundService();
            OnServiceStateChange?.Invoke(false);
            throw; // propagate to caller so UI can show the error message
        }

        OnServiceStateChange?.Invoke(true);
    }

    public void Stop()
    {
        MicrophoneAudioRecord.Stop();
        MicrophoneForegroundServiceManager.StopForegroundService();
        OnServiceStateChange?.Invoke(false);
    }

    public async Task<int> ReadAsync(byte[] buffer, int offset, int count)
    {
        return await MicrophoneAudioRecord.ReadAsync(buffer, offset, count);
    }

    public int GetBufferSize() => MicrophoneAudioRecord.BufferSize;

    public WaveFormat GetWaveFormat() => MicrophoneAudioRecord.GetWaveFormat();
}
