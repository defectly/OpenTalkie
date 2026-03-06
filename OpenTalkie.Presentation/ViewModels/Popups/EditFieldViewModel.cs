using CommunityToolkit.Maui.Views;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace OpenTalkie.Presentation.ViewModels.Popups;

public partial class EditFieldViewModel : ObservableObject
{
    private readonly Popup _popup;
    private readonly Func<string, Task> _onSave;

    [ObservableProperty]
    public partial string Title { get; set; }

    [ObservableProperty]
    public partial string Value { get; set; }

    [ObservableProperty]
    public partial string Placeholder { get; set; } = "Enter value";

    [ObservableProperty]
    public partial Keyboard KeyboardType { get; set; }

    [ObservableProperty]
    public partial int? MaxLength { get; set; }

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
            await _onSave.Invoke(Value);
        }

        await _popup.CloseAsync();
    }

    [RelayCommand]
    private void Cancel()
    {
        _ = _popup.CloseAsync();
    }
}
