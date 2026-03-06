using OpenTalkie.Application.Abstractions.Repositories;
using OpenTalkie.Application.Abstractions.Services;
using OpenTalkie.Domain.Models;

namespace OpenTalkie.Infrastructure.Android.Platforms.Android.Infrastructure.Services.Microphone;

public class MicrophoneCapturingService : IMicrophoneCapturingService
{
    public Action<bool>? OnServiceStateChange { get; set; }

    public MicrophoneCapturingService(IMicrophoneRepository microphoneRepository)
    {
        MicrophoneAudioRecord.Configure(microphoneRepository);
        microphoneRepository.PreferredAudioInputDeviceChanged += MicrophoneAudioRecord.SetPreferredAudioDevice;
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
            MicrophoneForegroundServiceManager.StopForegroundService();
            OnServiceStateChange?.Invoke(false);
            throw;
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
