using CommunityToolkit.Maui.Views;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace OpenTalkie.View.Popups;

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
    private string _message;

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
        _popup.Close();
    }

    [RelayCommand]
    private void Cancel()
    {
        _popup.Close();
    }
}