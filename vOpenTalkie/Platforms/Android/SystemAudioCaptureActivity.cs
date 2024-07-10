using Android.Content;
using Android.Media.Projection;
using AndroidX.Activity.Result;
using AndroidX.AppCompat.App;
using static AndroidX.Activity.Result.Contract.ActivityResultContracts;

namespace vOpenTalkie.Platforms.Android;

internal class SystemAudioCaptureActivity : AppCompatActivity
{
    private MediaProjection _mediaProjection;
    private MediaProjectionManager _mediaProjectionManager;
    private SystemAudioCaptureCallback _systemAudioCaptureCallback;
    private ActivityResultLauncher _mediaProjectionLauncher;
    private Intent _mediaProjectionIntent;

    protected override void OnStart()
    {
        base.OnStart();

        _mediaProjectionManager = (MediaProjectionManager)GetSystemService(Context.MediaProjectionService)!;

        _systemAudioCaptureCallback = new SystemAudioCaptureCallback(_mediaProjectionManager);
        _mediaProjectionLauncher = RegisterForActivityResult(new StartActivityForResult(), _systemAudioCaptureCallback);
        
        _mediaProjectionIntent = _mediaProjectionManager.CreateScreenCaptureIntent();
        _mediaProjectionLauncher.Launch(_mediaProjectionIntent);
    }

    protected override void OnStop()
    {
        base.OnStop();

        _mediaProjection.Stop();
        _mediaProjection.Dispose();
        _mediaProjectionManager.Dispose();
        _systemAudioCaptureCallback.Dispose();
        _mediaProjectionLauncher.Unregister();
        _mediaProjectionLauncher.Dispose();
        _mediaProjectionIntent.Dispose();
    }
}
