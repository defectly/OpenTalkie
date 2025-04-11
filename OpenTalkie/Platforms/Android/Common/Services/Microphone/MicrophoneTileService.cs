using Android;
using Android.App;
using Android.Graphics.Drawables;
using Android.Service.QuickSettings;
using Android.Widget;
using OpenTalkie.Common.Services;
using OpenTalkie.Common.Services.Interfaces;

namespace OpenTalkie.Platforms.Android.Common.Services.Microphone;

[Service(Exported = true, Permission = Manifest.Permission.BindQuickSettingsTile)]

[IntentFilter([ActionQsTile])]
public class MicrophoneTileService : TileService
{
    private bool _isTileActive;

    public MicrophoneTileService()
    {
        var microphoneCapturingService = IPlatformApplication.Current?.Services.GetService<IMicrophoneCapturingService>();
        if (microphoneCapturingService == null)
            throw new NullReferenceException("Microphone service is not provided");

        microphoneCapturingService.OnServiceStateChange += OnMicrophoneServiceSwitch;
    }

    public override void OnStartListening()
    {
        base.OnStartListening();

        UpdateTile();
    }

    public override async void OnClick()
    {
        base.OnClick();

        await PerformActionAsync(_isTileActive);
    }

    private void UpdateTile()
    {
        if (QsTile == null) return;

        if (_isTileActive)
        {
            QsTile.State = TileState.Active;
            QsTile.Icon = Icon.CreateWithResource(this, global::Android.Resource.Drawable.PresenceAudioOnline);
        }
        else
        {
            QsTile.State = TileState.Inactive;
            QsTile.Icon = Icon.CreateWithResource(this, global::Android.Resource.Drawable.PresenceAudioOnline);
        }

        QsTile.UpdateTile();
    }

    private async Task PerformActionAsync(bool isActive)
    {
        if (QsTile == null) return;

        var broadcastService = IPlatformApplication.Current?.Services.GetService<MicrophoneBroadcastService>();
        if (broadcastService == null)
            throw new NullReferenceException("Microphone service is not provided");

        bool isSwitchSuccess = await broadcastService.Switch();

        if (!isSwitchSuccess)
        {
            var permissionStatus = await Permissions.CheckStatusAsync<Permissions.Microphone>();
            if (permissionStatus != PermissionStatus.Granted)
            {
                var message = "Mic permission is not granted";
                Toast.MakeText(Platform.AppContext, message, ToastLength.Short)?.Show();
                return;
            }
        }
    }

    private void OnMicrophoneServiceSwitch(bool isActive)
    {
        _isTileActive = isActive;
        UpdateTile();
    }
}
