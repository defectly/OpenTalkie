//using Android;
//using Android.App;
//using Android.Content;
//using Android.Content.PM;
//using Android.Graphics.Drawables;
//using Android.Service.QuickSettings;
//using Android.Widget;
//using OpenTalkie.Common.Services;

//namespace OpenTalkie.Platforms.Android.Common.Services.Playback;

//[Service(Exported = true, Permission = Manifest.Permission.BindQuickSettingsTile)]

//[IntentFilter([ActionQsTile])]
//public class PlaybackTileService : TileService
//{
//    private readonly PlaybackBroadcastService _broadcastService;

//    public PlaybackTileService()
//    {
//        if (OperatingSystem.IsAndroidVersionAtLeast(31))
//        {
//            var pm = Platform.AppContext.PackageManager;
//            var component = new ComponentName(Platform.AppContext, Java.Lang.Class.FromType(typeof(PlaybackTileService)));
//            pm.SetComponentEnabledSetting(component, ComponentEnabledState.Disabled, ComponentEnableOption.DontKillApp);
//            return;
//        }

//        var broadcastService = IPlatformApplication.Current?.Services.GetService<PlaybackBroadcastService>();
//        _broadcastService = broadcastService ?? throw new NullReferenceException("Playback service is not provided");
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
//            QsTile.Icon = Icon.CreateWithResource(this, global::Android.Resource.Drawable.IcMediaPlay);
//        }
//        else
//        {
//            QsTile.State = TileState.Inactive;
//            QsTile.Icon = Icon.CreateWithResource(this, global::Android.Resource.Drawable.IcMediaPlay);
//        }

//        QsTile.UpdateTile();
//    }

//    private async Task CallBroadcastServiceStartAsync()
//    {
//        if (QsTile == null) return;

//        if (_broadcastService.BroadcastState)
//        {
//            _broadcastService.Switch();
//            return;
//        }

//        bool isMicrophonePermissionGranted = await PermissionManager.RequestMicrophonePermissionAsync();

//        if (!isMicrophonePermissionGranted)
//        {
//            var message = "Microphone permission is not granted";
//            Toast.MakeText(Platform.AppContext, message, ToastLength.Short)?.Show();
//            return;
//        }

//        bool isMediaProjectionPermissionGranted = await _broadcastService.RequestPermissionAsync();

//        if (!isMediaProjectionPermissionGranted)
//        {
//            var message = "Screen capture permission is not granted";
//            Toast.MakeText(Platform.AppContext, message, ToastLength.Short)?.Show();
//            return;
//        }

//        bool isSwitchSuccess = _broadcastService.Switch();

//        if (!isSwitchSuccess)
//        {
//            var permissionStatus = await Permissions.CheckStatusAsync<Permissions.Microphone>();
//            if (permissionStatus != PermissionStatus.Granted)
//            {
//                var message = "Mic permission is not granted";
//                Toast.MakeText(Platform.AppContext, message, ToastLength.Short)?.Show();
//                return;
//            }
//            else
//            {
//                var message = "Something went wrong";
//                Toast.MakeText(Platform.AppContext, message, ToastLength.Short)?.Show();
//                return;
//            }
//        }
//    }
//}
