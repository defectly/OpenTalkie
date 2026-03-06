using CommunityToolkit.Maui.Views;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace OpenTalkie.Presentation.ViewModels.Popups;

public partial class ErrorViewModel : ObservableObject
{
    private readonly Popup _popup;

    [ObservableProperty]
    public partial string Message { get; set; }

    public ErrorViewModel(string message, Popup popup)
    {
        Message = message;
        _popup = popup;
    }

    [RelayCommand]
    private void Close()
    {
        _ = _popup.CloseAsync();
    }
}
