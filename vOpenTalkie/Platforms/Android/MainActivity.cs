using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;

namespace vOpenTalkie;

[Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
public class MainActivity : MauiAppCompatActivity
{
    Intent intent;

    PowerManager pm;

    protected override void OnCreate(Bundle savedInstanceState)
    {
        base.OnCreate(savedInstanceState);

        intent = new Intent();
        pm = (PowerManager)GetSystemService(PowerService);

        Battery.BatteryOptimizationDialog += ShowBatteryOptimizationDialog;

        CheckBatteryIgnoring();
    }

    bool CheckBatteryIgnoring()
    {
        if (pm.IsIgnoringBatteryOptimizations(PackageName))
        {
            intent.SetAction(Android.Provider.Settings.ActionIgnoreBatteryOptimizationSettings);
            Battery.BatteryOptimizationTurned?.Invoke();
            return true;
        }

        return false;
    }

    private void ShowBatteryOptimizationDialog()
    {
        if (CheckBatteryIgnoring())
            return;

        intent.SetAction(Android.Provider.Settings.ActionRequestIgnoreBatteryOptimizations);
        intent.SetData(Android.Net.Uri.Parse("package:" + PackageName));
        StartActivity(intent);

        CheckBatteryIgnoring();
    }
}
