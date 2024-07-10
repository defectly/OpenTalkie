using Android.App;
using Android.Content;
using Android.OS;
using AndroidX.Core.App;

namespace vOpenTalkie;

[Service(ForegroundServiceType = Android.Content.PM.ForegroundService.TypeMicrophone)]
internal partial class ForegroundMicrophoneService : Service
{
    private string NOTIFICATION_CHANNEL_ID = "1000";
    private int NOTIFICATION_ID = 1;
    private string NOTIFICATION_CHANNEL_NAME = "notification";

    private void startForegroundService()
    {
        var notifcationManager = GetSystemService(NotificationService) as NotificationManager;

        if (Build.VERSION.SdkInt >= BuildVersionCodes.O)
            createNotificationChannel(notifcationManager);

        var notification = new NotificationCompat.Builder(this, NOTIFICATION_CHANNEL_ID);
        notification.SetAutoCancel(false);
        notification.SetOngoing(true);
        notification.SetSmallIcon(Resource.Mipmap.appicon);
        notification.SetContentTitle("Open Talkie");
        notification.SetContentText("Open Talkie Service is running");

        StartForeground(NOTIFICATION_ID, notification.Build(), Android.Content.PM.ForegroundService.TypeMicrophone);
    }

    private void createNotificationChannel(NotificationManager notificationMnaManager)
    {
        var channel = new NotificationChannel(NOTIFICATION_CHANNEL_ID, NOTIFICATION_CHANNEL_NAME,
        NotificationImportance.Low);
        notificationMnaManager.CreateNotificationChannel(channel);
    }

    public override IBinder OnBind(Intent intent) => null;

    public override StartCommandResult OnStartCommand(Intent intent, StartCommandFlags flags, int startId)
    {
        startForegroundService();
        return StartCommandResult.NotSticky;
    }
}
