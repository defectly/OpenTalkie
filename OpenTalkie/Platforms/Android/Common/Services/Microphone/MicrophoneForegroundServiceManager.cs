using Android.Content;
using Android.OS;

namespace OpenTalkie.Platforms.Android.Common.Services.Microphone;

internal static class MicrophoneForegroundServiceManager
{
    private static TaskCompletionSource<bool>? serviceStartAwaiter;

    private static string NotificationContentTitle { get; set; } = "Microphone capturing in progress...";

    private static string NotificationContentText { get; set; } = "A Microphone capturing is currently in progress, be careful with any sensitive information.";

    public static async Task StartForegroundServiceAsync(ScreenAudioCapturingOptions? options = null)
    {
        serviceStartAwaiter = new TaskCompletionSource<bool>();

        var context = Platform.AppContext;
        var messenger = new Messenger(new MicrophoneHandler(serviceStartAwaiter));

        Intent notificationSetup = new(context, typeof(MicrophoneForegroundService));
        notificationSetup.PutExtra(MicrophoneForegroundService.ExtraCommandNotificationSetup, true);
        notificationSetup.PutExtra(MicrophoneForegroundService.ExtraExternalMessenger, messenger);
        notificationSetup.PutExtra(MicrophoneForegroundService.ExtraContentTitle, NotificationContentTitle);
        notificationSetup.PutExtra(MicrophoneForegroundService.ExtraContentText, NotificationContentText);

        // Android O
        if (OperatingSystem.IsAndroidVersionAtLeast(26))
            context.StartForegroundService(notificationSetup);
        else
            context.StartService(notificationSetup);

        await serviceStartAwaiter.Task;

        Intent beginRecording = new(context, typeof(MicrophoneForegroundService));
        beginRecording.PutExtra(MicrophoneForegroundService.ExtraCommandBeginRecording, true);

        // Android O
        if (OperatingSystem.IsAndroidVersionAtLeast(26))
            context.StartForegroundService(beginRecording);
        else
            context.StartService(beginRecording);
    }

    internal static void StopForegroundService()
    {
        var context = Platform.AppContext;
        context.StopService(new Intent(context, typeof(MicrophoneForegroundService)));
    }
}

internal class MicrophoneHandler(TaskCompletionSource<bool> tcs) : Handler
{
    private readonly TaskCompletionSource<bool> _tcs = tcs;

    public override void HandleMessage(Message msg)
    {
        if (msg.What == MicrophoneForegroundService.MsgServiceStarted)
            _tcs.TrySetResult(true);
    }
}