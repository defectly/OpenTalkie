using CommunityToolkit.Maui.Views;

namespace OpenTalkie.View.Popups;

public partial class EditFieldPopup : Popup
{
    public EditFieldPopup(string title, string initialValue, Keyboard keyboardType, Func<string, Task> onSave)
    {
        InitializeComponent();
        BindingContext = new EditFieldViewModel(title, initialValue, keyboardType, onSave, this);
    }

    public EditFieldPopup(string title, string initialValue, Keyboard keyboardType, int maxLength, Func<string, Task> onSave)
    {
        InitializeComponent();
        BindingContext = new EditFieldViewModel(title, initialValue, keyboardType, maxLength, onSave, this);
    }

    public EditFieldPopup(string title, string initialValue, Func<string, Task> onSave)
    {
        InitializeComponent();
        BindingContext = new EditFieldViewModel(title, initialValue, Keyboard.Default, onSave, this);
    }
}
