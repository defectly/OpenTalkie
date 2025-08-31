using OpenTalkie.ViewModel;

namespace OpenTalkie.View;

public partial class ReceiverSettingsPage : ContentPage
{
    public ReceiverSettingsPage(ReceiverSettingsViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}

