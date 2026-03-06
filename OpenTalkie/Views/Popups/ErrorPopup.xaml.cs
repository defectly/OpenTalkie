using CommunityToolkit.Maui.Views;
using OpenTalkie.Presentation.ViewModels.Popups;

namespace OpenTalkie.Presentation.Views.Popups;

public partial class ErrorPopup : Popup
{
    public ErrorPopup(string message)
    {
        InitializeComponent();
        BindingContext = new ErrorViewModel(message, this);
    }
}
