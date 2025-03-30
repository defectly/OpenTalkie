﻿using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using AndroidX.Core.App;

namespace OpenTalkie.Platforms.Android.Common.ForegroundServices;

[Service(ForegroundServiceType = ForegroundService.TypeMediaProjection)]
public class MediaProjectionForegroundService : Service
{
    private readonly string NOTIFICATION_CHANNEL_ID = "1001";
    private readonly int NOTIFICATION_ID = 2;
    private readonly string NOTIFICATION_CHANNEL_NAME = "system_audio_notification";

    private Intent _intent = new(Platform.AppContext, typeof(MediaProjectionForegroundService));

    public void Start() =>
        Platform.AppContext.StartForegroundService(_intent);

    public void Stop() =>
        Platform.AppContext.StopService(_intent);

    private void StartForegroundService()
    {
        var openAppIntent = new Intent(this, typeof(MainActivity));
        openAppIntent.SetFlags(ActivityFlags.ClearTop | ActivityFlags.NewTask);
        var pendingIntent = PendingIntent.GetActivity(this, 0, openAppIntent, PendingIntentFlags.UpdateCurrent | PendingIntentFlags.Immutable);

        var notifcationManager = GetSystemService(NotificationService) as NotificationManager;

        if (OperatingSystem.IsAndroidVersionAtLeast(26))
            CreateNotificationChannel(notifcationManager);

        var notification = new NotificationCompat.Builder(this, NOTIFICATION_CHANNEL_ID);
        notification.SetAutoCancel(false);
        notification.SetOngoing(true);
        notification.SetSmallIcon(Resource.Mipmap.appicon);
        notification.SetContentTitle("Open Talkie");
        notification.SetContentText("System audio capture service is running");
        notification.SetContentIntent(pendingIntent);

        if (OperatingSystem.IsAndroidVersionAtLeast(29))
            StartForeground(NOTIFICATION_ID, notification.Build(), ForegroundService.TypeMediaProjection);
        else
            StartForeground(NOTIFICATION_ID, notification.Build());
    }

    private void CreateNotificationChannel(NotificationManager notificationMnaManager)
    {
        var channel = new NotificationChannel(NOTIFICATION_CHANNEL_ID, NOTIFICATION_CHANNEL_NAME,
        NotificationImportance.Low);
        notificationMnaManager.CreateNotificationChannel(channel);
    }

    public override IBinder OnBind(Intent? intent) => null;

    public override StartCommandResult OnStartCommand(Intent? intent, StartCommandFlags flags, int startId)
    {
        StartForegroundService();
        return StartCommandResult.NotSticky;
    }
}
