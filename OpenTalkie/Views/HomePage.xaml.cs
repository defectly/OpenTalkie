using Microsoft.Maui.Controls;
using OpenTalkie.Presentation.ViewModels;

namespace OpenTalkie.Presentation.Views;

public partial class HomePage : ContentPage
{
    public HomePage(HomeViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
