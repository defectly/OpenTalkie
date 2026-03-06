using Android.App;
using Android.Runtime;
using Java.Lang;
using Microsoft.Maui;
using Microsoft.Maui.Hosting;
using System.Diagnostics;

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
            Debug.WriteLine($"Failed to load RNNoise native library: {ex.Message}");
        }
        catch (System.Exception ex)
        {
            Debug.WriteLine($"Unexpected RNNoise load failure: {ex.Message}");
        }
    }
}
