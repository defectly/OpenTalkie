using CommunityToolkit.Maui.Views;
using OpenTalkie.ViewModel.Popups;

namespace OpenTalkie.View.Popups;

public partial class ErrorPopup : Popup
{
    public ErrorPopup(string message)
    {
        InitializeComponent();
        BindingContext = new ErrorViewModel(message, this);
    }
}