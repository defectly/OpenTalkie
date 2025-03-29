using CommunityToolkit.Maui.Views;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace OpenTalkie.View.Popups;

public partial class EditFieldViewModel : ObservableObject
{
    private readonly Popup _popup;
    private readonly Action<string> _onSave;

    [ObservableProperty]
    private string _title;

    [ObservableProperty]
    private string _value;

    [ObservableProperty]
    private string _placeholder = "Enter value";

    [ObservableProperty]
    private Keyboard _keyboardType;

    public EditFieldViewModel(string title, string initialValue, Keyboard keyboardType, Action<string> onSave, Popup popup)
    {
        Title = title;
        Value = initialValue;
        KeyboardType = keyboardType;
        _onSave = onSave;
        _popup = popup;
    }

    [RelayCommand]
    private void Save()
    {
        _onSave?.Invoke(Value);
        _popup.Close();
    }

    [RelayCommand]
    private void Cancel()
    {
        _popup.Close();
    }
}