using OpenTalkie.ViewModel;

namespace OpenTalkie.View;

public partial class HomePage : ContentPage
{
	public HomePage(HomeViewModel vm)
    {
		InitializeComponent();
        BindingContext = vm;
    }
}