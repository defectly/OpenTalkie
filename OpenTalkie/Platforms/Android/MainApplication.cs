using Android.App;
using Android.Runtime;
using OpenTalkie;

namespace OpenTalkie.Platforms.Android;

[Application]
public class MainApplication(nint handle, JniHandleOwnership ownership) : MauiApplication(handle, ownership)
{
    protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();
}
