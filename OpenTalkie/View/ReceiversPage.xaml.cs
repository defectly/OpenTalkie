namespace OpenTalkie.View;

public partial class ReceiversPage : ContentPage
{
    public ReceiversPage(ViewModel.ReceiversViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
