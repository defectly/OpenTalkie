using Android.Media.Projection;
using AndroidX.Activity.Result;

namespace OpenTalkie.Platforms.Android;

public class SystemAudioCaptureCallback : Java.Lang.Object, IActivityResultCallback
{
    public static MediaProjectionManager? MediaProjectionManager;
    public static MediaProjection MediaProjection;
    public Action<bool> Running;

    public int ResultCode = 100;


    public SystemAudioCaptureCallback(MediaProjectionManager? mediaProjectionManager)
    {
        MediaProjectionManager = mediaProjectionManager;
    }
    public void OnActivityResult(Java.Lang.Object? result)
    {
        ActivityResult activityResult = result as ActivityResult;
        if (activityResult.ResultCode == -1)
        {
            ResultCode = activityResult.ResultCode;

            MediaProjection = MediaProjectionManager.GetMediaProjection(activityResult.ResultCode, activityResult.Data);
            Running?.Invoke(true);
        }
        else
        {
            Running?.Invoke(false);
        }
    }
}