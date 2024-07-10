using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.Media.Projection;
using Android.OS;
using AndroidX.Activity.Result;
using vOpenTalkie.Platforms.Android;
using static AndroidX.Activity.Result.Contract.ActivityResultContracts;

namespace vOpenTalkie;

[Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
public class MainActivity : MauiAppCompatActivity
{
    public static MediaProjectionManager MediaProjectionManager;
    public static SystemAudioCaptureCallback SystemAudioCaptureCallback;
    public static ActivityResultLauncher MediaProjectionLauncher;

    protected override void OnCreate(Bundle savedInstanceState)
    {
        base.OnCreate(savedInstanceState);

        MediaProjectionManager = (MediaProjectionManager)GetSystemService(Context.MediaProjectionService)!;

        SystemAudioCaptureCallback = new SystemAudioCaptureCallback(MediaProjectionManager);
        MediaProjectionLauncher = RegisterForActivityResult(new StartActivityForResult(), SystemAudioCaptureCallback);

    }
}
