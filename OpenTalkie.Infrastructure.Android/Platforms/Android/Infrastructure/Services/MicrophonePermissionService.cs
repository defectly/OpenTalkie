using OpenTalkie.Application.Abstractions.Services;

namespace OpenTalkie.Infrastructure.Android.Platforms.Android.Infrastructure.Services;

public sealed class MicrophonePermissionService : IMicrophonePermissionService
{
    private readonly ILogger<MicrophonePermissionService> _logger;

    public MicrophonePermissionService(ILogger<MicrophonePermissionService> logger)
    {
        _logger = logger;
    }

    public async Task<bool> RequestMicrophonePermissionAsync()
    {
        var status = await Permissions.CheckStatusAsync<Permissions.Microphone>();
        if (status == PermissionStatus.Granted)
        {
            _logger.LogDebug("Microphone permission already granted.");
            return true;
        }

        _logger.LogInformation("Requesting microphone permission.");
        status = await Permissions.RequestAsync<Permissions.Microphone>();
        var granted = status == PermissionStatus.Granted;
        var logLevel = granted ? LogLevel.Information : LogLevel.Warning;

        if (_logger.IsEnabled(logLevel))
            _logger.Log(logLevel, "Microphone permission {Result}.", granted ? "granted" : "denied");

        return granted;
    }
}


