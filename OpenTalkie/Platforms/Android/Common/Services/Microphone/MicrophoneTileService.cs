//using Android;
//using Android.App;
//using Android.Content;
//using Android.Content.PM;
//using Android.Graphics.Drawables;
//using Android.Service.QuickSettings;
//using Android.Widget;
//using OpenTalkie.Common.Services;

//namespace OpenTalkie.Platforms.Android.Common.Services.Microphone;

//[Service(Exported = true, Permission = Manifest.Permission.BindQuickSettingsTile)]

//[IntentFilter([ActionQsTile])]
//public class MicrophoneTileService : TileService
//{
//    private MicrophoneBroadcastService _broadcastService;

//    public MicrophoneTileService()
//    {
//        if (OperatingSystem.IsAndroidVersionAtLeast(31))
//        {
//            var pm = Platform.AppContext.PackageManager;
//            var component = new ComponentName(Platform.AppContext, Java.Lang.Class.FromType(typeof(MicrophoneTileService)));
//            pm.SetComponentEnabledSetting(component, ComponentEnabledState.Disabled, ComponentEnableOption.DontKillApp);
//            return;
//        }

//        var broadcastService = IPlatformApplication.Current?.Services.GetService<MicrophoneBroadcastService>();
//        _broadcastService = broadcastService ?? throw new NullReferenceException("Microphone service is not provided");
//        _broadcastService.BroadcastStateChanged += (isActive) => UpdateTile();
//    }

//    public override void OnStartListening()
//    {
//        base.OnStartListening();

//        UpdateTile();
//    }

//    public override async void OnClick()
//    {
//        base.OnClick();

//        await CallBroadcastServiceStartAsync();
//    }

//    private void UpdateTile()
//    {
//        if (QsTile == null) return;

//        if (_broadcastService.BroadcastState)
//        {
//            QsTile.State = TileState.Active;
//            QsTile.Icon = Icon.CreateWithResource(this, global::Android.Resource.Drawable.PresenceAudioOnline);
//        }
//        else
//        {
//            QsTile.State = TileState.Inactive;
//            QsTile.Icon = Icon.CreateWithResource(this, global::Android.Resource.Drawable.PresenceAudioOnline);
//        }

//        QsTile.UpdateTile();
//    }

//    private async Task CallBroadcastServiceStartAsync()
//    {
//        if (QsTile == null) return;

//        bool isSwitchSuccess = await _broadcastService.Switch();

//        if (!isSwitchSuccess)
//        {
//            var permissionStatus = await Permissions.CheckStatusAsync<Permissions.Microphone>();
//            if (permissionStatus != PermissionStatus.Granted)
//            {
//                var message = "Mic permission is not granted";
//                Toast.MakeText(Platform.AppContext, message, ToastLength.Short)?.Show();
//                return;
//            }
//        }
//    }
//}
