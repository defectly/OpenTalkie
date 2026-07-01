using OpenTalkie.Application.Abstractions.Repositories;
using OpenTalkie.Application.Abstractions.Services;
using OpenTalkie.Domain.Models;

namespace OpenTalkie.Infrastructure.Android.Platforms.Android.Infrastructure.Services.Microphone;

public class MicrophoneCapturingService : IMicrophoneCapturingService
{
    private readonly ILogger<MicrophoneCapturingService> _logger;

    public Action<bool>? OnServiceStateChange { get; set; }

    public MicrophoneCapturingService(
        IMicrophoneRepository microphoneRepository,
        ILogger<MicrophoneCapturingService> logger)
    {
        _logger = logger;
        MicrophoneAudioRecord.Configure(microphoneRepository);
        microphoneRepository.PreferredAudioInputDeviceChanged += MicrophoneAudioRecord.SetPreferredAudioDevice;
    }

    public async Task StartAsync()
    {
        _logger.LogInformation("Starting microphone capturing service.");
        await MicrophoneForegroundServiceManager.StartForegroundServiceAsync();

        try
        {
            MicrophoneAudioRecord.Start();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Microphone audio record failed to start.");
            MicrophoneForegroundServiceManager.StopForegroundService();
            OnServiceStateChange?.Invoke(false);
            throw;
        }

        OnServiceStateChange?.Invoke(true);
        _logger.LogInformation("Microphone capturing service started.");
    }

    public void Stop()
    {
        _logger.LogInformation("Stopping microphone capturing service.");
        MicrophoneAudioRecord.Stop();
        MicrophoneForegroundServiceManager.StopForegroundService();
        OnServiceStateChange?.Invoke(false);
        _logger.LogInformation("Microphone capturing service stopped.");
    }

    public async Task<int> ReadAsync(byte[] buffer, int offset, int count)
    {
        return await MicrophoneAudioRecord.ReadAsync(buffer, offset, count);
    }

    public int GetBufferSize() => MicrophoneAudioRecord.BufferSize;

    public WaveFormat GetWaveFormat() => MicrophoneAudioRecord.GetWaveFormat();
}
