using OpenTalkie.ViewModel;

namespace OpenTalkie.View;

public partial class PlaybackSettingsPage : ContentPage
{
    public PlaybackSettingsPage(PlaybackSettingsViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }
}