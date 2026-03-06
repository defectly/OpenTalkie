using CommunityToolkit.Maui.Views;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace OpenTalkie.Presentation.ViewModels.Popups;

public partial class OptionsViewModel : ObservableObject
{
    private readonly Popup _popup;
    private readonly Action<string> _onSelect;

    [ObservableProperty]
    public partial string Title { get; set; }

    [ObservableProperty]
    public partial string[] Options { get; set; }

    public OptionsViewModel(string title, string[] options, Action<string> onSelect, Popup popup)
    {
        Title = title;
        Options = options;
        _onSelect = onSelect;
        _popup = popup;
    }

    [RelayCommand]
    private void SelectOption(string option)
    {
        _onSelect?.Invoke(option);
        _ = _popup.CloseAsync();
    }

    [RelayCommand]
    private void Cancel()
    {
        _ = _popup.CloseAsync();
    }
}
