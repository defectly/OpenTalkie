using Android.Content;
using Android.OS;
using OpenTalkie.Application.Abstractions.Services;

namespace OpenTalkie.Infrastructure.Android.Platforms.Android.Infrastructure.Services;

public class WakeLockService : Java.Lang.Object, IWakeLockService
{
    private readonly Lock _lock = new();
    private PowerManager.WakeLock? _wakeLock;
    private const string Tag = "OpenTalkieWakeLock";

    public void Acquire()
    {
        lock (_lock)
        {
            if (_wakeLock?.IsHeld == true)
                return;

            if (Platform.AppContext.GetSystemService(Context.PowerService) is not PowerManager powerManager)
                return;

            _wakeLock?.Dispose();
            var wakeLock = powerManager.NewWakeLock(WakeLockFlags.Partial, Tag);
            if (wakeLock is null)
                return;

            _wakeLock = wakeLock;
            wakeLock.Acquire();
        }
    }

    public void Release()
    {
        lock (_lock)
        {
            try
            {
                if (_wakeLock?.IsHeld == true)
                    _wakeLock.Release();
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
}
