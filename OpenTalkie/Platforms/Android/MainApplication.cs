using Android.App;
using Android.Runtime;
using Android.Util;
using Java.Lang;

namespace OpenTalkie.Platforms.Android;

[Application]
public class MainApplication(nint handle, JniHandleOwnership ownership) : MauiApplication(handle, ownership)
{
    public override void OnCreate()
    {
        base.OnCreate();

        TryLoadRnnoise();
    }

    protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();

    private static void TryLoadRnnoise()
    {
        try
        {
            JavaSystem.LoadLibrary("rnnoise");
        }
        catch (UnsatisfiedLinkError ex)
        {
            LogWarning("Failed to load RNNoise native library.", ex);
        }
        catch (System.Exception ex)
        {
            LogWarning("Unexpected RNNoise load failure.", ex);
        }
    }

    private static void LogWarning(string message, System.Exception exception)
    {
        Log.Warn("OpenTalkie", $"{message}{Environment.NewLine}{exception}");
    }
}
