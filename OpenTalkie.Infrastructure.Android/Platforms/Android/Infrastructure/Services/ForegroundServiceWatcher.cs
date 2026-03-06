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

    public static void Configure(IWakeLockService service)
    {
        wakeLockService = service;
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
            System.Diagnostics.Debug.WriteLine($"ForegroundServiceWatcher: unknown service name '{serviceName}'.");
            return;
        }

        if (wakeLockService == null)
        {
            System.Diagnostics.Debug.WriteLine("ForegroundServiceWatcher: IWakeLockService is unavailable.");
            return;
        }

        if (microphoneForegroundServiceState || mediaProjectionForegroundServiceState || receiverForegroundServiceState)
            wakeLockService.Acquire();
        else
            wakeLockService.Release();
    }
}
