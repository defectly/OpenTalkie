using OpenTalkie.Common.Services.Interfaces;
using OpenTalkie.Platforms.Android.Common.Services.Microphone;
using OpenTalkie.Platforms.Android.Common.Services.Playback;
using OpenTalkie.Platforms.Android.Common.Services.Receiver;

namespace OpenTalkie.Platforms.Android.Common.Services;

public static class ForegroundServiceWatcher
{
    private static bool microphoneForegroundServiceState;
    private static bool mediaProjectionForegroundServiceState;
    private static bool receiverForegroundServiceState;

    private static readonly IWakeLockService wakeLockService = IPlatformApplication.Current?.Services.GetService<IWakeLockService>()!;

    public static void NotifyServiceState(string serviceName, bool serviceState)
    {

        _ = serviceName switch
        {
            nameof(MicrophoneForegroundService) => microphoneForegroundServiceState = serviceState,
            nameof(MediaProjectionForegroundService) => mediaProjectionForegroundServiceState = serviceState,
            nameof(ReceiverForegroundService) => receiverForegroundServiceState = serviceState,
            _ => throw new ArgumentOutOfRangeException(nameof(serviceName), $"Not expected service name value: {serviceName}"),
        };

        if (microphoneForegroundServiceState || mediaProjectionForegroundServiceState || receiverForegroundServiceState)
            wakeLockService.Acquire();
        else
            wakeLockService.Release();
    }
}
