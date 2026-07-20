using Android.Content;
using Android.Net;
using Android.Net.Wifi;
using OpenTalkie.Application.Abstractions.Services;

namespace OpenTalkie.Infrastructure.Android.Platforms.Android.Infrastructure.Services;

public sealed class WifiLockService : Java.Lang.Object, IWifiLockService
{
    private const string Tag = "OpenTalkieWifiLock";

    private readonly Lock _lock = new();
    private WifiManager.WifiLock? _wifiLock;

    public void Acquire()
    {
        lock (_lock)
        {
            if (_wifiLock?.IsHeld == true)
                return;

            try
            {
                if (Platform.AppContext.GetSystemService(Context.WifiService) is not WifiManager wifiManager)
                    return;

                _wifiLock?.Dispose();
                var wifiLock = wifiManager.CreateWifiLock(WifiMode.FullHighPerf, Tag);
                if (wifiLock is null)
                    return;

                wifiLock.SetReferenceCounted(false);
                _wifiLock = wifiLock;
                wifiLock.Acquire();
            }
            catch (Java.Lang.Exception ex)
            {
                _wifiLock?.Dispose();
                _wifiLock = null;
                System.Diagnostics.Debug.WriteLine($"WifiLock acquire error: {ex.Message}");
            }
        }
    }

    public void Release()
    {
        lock (_lock)
        {
            try
            {
                if (_wifiLock?.IsHeld == true)
                    _wifiLock.Release();
            }
            catch (Java.Lang.Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"WifiLock release error: {ex.Message}");
            }
            finally
            {
                _wifiLock?.Dispose();
                _wifiLock = null;
            }
        }
    }
}
