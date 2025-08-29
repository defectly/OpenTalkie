using OpenTalkie.ViewModel;

namespace OpenTalkie.View;

public partial class MicSettingsPage : ContentPage
{
    public MicSettingsPage(MicSettingsViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
