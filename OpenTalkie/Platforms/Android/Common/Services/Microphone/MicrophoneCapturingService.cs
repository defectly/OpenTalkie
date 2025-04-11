using OpenTalkie.Common.Services.Interfaces;

namespace OpenTalkie.Platforms.Android.Common.Services.Microphone;

public class MicrophoneCapturingService : IMicrophoneCapturingService
{
    public Action<bool>? OnServiceStateChange { get; set; }

    public async Task<bool> StartAsync()
    {
        bool isPermissionGranted = await PermissionManager.RequestMicrophonePermissionAsync();
        if (!isPermissionGranted)
            return false;

        await MicrophoneForegroundServiceManager.StartForegroundServiceAsync();

        MicrophoneAudioRecord.Start();

        OnServiceStateChange?.Invoke(true);

        return true;
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
