using Android;
using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using Android.Util;

namespace OpenTalkie.Platforms.Android.Common.Services.Playback;

[Service(ForegroundServiceType = ForegroundService.TypeMediaProjection, Permission = Manifest.Permission.ForegroundServiceMediaProjection)]
internal class MediaProjectionForegroundService : Service
{
    public const int NotificationId = 1337;
    public const string ChannelId = "ScreenAudioCapturingService";
    public const string ExtraExternalMessenger = "ExternalMessenger";
    public const string ExtraContentTitle = "Screen audio capturing";
    public const string ExtraContentText = "service is running";
    public const string ExtraCommandNotificationSetup = "SetupNotification";
    public const string ExtraCommandBeginRecording = "BeginCapturing";

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
        Intent? notificationIntent;
        if (string.IsNullOrEmpty(PackageName) || PackageManager == null)
        {
            Log.Error(ChannelId, "PackageName or PackageManager is null, cannot create launch intent.");
            return;
        }

        notificationIntent = PackageManager.GetLaunchIntentForPackage(PackageName);
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

        // Android O
        if (OperatingSystem.IsAndroidVersionAtLeast(26))
        {
            CreateNotificationChannel();

            var contentTitle = intent?.GetStringExtra(ExtraContentTitle);
            var contentText = intent?.GetStringExtra(ExtraContentText);

            var notification = new Notification.Builder(this, ChannelId)
                .SetContentTitle(contentTitle)
                .SetContentText(contentText)
                .SetSmallIcon(global::Android.Resource.Drawable.IcMediaPlay)
                .SetContentIntent(pendingIntent)
                .SetAutoCancel(false)
                .Build();

            // Android Q
            if (OperatingSystem.IsAndroidVersionAtLeast(29))
                StartForeground(NotificationId, notification, ForegroundService.TypeMediaProjection);
            else
                StartForeground(NotificationId, notification);
        }
        else
        {
            // Pre-Android O
            StartForeground(NotificationId, CreateFallbackNotification(pendingIntent));
        }
    }

    private void CreateNotificationChannel()
    {
        if (!OperatingSystem.IsAndroidVersionAtLeast(26))
            return;

        var channel = new NotificationChannel(ChannelId, "Screen capturing service", NotificationImportance.Default)
        {
            Description = "Notification channel for screen recording service"
        };

        var notificationManager = GetSystemService(NotificationService) as NotificationManager;
        notificationManager?.CreateNotificationChannel(channel);
    }

    private Notification CreateFallbackNotification(PendingIntent? pendingIntent = null)
    {
        if (!OperatingSystem.IsAndroidVersionAtLeast(26))
        {
            var builder = new Notification.Builder(this)
                .SetContentTitle("Screen audio capturing")
                .SetContentText("Screen audio capturing is running.")
                .SetSmallIcon(global::Android.Resource.Drawable.IcMediaPlay);

            if (pendingIntent != null)
            {
                builder.SetContentIntent(pendingIntent);
                builder.SetAutoCancel(false);
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