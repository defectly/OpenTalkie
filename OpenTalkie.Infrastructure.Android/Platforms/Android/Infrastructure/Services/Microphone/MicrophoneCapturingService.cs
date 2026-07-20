using OpenTalkie.Application.Abstractions.Repositories;
using OpenTalkie.Application.Abstractions.Services;

namespace OpenTalkie.Infrastructure.Android.Platforms.Android.Infrastructure.Services.Microphone;

public class MicrophoneCapturingService : IMicrophoneCapturingService
{
    private readonly ForegroundServicePowerCoordinator _powerCoordinator;

    public Action<bool>? OnServiceStateChange { get; set; }

    public MicrophoneCapturingService(
        IMicrophoneRepository microphoneRepository,
        IAudioManagerSettingsRepository audioManagerSettingsRepository,
        ForegroundServicePowerCoordinator powerCoordinator)
    {
        _powerCoordinator = powerCoordinator;
        MicrophoneAudioRecord.Configure(microphoneRepository, audioManagerSettingsRepository);
        microphoneRepository.PreferredAudioInputDeviceChanged += MicrophoneAudioRecord.SetPreferredAudioDevice;
    }

    public async Task StartAsync()
    {
        await MicrophoneForegroundServiceManager.StartForegroundServiceAsync();

        try
        {
            MicrophoneAudioRecord.Start();
            _powerCoordinator.SetMicrophoneActive(true);
        }
        catch
        {
            MicrophoneForegroundServiceManager.StopForegroundService();
            _powerCoordinator.SetMicrophoneActive(false);
            OnServiceStateChange?.Invoke(false);
            throw;
        }

        OnServiceStateChange?.Invoke(true);
    }

    public void Stop()
    {
        MicrophoneAudioRecord.Stop();
        MicrophoneForegroundServiceManager.StopForegroundService();
        _powerCoordinator.SetMicrophoneActive(false);
        OnServiceStateChange?.Invoke(false);
    }

    public async Task<int> ReadAsync(byte[] buffer, int offset, int count) =>
        await MicrophoneAudioRecord.ReadAsync(buffer, offset, count);

    public int GetBufferSize() => MicrophoneAudioRecord.BufferSize;

    public WaveFormat GetWaveFormat() => MicrophoneAudioRecord.GetWaveFormat();
}
