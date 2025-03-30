using CommunityToolkit.Maui.Views;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace OpenTalkie.ViewModel.Popups;

public partial class OptionsViewModel : ObservableObject
{
    private readonly Popup _popup;
    private readonly Action<string> _onSelect;

    [ObservableProperty]
    private string _title;

    [ObservableProperty]
    private string[] _options;

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
        _popup.Close();
    }

    [RelayCommand]
    private void Cancel()
    {
        _popup.Close();
    }
}