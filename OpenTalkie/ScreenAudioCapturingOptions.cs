namespace OpenTalkie;

public class ScreenAudioCapturingOptions
{
    internal const string defaultAndroidNotificationTitle = "Screen audio capturing in progress...";
    internal const string defaultAndroidNotificationText = " A screen audio capturing is currently in progress";

    /// <summary>
    /// Gets or sets the notification content title.
    /// Default value is "Screen recording in progress...".
    /// </summary>
    /// <remarks>This property only has effect on Android. On other platforms no notification is shown when a screen recording is being made.</remarks>
    public string NotificationContentTitle { get; set; } = defaultAndroidNotificationTitle;

    /// <summary>
    /// Gets or sets the notification content text.
    /// Default value is "A screen recording is currently in progress".
    /// </summary>
    /// <remarks>This property only has effect on Android. On other platforms no notification is shown when a screen recording is being made.</remarks>
    public string NotificationContentText { get; set; } = defaultAndroidNotificationText;
}
