namespace OpenTalkie.Platforms.Android.Common;

internal class PermissionManager
{
    internal static async Task<bool> RequestMicrophonePermissionAsync()
    {
        var status = await Permissions.CheckStatusAsync<Permissions.Microphone>();
        if (status == PermissionStatus.Granted) return true;

        status = await Permissions.RequestAsync<Permissions.Microphone>();

        return status == PermissionStatus.Granted;
    }
}
