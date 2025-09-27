using Android.Content;

namespace OpenTalkie.Platforms.Android.Common.Services.Receiver;

internal static class ReceiverForegroundServiceManager
{
    private static string NotificationContentTitle { get; set; } = "Receiving audio in progress...";
    private static string NotificationContentText { get; set; } = "Audio receiver is currently active";

    public static void StartForegroundService()
    {
        var context = Platform.AppContext;

        Intent notificationSetup = new(context, typeof(ReceiverForegroundService));
        notificationSetup.PutExtra(ReceiverForegroundService.ExtraCommandNotificationSetup, true);
        notificationSetup.PutExtra(ReceiverForegroundService.ExtraContentTitle, NotificationContentTitle);
        notificationSetup.PutExtra(ReceiverForegroundService.ExtraContentText, NotificationContentText);

        if (OperatingSystem.IsAndroidVersionAtLeast(26))
            context.StartForegroundService(notificationSetup);
        else
            context.StartService(notificationSetup);

        ForegroundServiceWatcher.NotifyServiceState(nameof(ReceiverForegroundService), true);
    }

    public static void StopForegroundService()
    {
        var context = Platform.AppContext;
        context.StopService(new Intent(context, typeof(ReceiverForegroundService)));
        ForegroundServiceWatcher.NotifyServiceState(nameof(ReceiverForegroundService), false);
    }
}

