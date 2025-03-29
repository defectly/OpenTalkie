using CommunityToolkit.Maui.Views;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace OpenTalkie.ViewModel.Popups;

public partial class ErrorViewModel : ObservableObject
{
    private readonly Popup _popup;

    [ObservableProperty]
    private string _message;

    public ErrorViewModel(string message, Popup popup)
    {
        Message = message;
        _popup = popup;
    }

    [RelayCommand]
    private void Close()
    {
        _popup.Close();
    }
}