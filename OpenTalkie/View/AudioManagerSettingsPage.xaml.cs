using OpenTalkie.ViewModel;

namespace OpenTalkie.View;

public partial class AudioManagerSettingsPage : ContentPage
{
    public AudioManagerSettingsPage(AudioManagerSettingsViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}