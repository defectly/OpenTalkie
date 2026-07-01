using OpenTalkie.Application.Abstractions.Services;
using OpenTalkie.Infrastructure.Android.Platforms.Android.Infrastructure.Services.Microphone;
using OpenTalkie.Infrastructure.Android.Platforms.Android.Infrastructure.Services.Playback;
using OpenTalkie.Infrastructure.Android.Platforms.Android.Infrastructure.Services.Receiver;

namespace OpenTalkie.Infrastructure.Android.Platforms.Android.Infrastructure.Services;

public static class ForegroundServiceWatcher
{
    private static bool microphoneForegroundServiceState;
    private static bool mediaProjectionForegroundServiceState;
    private static bool receiverForegroundServiceState;
    private static IWakeLockService? wakeLockService;
    private static ILogger<ForegroundServiceWatcherLogScope>? logger;

    public static void Configure(IWakeLockService service, ILogger<ForegroundServiceWatcherLogScope> log)
    {
        wakeLockService = service;
        logger = log;
    }

    public static void NotifyServiceState(string serviceName, bool serviceState)
    {
        var recognized = true;
        switch (serviceName)
        {
            case nameof(MicrophoneForegroundService):
                microphoneForegroundServiceState = serviceState;
                break;
            case nameof(MediaProjectionForegroundService):
                mediaProjectionForegroundServiceState = serviceState;
                break;
            case nameof(ReceiverForegroundService):
                receiverForegroundServiceState = serviceState;
                break;
            default:
                recognized = false;
                break;
        }

        if (!recognized)
        {
            if (logger?.IsEnabled(LogLevel.Warning) == true)
                logger.LogWarning("ForegroundServiceWatcher received unknown service name '{ServiceName}'.", serviceName);

            return;
        }

        if (wakeLockService == null)
        {
            logger?.LogWarning("ForegroundServiceWatcher has no wake lock service.");
            return;
        }

        if (logger?.IsEnabled(LogLevel.Information) == true)
        {
            logger.LogInformation("Foreground service {ServiceName} state changed to {ServiceState}.", serviceName, serviceState);
        }

        if (microphoneForegroundServiceState || mediaProjectionForegroundServiceState || receiverForegroundServiceState)
            wakeLockService.Acquire();
        else
            wakeLockService.Release();
    }
}

public sealed class ForegroundServiceWatcherLogScope;
