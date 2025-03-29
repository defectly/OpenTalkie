using CommunityToolkit.Maui.Views;
using OpenTalkie.ViewModel.Popups;

namespace OpenTalkie.View.Popups;

public partial class OptionsPopup : Popup
{
    public OptionsPopup(string title, string[] options, Action<string> onSelect)
    {
        InitializeComponent();
        BindingContext = new OptionsViewModel(title, options, onSelect, this);
    }
}