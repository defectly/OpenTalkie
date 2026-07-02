using Android.Views;

namespace OpenTalkie.Platforms.Android;

public class SwipeNavigationDetector
{
    private const int SwipeThreshold = 80;
    private float _startX;
    private float _startY;

    // Processes incoming window touches to check for swipe gestures.
    public bool HandleTouchEvent(MotionEvent? ev)
    {
        if (ev is null) return false;

        switch (ev.Action)
        {
            case MotionEventActions.Down:
                _startX = ev.RawX;
                _startY = ev.RawY;
                break;

            case MotionEventActions.Up:
                var deltaX = ev.RawX - _startX;
                var deltaY = ev.RawY - _startY;

                // Ensure horizontal movement is greater than the threshold and flatter than a vertical scroll
                if (Math.Abs(deltaX) > SwipeThreshold && Math.Abs(deltaX) > Math.Abs(deltaY) * 1.2f)
                {
                    // Run on main thread to safely update UI navigation
                    MainThread.BeginInvokeOnMainThread(() => TriggerTabNavigation(deltaX < 0 ? 1 : -1));
                    return true; 
                }
                break;
        }

        return false; 
    }


    // Calculates current shell tab index dynamically and shifts navigation.
    // Works perfectly on root pages and sub-pages alike.
    private void TriggerTabNavigation(int direction)
    {
        var shell = Shell.Current;
        if (shell is null) return;

        // Optional UX Guardrail: Uncomment the line below if you don't want 
        // swiping to switch tabs when a sub-page has a "Go Back" backstack arrow.
        // if (shell.Navigation.NavigationStack.Count > 1) return;

        var tabBar = shell.CurrentItem; 
        if (tabBar is null) return;

        var currentTab = tabBar.CurrentItem; 
        if (currentTab is null) return;

        // Fetch only visible tabs to handle hidden or dynamic items properly
        var visibleTabs = tabBar.Items.Where(t => t.IsVisible).ToList();
        int currentIndex = visibleTabs.IndexOf(currentTab);
        
        if (currentIndex < 0) return;

        int nextIndex = currentIndex + direction;

        // Execute the tab shift if within layout boundaries
        if (nextIndex >= 0 && nextIndex < visibleTabs.Count)
        {
            tabBar.CurrentItem = visibleTabs[nextIndex];
        }
    }
}