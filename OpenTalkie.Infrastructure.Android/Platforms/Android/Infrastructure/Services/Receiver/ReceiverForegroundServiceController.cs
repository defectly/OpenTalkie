using OpenTalkie.Application.Abstractions.Services;

namespace OpenTalkie.Infrastructure.Android.Platforms.Android.Infrastructure.Services.Receiver;

public class ReceiverForegroundServiceController : IReceiverForegroundServiceController
{
    public void Start()
    {
        ReceiverForegroundServiceManager.StartForegroundService();
    }

    public void Stop()
    {
        ReceiverForegroundServiceManager.StopForegroundService();
    }
}


