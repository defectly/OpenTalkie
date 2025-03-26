using OpenTalkie.ViewModel;

namespace OpenTalkie.View;

public partial class MicSettingsPage : ContentPage
{
	public MicSettingsPage(MicSettingsViewModel vm)
	{
		InitializeComponent();
		BindingContext = vm;
	}
}