using Android.App;
using Android.Content;
using Android.Media.Projection;
using OpenTalkie.Platforms.Android.Common.ForegroundServices;

namespace OpenTalkie.Platforms.Android;

public class MediaProjectionProvider
{
    private MediaProjectionManager? _mediaProjectionManager;
    private const int MediaProjectionRequestCode = 101;
    private TaskCompletionSource<bool>? _permissionTaskCompletionSource;
    private MediaProjection? _mediaProjection;
    private readonly MediaProjectionForegroundService _foregroundService = new();

    public MediaProjectionProvider(Activity activity)
    {
        _mediaProjectionManager = (MediaProjectionManager)activity.GetSystemService(Context.MediaProjectionService)!
            ?? throw new NullReferenceException($"No media projection manager provided");
    }

    public async Task<bool> RequestMediaProjectionPermissionAsync()
    {
        if (_mediaProjection != null)
            return true;

        if (Platform.CurrentActivity == null)
            return false;

        _permissionTaskCompletionSource = new TaskCompletionSource<bool>();

        _foregroundService.Start();

        var intent = _mediaProjectionManager.CreateScreenCaptureIntent();

        Platform.CurrentActivity.StartActivityForResult(intent, MediaProjectionRequestCode);

        if (await _permissionTaskCompletionSource.Task)
        {
            return true;
        }
        else
        {
            _foregroundService.Stop();
            return false;
        }
    }

    public MediaProjection? GetMediaProjection()
    {
        return _mediaProjection;
    }

    public void DisposeMediaProjection()
    {
        _mediaProjection?.Stop();
        _mediaProjection = null;
        _foregroundService.Stop();
    }

    public void OnActivityResult(int requestCode, Result resultCode, Intent data)
    {
        if (requestCode == MediaProjectionRequestCode)
        {
            if (resultCode == Result.Ok)
            {
                _mediaProjection = _mediaProjectionManager.GetMediaProjection((int)Result.Ok, data);
                _permissionTaskCompletionSource?.TrySetResult(true);
            }
            else
            {
                _foregroundService.Stop();
                _permissionTaskCompletionSource?.TrySetResult(false);
            }
        }
    }
}