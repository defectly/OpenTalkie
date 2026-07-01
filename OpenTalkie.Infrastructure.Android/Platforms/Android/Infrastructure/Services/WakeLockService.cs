using Android.Content;
using Android.OS;
using OpenTalkie.Application.Abstractions.Services;

namespace OpenTalkie.Infrastructure.Android.Platforms.Android.Infrastructure.Services;

public class WakeLockService : Java.Lang.Object, IWakeLockService
{
    private PowerManager.WakeLock? _wakeLock;
    private const string Tag = "OpenTalkieWakeLock";
    private readonly ILogger<WakeLockService> _logger;

    public WakeLockService(ILogger<WakeLockService> logger, ILoggerFactory loggerFactory)
    {
        _logger = logger;
        ForegroundServiceWatcher.Configure(this, loggerFactory.CreateLogger<ForegroundServiceWatcherLogScope>());
    }

    public void Acquire()
    {
        if (_wakeLock != null && _wakeLock.IsHeld)
        {
            _logger.LogDebug("Wake lock acquire ignored because it is already held.");
            return;
        }

        var powerManager = Platform.CurrentActivity?.GetSystemService(Context.PowerService) as PowerManager;
        if (powerManager == null)
        {
            _logger.LogWarning("PowerManager unavailable; wake lock was not acquired.");
            return;
        }

        _wakeLock = powerManager.NewWakeLock(WakeLockFlags.Partial, Tag);
        _wakeLock?.Acquire();
        _logger.LogInformation("Wake lock acquired.");
    }

    public void Release()
    {
        try
        {
            _wakeLock?.Release();
        }
        catch (Java.Lang.Exception ex)
        {
            _logger.LogWarning(ex, "Wake lock release failed.");
        }
        finally
        {
            _wakeLock?.Dispose();
            _wakeLock = null;
            _logger.LogInformation("Wake lock released.");
        }
    }
}
