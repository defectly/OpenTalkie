using OpenTalkie.Application.Abstractions.Services;

namespace OpenTalkie.Infrastructure.Android.Platforms.Android.Infrastructure.Services;

public sealed class ForegroundServicePowerCoordinator(
    IWakeLockService wakeLockService,
    IWifiLockService wifiLockService)
{
    private readonly Lock _lock = new();
    private readonly HashSet<ServiceKind> _activeServices = [];

    public void SetMicrophoneActive(bool active) =>
        SetServiceState(ServiceKind.Microphone, active);

    public void SetMediaProjectionActive(bool active) =>
        SetServiceState(ServiceKind.MediaProjection, active);

    public void SetReceiverActive(bool active) =>
        SetServiceState(ServiceKind.Receiver, active);

    private void SetServiceState(ServiceKind serviceKind, bool active)
    {
        lock (_lock)
        {
            var hadActiveServices = _activeServices.Count > 0;

            if (active)
                _activeServices.Add(serviceKind);
            else
                _activeServices.Remove(serviceKind);

            var hasActiveServices = _activeServices.Count > 0;
            if (hadActiveServices == hasActiveServices)
                return;

            if (hasActiveServices)
            {
                wakeLockService.Acquire();
                wifiLockService.Acquire();
            }
            else
            {
                wifiLockService.Release();
                wakeLockService.Release();
            }
        }
    }

    private enum ServiceKind
    {
        Microphone,
        MediaProjection,
        Receiver
    }
}
