using OpenTalkie.Application.Abstractions.Services;

namespace OpenTalkie.Infrastructure.Android.Platforms.Android.Infrastructure.Services.Receiver;

public class ReceiverForegroundServiceController : IReceiverForegroundServiceController
{
    private readonly ILogger<ReceiverForegroundServiceController> _logger;

    public ReceiverForegroundServiceController(ILogger<ReceiverForegroundServiceController> logger)
    {
        _logger = logger;
    }

    public void Start()
    {
        _logger.LogInformation("Starting receiver foreground service.");
        ReceiverForegroundServiceManager.StartForegroundService();
    }

    public void Stop()
    {
        _logger.LogInformation("Stopping receiver foreground service.");
        ReceiverForegroundServiceManager.StopForegroundService();
    }
}


