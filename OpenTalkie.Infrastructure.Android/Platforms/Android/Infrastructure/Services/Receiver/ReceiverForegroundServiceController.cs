using OpenTalkie.Application.Abstractions.Services;

namespace OpenTalkie.Infrastructure.Android.Platforms.Android.Infrastructure.Services.Receiver;

public class ReceiverForegroundServiceController(
    ForegroundServicePowerCoordinator powerCoordinator) : IReceiverForegroundServiceController
{
    public void Start()
    {
        ReceiverForegroundServiceManager.StartForegroundService();
        powerCoordinator.SetReceiverActive(true);
    }

    public void Stop()
    {
        ReceiverForegroundServiceManager.StopForegroundService();
        powerCoordinator.SetReceiverActive(false);
    }
}


