using Android.App;
using Android.Content;
using Android.Media.Projection;
using Android.OS;

namespace OpenTalkie.Platforms.Android.Common.Services.Playback;

public partial class MediaProjectionProvider : MediaProjection.Callback, IScreenAudioCapturing
{
    public const int RequestMediaProjectionCode = (int)Result.FirstUser + 1;

    private TaskCompletionSource<bool>? serviceStartAwaiter;
    private TaskCompletionSource<bool>? recordingStartAwaiter;

    private string NotificationContentTitle { get; set; } =
        ScreenAudioCapturingOptions.defaultAndroidNotificationTitle;

    private string NotificationContentText { get; set; } =
        ScreenAudioCapturingOptions.defaultAndroidNotificationText;

    private MediaProjectionManager? ProjectionManager { get; set; }
    private MediaProjection? MediaProjection { get; set; }

    public bool IsSupported => ProjectionManager is not null;

    public MediaProjectionProvider()
    {
        ProjectionManager = (MediaProjectionManager?)Platform.AppContext.GetSystemService(Context.MediaProjectionService);
    }

    public Task<bool> StartRecording(ScreenAudioCapturingOptions? options = null)
    {
        if (!IsSupported)
            throw new NotSupportedException("Screen audio capturing not supported on this device.");

        if (!string.IsNullOrWhiteSpace(options?.NotificationContentTitle))
            NotificationContentTitle = options.NotificationContentTitle;

        if (!string.IsNullOrWhiteSpace(options?.NotificationContentText))
            NotificationContentText = options.NotificationContentText;

        recordingStartAwaiter = new TaskCompletionSource<bool>();

        Setup();

        return recordingStartAwaiter.Task;
    }

    public void StopRecording()
    {
        MediaProjection?.Stop();

        var context = Platform.AppContext;
        context.StopService(new Intent(context, typeof(MediaProjectionForegroundService)));
    }

    internal void OnScreenCapturePermissionDenied()
    {
        recordingStartAwaiter?.TrySetResult(false);
    }

    internal async void OnScreenCapturePermissionGranted(int resultCode, Intent? data)
    {
        serviceStartAwaiter = new TaskCompletionSource<bool>();

        var context = Platform.AppContext;
        var messenger = new Messenger(new ExternalHandler(serviceStartAwaiter));

        Intent notificationSetup = new(context, typeof(MediaProjectionForegroundService));
        notificationSetup.PutExtra(MediaProjectionForegroundService.ExtraCommandNotificationSetup, true);
        notificationSetup.PutExtra(MediaProjectionForegroundService.ExtraExternalMessenger, messenger);
        notificationSetup.PutExtra(MediaProjectionForegroundService.ExtraContentTitle, NotificationContentTitle);
        notificationSetup.PutExtra(MediaProjectionForegroundService.ExtraContentText, NotificationContentText);

        // Android O
        if (OperatingSystem.IsAndroidVersionAtLeast(26))
            context.StartForegroundService(notificationSetup);
        else
            context.StartService(notificationSetup);

        await serviceStartAwaiter.Task;

        // Prepare MediaProjection which will be later be used by the MediaProjectionForegroundService
        // and call the BeginRecording()
        MediaProjection = ProjectionManager?.GetMediaProjection(resultCode, data!);
        MediaProjection?.RegisterCallback(this, null);

        Intent beginRecording = new(context, typeof(MediaProjectionForegroundService));
        beginRecording.PutExtra(MediaProjectionForegroundService.ExtraCommandBeginRecording, true);

        // Android O
        if (OperatingSystem.IsAndroidVersionAtLeast(26))
            context.StartForegroundService(beginRecording);
        else
            context.StartService(beginRecording);

        recordingStartAwaiter?.TrySetResult(true);
    }

    public void Setup()
    {
        if (ProjectionManager is not null)
        {
            Intent captureIntent = ProjectionManager.CreateScreenCaptureIntent();
            Platform.CurrentActivity?.StartActivityForResult(captureIntent, RequestMediaProjectionCode);
        }
    }

    public MediaProjection? GetMediaProjection()
    {
        return MediaProjection;
    }
}
internal class ExternalHandler(TaskCompletionSource<bool> tcs) : Handler
{
    private readonly TaskCompletionSource<bool> _tcs = tcs;

    public override void HandleMessage(Message msg)
    {
        if (msg.What == MediaProjectionForegroundService.MsgServiceStarted)
            _tcs.TrySetResult(true);
    }
}