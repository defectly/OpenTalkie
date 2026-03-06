using Android.Content;
using Android.OS;
using OpenTalkie.Application.Abstractions.Services;

namespace OpenTalkie.Infrastructure.Android.Platforms.Android.Infrastructure.Services;

public class WakeLockService : Java.Lang.Object, IWakeLockService
{
    private PowerManager.WakeLock? _wakeLock;
    private const string Tag = "OpenTalkieWakeLock";

    public WakeLockService()
    {
        ForegroundServiceWatcher.Configure(this);
    }

    public void Acquire()
    {
        if (_wakeLock != null && _wakeLock.IsHeld) return;

        var powerManager = Platform.CurrentActivity?.GetSystemService(Context.PowerService) as PowerManager;
        if (powerManager == null) return;

        _wakeLock = powerManager.NewWakeLock(WakeLockFlags.Partial, Tag);
        _wakeLock?.Acquire();
    }

    public void Release()
    {
        try
        {
            _wakeLock?.Release();
        }
        catch (Java.Lang.Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"WakeLock release error: {ex.Message}");
        }
        finally
        {
            _wakeLock?.Dispose();
            _wakeLock = null;
        }
    }
}
