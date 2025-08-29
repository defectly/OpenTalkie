using OpenTalkie.ViewModel;

namespace OpenTalkie.View;

public partial class AddStreamPage : ContentPage
{
    public AddStreamPage(AddStreamViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
