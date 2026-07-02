using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using Android.Views;

namespace OpenTalkie.Platforms.Android;

[Activity(Theme = "@style/Maui.SplashTheme", 
    MainLauncher = true, 
    ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
public class MainActivity : MauiAppCompatActivity
{
    public static MainActivity? Instance { get; private set; }

    // Instantiate swipe detector
    private SwipeNavigationDetector _swipeDetector = null!;


    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        Instance = this;

        _swipeDetector = new SwipeNavigationDetector();
    }

    // Intercept touch data at the root window level
    public override bool DispatchTouchEvent(MotionEvent? ev)
    {
        // Hand the touch data off to our helper file. 
        // If it returns true, it means a swipe happened and we swallow it.
        if (_swipeDetector != null && _swipeDetector.HandleTouchEvent(ev))
        {
            return true; 
        }

        // Otherwise, let normal clicks and scrolls pass through to MAUI untouched
        return base.DispatchTouchEvent(ev);
    }

    protected override void OnActivityResult(int requestCode, Result resultCode, Intent? data)
    {
        base.OnActivityResult(requestCode, resultCode, data);
    }
}
