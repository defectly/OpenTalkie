using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.Media.Projection;
using Android.OS;
using AndroidX.Core.App;
using Intent = Android.Content.Intent;

namespace OpenTalkie.Platforms.Android.Common.ForegroundServices;

[Service(ForegroundServiceType = ForegroundService.TypeMediaProjection)]
public class MediaProjectionForegroundService : Service
{
    private const string NOTIFICATION_CHANNEL_ID = "1001";
    private const int NOTIFICATION_ID = 2;
    private const string NOTIFICATION_CHANNEL_NAME = "system_audio_notification";
    private readonly Intent _intent = new(Platform.AppContext, typeof(MediaProjectionForegroundService));

    private MediaProjection? _mediaProjection;

    public void Start()
    {
        Platform.AppContext.StartForegroundService(_intent);
    }

    public override void OnCreate()
    {
        StartForegroundService();
    }

    public void Stop()
    {
        Platform.AppContext.StopService(_intent);
    }

    private void StartForegroundService()
    {
        try
        {
            var openAppIntent = new Intent(this, typeof(MainActivity));
            openAppIntent.SetFlags(ActivityFlags.ClearTop | ActivityFlags.NewTask);
            var pendingIntent = PendingIntent.GetActivity(this, 0, openAppIntent,
                PendingIntentFlags.UpdateCurrent | PendingIntentFlags.Immutable);

            if (OperatingSystem.IsAndroidVersionAtLeast(26))
            {
                if (GetSystemService(NotificationService) is not NotificationManager notificationManager)
                    throw new NullReferenceException("Can't get notification manager");

                CreateNotificationChannel(notificationManager);
            }

            var notification = new NotificationCompat.Builder(this, NOTIFICATION_CHANNEL_ID)
                .SetAutoCancel(false)
                .SetOngoing(true)
                .SetSmallIcon(Resource.Mipmap.appicon)
                .SetContentTitle("Open Talkie")
                .SetContentText("System audio capture service is running")
                .SetContentIntent(pendingIntent)
                .Build();

            if (OperatingSystem.IsAndroidVersionAtLeast(29))
                StartForeground(NOTIFICATION_ID, notification, ForegroundService.TypeMediaProjection);
            else
                StartForeground(NOTIFICATION_ID, notification);
        }
        catch
        {
            StopSelf();
        }
    }

    private static void CreateNotificationChannel(NotificationManager notificationManager)
    {
        var channel = new NotificationChannel(NOTIFICATION_CHANNEL_ID, NOTIFICATION_CHANNEL_NAME,
            NotificationImportance.Low);
        notificationManager.CreateNotificationChannel(channel);
    }

    public override IBinder? OnBind(Intent? intent) => null;

    public override StartCommandResult OnStartCommand(Intent? intent, StartCommandFlags flags, int startId)
    {
        StartForegroundService();
        return StartCommandResult.Sticky;
    }

    public override void OnDestroy()
    {
        _mediaProjection?.Stop();
        _mediaProjection = null;
        StopForeground(StopForegroundFlags.Remove);
        base.OnDestroy();
    }
}