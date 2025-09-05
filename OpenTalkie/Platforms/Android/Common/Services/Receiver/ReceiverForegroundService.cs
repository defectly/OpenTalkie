using Android;
using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using Android.Util;

namespace OpenTalkie.Platforms.Android.Common.Services.Receiver;

[Service(Permission = Manifest.Permission.ForegroundServiceMediaPlayback,
    ForegroundServiceType = ForegroundService.TypeMediaPlayback)]
internal class ReceiverForegroundService : Service
{
    public const int NotificationId = 1339;
    public const string ChannelId = "ReceiverService";
    public const string ExtraExternalMessenger = "ExternalMessenger";
    public const string ExtraContentTitle = "Receiving audio";
    public const string ExtraContentText = "service is running";
    public const string ExtraCommandNotificationSetup = "SetupNotification";

    public const int MsgServiceStarted = 1;

    public override IBinder? OnBind(Intent? intent) => null;

    public override StartCommandResult OnStartCommand(Intent? intent, StartCommandFlags flags, int startId)
    {
        NotifyHandler(intent);

        if (intent?.GetBooleanExtra(ExtraCommandNotificationSetup, false) == true)
            SetupForegroundNotification(intent);

        return StartCommandResult.Sticky;
    }

    private static void NotifyHandler(Intent? intent)
    {
        if (GetParcelableExtra<Messenger>(intent, ExtraExternalMessenger) is Messenger messenger)
        {
            try
            {
                var msg = Message.Obtain(null, MsgServiceStarted);
                messenger.Send(msg);
            }
            catch (RemoteException ex)
            {
                Log.Error(ChannelId, "Failed to send message to activity: " + ex);
            }
        }
    }

    private void SetupForegroundNotification(Intent? intent)
    {
        if (string.IsNullOrEmpty(PackageName) || PackageManager == null)
        {
            Log.Error(ChannelId, "PackageName or PackageManager is null, cannot create launch intent.");
            return;
        }

        var notificationIntent = PackageManager.GetLaunchIntentForPackage(PackageName);
        if (notificationIntent == null)
        {
            Log.Error(ChannelId, "Failed to get launch intent for package: " + PackageName);
            return;
        }
        notificationIntent.SetFlags(ActivityFlags.SingleTop | ActivityFlags.ClearTop);

        PendingIntentFlags pendingIntentFlags = PendingIntentFlags.UpdateCurrent;
        if (OperatingSystem.IsAndroidVersionAtLeast(31)) // Android 12+
            pendingIntentFlags |= PendingIntentFlags.Mutable;

        PendingIntent pendingIntent = PendingIntent.GetActivity(this, 0, notificationIntent, pendingIntentFlags);

        if (OperatingSystem.IsAndroidVersionAtLeast(26))
        {
            CreateNotificationChannel();

            var contentTitle = intent?.GetStringExtra(ExtraContentTitle) ?? "Receiving audio";
            var contentText = intent?.GetStringExtra(ExtraContentText) ?? "Receiver is running";

            var notification = new Notification.Builder(this, ChannelId)
                .SetContentTitle(contentTitle)
                .SetContentText(contentText)
                .SetSmallIcon(global::Android.Resource.Drawable.IcMediaPlay)
                .SetContentIntent(pendingIntent)
                .SetOngoing(true)
                .SetAutoCancel(false)
                .Build();

            // Use typed overload when available on this API level
            try
            {
                if (OperatingSystem.IsAndroidVersionAtLeast(29))
                    StartForeground(NotificationId, notification, ForegroundService.TypeMediaPlayback);
                else
                    StartForeground(NotificationId, notification);
            }
            catch
            {
                StartForeground(NotificationId, notification);
            }
        }
        else
        {
            StartForeground(NotificationId, CreateFallbackNotification(pendingIntent));
        }
    }

    private void CreateNotificationChannel()
    {
        if (!OperatingSystem.IsAndroidVersionAtLeast(26))
            return;

        var channel = new NotificationChannel(ChannelId, "Receiver service", NotificationImportance.Default)
        {
            Description = "Notification channel for audio receiving service"
        };

        var notificationManager = GetSystemService(NotificationService) as NotificationManager;
        notificationManager?.CreateNotificationChannel(channel);
    }

    private Notification CreateFallbackNotification(PendingIntent? pendingIntent = null)
    {
        if (!OperatingSystem.IsAndroidVersionAtLeast(26))
        {
            var builder = new Notification.Builder(this)
                .SetContentTitle("Receiving audio")
                .SetContentText("Receiver is running.")
                .SetSmallIcon(global::Android.Resource.Drawable.IcMediaPlay);

            if (pendingIntent != null)
            {
                builder.SetContentIntent(pendingIntent);
                builder.SetOngoing(true).SetAutoCancel(false);
            }

            return builder.Build();
        }

        return new Notification();
    }

    static T? GetParcelableExtra<T>(Intent? intent, string name) where T : Java.Lang.Object =>
        OperatingSystem.IsAndroidVersionAtLeast(33)
            ? intent?.GetParcelableExtra(name, Java.Lang.Class.FromType(typeof(T))) as T
            : intent?.GetParcelableExtra(name) as T;
}

