using OpenTalkie.ViewModel;

namespace OpenTalkie.View;

public partial class PlaybackSettingsPage : ContentPage
{
    public PlaybackSettingsPage(PlaybackSettingsViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
