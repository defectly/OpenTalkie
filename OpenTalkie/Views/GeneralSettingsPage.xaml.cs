using OpenTalkie.Presentation.ViewModels;

namespace OpenTalkie.Presentation.Views;

public partial class GeneralSettingsPage : ContentPage
{
    public GeneralSettingsPage(GeneralSettingsViewModel viewModel)
    {
        BindingContext = viewModel;
        InitializeComponent();
    }
}
