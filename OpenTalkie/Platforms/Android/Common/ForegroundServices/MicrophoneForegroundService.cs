using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using AndroidX.Core.App;

namespace OpenTalkie.Platforms.Android.Common.ForegroundServices;

[Service(ForegroundServiceType = ForegroundService.TypeMicrophone)]
internal class MicrophoneForegroundService : Service
{
    private readonly string NOTIFICATION_CHANNEL_ID = "1000";
    private readonly int NOTIFICATION_ID = 1;
    private readonly string NOTIFICATION_CHANNEL_NAME = "microphone_notification";

    private readonly Intent _intent = new(Platform.AppContext, typeof(MicrophoneForegroundService));

    public void Start() =>
        Platform.AppContext.StartForegroundService(_intent);

    public void Stop() =>
        Platform.AppContext.StopService(_intent);

    private void StartForegroundService()
    {
        var openAppIntent = new Intent(this, typeof(MainActivity));
        openAppIntent.SetFlags(ActivityFlags.ClearTop | ActivityFlags.NewTask);
        var pendingIntent = PendingIntent.GetActivity(this, 0, openAppIntent, PendingIntentFlags.UpdateCurrent | PendingIntentFlags.Immutable);

        if (OperatingSystem.IsAndroidVersionAtLeast(26))
        {
            if (GetSystemService(NotificationService) is not NotificationManager notificationManager)
                throw new NullReferenceException("Can't get notification manager");

            CreateNotificationChannel(notificationManager);
        }

        var notification = new NotificationCompat.Builder(this, NOTIFICATION_CHANNEL_ID);
        notification.SetAutoCancel(false);
        notification.SetOngoing(true);
        notification.SetSmallIcon(Resource.Mipmap.appicon);
        notification.SetContentTitle("Open Talkie");
        notification.SetContentText("Microphone capture service is running");
        notification.SetContentIntent(pendingIntent);

        if (OperatingSystem.IsAndroidVersionAtLeast(30))
            StartForeground(NOTIFICATION_ID, notification.Build(), ForegroundService.TypeMicrophone);
        else
            StartForeground(NOTIFICATION_ID, notification.Build());
    }

    private void CreateNotificationChannel(NotificationManager notificationMnaManager)
    {
        var channel = new NotificationChannel(NOTIFICATION_CHANNEL_ID, NOTIFICATION_CHANNEL_NAME,
        NotificationImportance.Low);
        notificationMnaManager.CreateNotificationChannel(channel);
    }

    public override IBinder? OnBind(Intent? intent) => null;

    public override StartCommandResult OnStartCommand(Intent? intent, StartCommandFlags flags, int startId)
    {
        StartForegroundService();
        return StartCommandResult.NotSticky;
    }
}
