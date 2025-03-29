using CommunityToolkit.Maui.Views;

namespace OpenTalkie.View.Popups;

public partial class EditFieldPopup : Popup
{
    public EditFieldPopup(string title, string initialValue, Keyboard keyboardType, Action<string> onSave)
    {
        InitializeComponent();
        BindingContext = new EditFieldViewModel(title, initialValue, keyboardType, onSave, this);
    }
    public EditFieldPopup(string title, string initialValue, Action<string> onSave)
    {
        InitializeComponent();
        BindingContext = new EditFieldViewModel(title, initialValue, Keyboard.Default, onSave, this);
    }
}
