namespace OpenTalkie.View;

public partial class ReceiversPage : ContentPage
{
    public ReceiversPage(ViewModel.ReceiversViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }
}

