using CommunityToolkit.Maui.Views;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace OpenTalkie.Presentation.Views.Popups;

public partial class DeleteConfirmationPopup : Popup
{
    public DeleteConfirmationPopup(string message, Action onDelete)
    {
        InitializeComponent();
        BindingContext = new DeleteConfirmationViewModel(message, onDelete, this);
    }
}

public partial class DeleteConfirmationViewModel : ObservableObject
{
    private readonly Popup _popup;
    private readonly Action _onDelete;

    [ObservableProperty]
    public partial string Message { get; set; }

    public DeleteConfirmationViewModel(string message, Action onDelete, Popup popup)
    {
        Message = message;
        _onDelete = onDelete;
        _popup = popup;
    }

    [RelayCommand]
    private void Delete()
    {
        _onDelete?.Invoke();
        _ = _popup.CloseAsync();
    }

    [RelayCommand]
    private void Cancel()
    {
        _ = _popup.CloseAsync();
    }
}
