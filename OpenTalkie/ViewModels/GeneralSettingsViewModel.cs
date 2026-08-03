using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Maui.Storage;

namespace OpenTalkie.Presentation.ViewModels;

public partial class GeneralSettingsViewModel : ObservableObject
{
    // Single Source of Truth for both files
    public const string SwipeNavPreferenceKey = "SwipeNavigationEnabled";
    public const bool DefaultSwipeNavEnabled = true;

    [ObservableProperty]
    private bool _isSwipingEnabled;

    public GeneralSettingsViewModel()
    {
        // Read using internal public constants
        _isSwipingEnabled = Preferences.Default.Get(SwipeNavPreferenceKey, DefaultSwipeNavEnabled);
    }

    partial void OnIsSwipingEnabledChanged(bool value)
    {
        Preferences.Default.Set(SwipeNavPreferenceKey, value);
    }
}