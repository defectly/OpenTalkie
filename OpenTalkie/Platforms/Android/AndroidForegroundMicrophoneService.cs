using Android.App;
using Android.Content;
using Android.OS;
using AndroidX.Core.App;

namespace OpenTalkie;

[Service(ForegroundServiceType = Android.Content.PM.ForegroundService.TypeMicrophone)]
internal class AndroidForegroundMicrophoneService : Service
{
    private readonly string NOTIFICATION_CHANNEL_ID = "1000";
    private readonly int NOTIFICATION_ID = 1;
    private readonly string NOTIFICATION_CHANNEL_NAME = "microphone_notification";

    private Intent _intent = new(Android.App.Application.Context, typeof(AndroidForegroundMicrophoneService));

    public void Start() =>
        Android.App.Application.Context.StartForegroundService(_intent);

    public void Stop() =>
        Android.App.Application.Context.StopService(_intent);

    private void StartForegroundService()
    {
        var notifcationManager = GetSystemService(NotificationService) as NotificationManager;

        if (OperatingSystem.IsAndroidVersionAtLeast(26))
            CreateNotificationChannel(notifcationManager);

        var notification = new NotificationCompat.Builder(this, NOTIFICATION_CHANNEL_ID);
        notification.SetAutoCancel(false);
        notification.SetOngoing(true);
        notification.SetSmallIcon(Resource.Mipmap.appicon);
        notification.SetContentTitle("Open Talkie");
        notification.SetContentText("Microphone capture service is running");

        if (OperatingSystem.IsAndroidVersionAtLeast(30))
            StartForeground(NOTIFICATION_ID, notification.Build(), Android.Content.PM.ForegroundService.TypeMicrophone);
        else
            StartForeground(NOTIFICATION_ID, notification.Build());
    }

    private void CreateNotificationChannel(NotificationManager notificationMnaManager)
    {
        var channel = new NotificationChannel(NOTIFICATION_CHANNEL_ID, NOTIFICATION_CHANNEL_NAME,
        NotificationImportance.Low);
        notificationMnaManager.CreateNotificationChannel(channel);
    }

    public override IBinder OnBind(Intent intent) => null;

    public override StartCommandResult OnStartCommand(Intent intent, StartCommandFlags flags, int startId)
    {
        StartForegroundService();
        return StartCommandResult.NotSticky;
    }
}
