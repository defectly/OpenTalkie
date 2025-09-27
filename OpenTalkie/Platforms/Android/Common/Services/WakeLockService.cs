using Android.Content;
using Android.OS;
using OpenTalkie.Common.Services.Interfaces;

namespace OpenTalkie.Platforms.Android.Common.Services;

public class WakeLockService : Java.Lang.Object, IWakeLockService
{
    private PowerManager.WakeLock? _wakeLock;
    private const string Tag = "OpenTalkieWakeLock";

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
            // Log error if needed
            System.Diagnostics.Debug.WriteLine($"WakeLock release error: {ex.Message}");
        }
        finally
        {
            _wakeLock?.Dispose();
            _wakeLock = null;
        }
    }
}