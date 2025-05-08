using CommunityToolkit.Maui.Views;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace OpenTalkie.View.Popups;

using System.Threading.Tasks; // Added for Task

public partial class EditFieldViewModel : ObservableObject
{
    private readonly Popup _popup;
    private readonly Func<string, Task> _onSave; // Changed Action<string> to Func<string, Task>

    [ObservableProperty]
    private string _title;

    [ObservableProperty]
    private string _value;

    [ObservableProperty]
    private string _placeholder = "Enter value";

    [ObservableProperty]
    private Keyboard _keyboardType;

    [ObservableProperty]
    private int? _maxLength;

    public EditFieldViewModel(string title, string initialValue, Keyboard keyboardType, Func<string, Task> onSave, Popup popup)
    {
        Title = title;
        Value = initialValue;
        KeyboardType = keyboardType;
        _onSave = onSave;
        _popup = popup;
    }

    public EditFieldViewModel(string title, string initialValue, Keyboard keyboardType, int maxLength, Func<string, Task> onSave, Popup popup)
    {
        Title = title;
        Value = initialValue;
        KeyboardType = keyboardType;
        MaxLength = maxLength;
        _onSave = onSave;
        _popup = popup;
    }

    [RelayCommand]
    private async Task Save()
    {
        if (_onSave != null)
        {
            await _onSave.Invoke(Value); // Await the callback before closing
        }
        _popup.Close();
    }

    [RelayCommand]
    private void Cancel()
    {
        _popup.Close();
    }
}
