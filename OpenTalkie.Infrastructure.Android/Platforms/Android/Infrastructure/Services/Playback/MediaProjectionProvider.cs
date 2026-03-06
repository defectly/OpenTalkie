using Android.App;
using Android.Content;
using Android.Media.Projection;
using Android.OS;
using OpenTalkie.Application.Abstractions.Services;
using OpenTalkie.Application.Models;

namespace OpenTalkie.Infrastructure.Android.Platforms.Android.Infrastructure.Services.Playback;

public sealed partial class MediaProjectionProvider : MediaProjection.Callback, IScreenAudioCapturing
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

    public Task<bool> RequestCaptureAsync(ScreenAudioCapturingOptions? options = null)
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

    public void StopCapture()
    {
        MediaProjection?.Stop();

        var context = Platform.AppContext;
        context.StopService(new Intent(context, typeof(MediaProjectionForegroundService)));
        ForegroundServiceWatcher.NotifyServiceState(nameof(MediaProjectionForegroundService), false);
    }

    internal MediaProjection? GetActiveProjection() => MediaProjection;

    public void OnScreenCapturePermissionDenied()
    {
        recordingStartAwaiter?.TrySetResult(false);
    }

    public async void OnScreenCapturePermissionGranted(int resultCode, Intent? data)
    {
        serviceStartAwaiter = new TaskCompletionSource<bool>();

        var context = Platform.AppContext;
        var messenger = new Messenger(new ExternalHandler(serviceStartAwaiter));

        Intent notificationSetup = new(context, typeof(MediaProjectionForegroundService));
        notificationSetup.PutExtra(MediaProjectionForegroundService.ExtraCommandNotificationSetup, true);
        notificationSetup.PutExtra(MediaProjectionForegroundService.ExtraExternalMessenger, messenger);
        notificationSetup.PutExtra(MediaProjectionForegroundService.ExtraContentTitle, NotificationContentTitle);
        notificationSetup.PutExtra(MediaProjectionForegroundService.ExtraContentText, NotificationContentText);

        if (OperatingSystem.IsAndroidVersionAtLeast(26))
            context.StartForegroundService(notificationSetup);
        else
            context.StartService(notificationSetup);

        await serviceStartAwaiter.Task;

        MediaProjection = ProjectionManager?.GetMediaProjection(resultCode, data!);
        MediaProjection?.RegisterCallback(this, null);

        Intent beginRecording = new(context, typeof(MediaProjectionForegroundService));
        beginRecording.PutExtra(MediaProjectionForegroundService.ExtraCommandBeginRecording, true);

        if (OperatingSystem.IsAndroidVersionAtLeast(26))
            context.StartForegroundService(beginRecording);
        else
            context.StartService(beginRecording);

        ForegroundServiceWatcher.NotifyServiceState(nameof(MediaProjectionForegroundService), true);

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
}

internal sealed class ExternalHandler(TaskCompletionSource<bool> tcs) : Handler
{
    private readonly TaskCompletionSource<bool> _tcs = tcs;

    public override void HandleMessage(Message msg)
    {
        if (msg.What == MediaProjectionForegroundService.MsgServiceStarted)
            _tcs.TrySetResult(true);
    }
}
