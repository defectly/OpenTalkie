using Microsoft.Maui;
using OpenTalkie.Presentation.Abstractions.Services;
using OpenTalkie.Presentation.Views.Popups;
using OpenTalkie.Views;

namespace OpenTalkie.Services;

public sealed class UserDialogService : IUserDialogService
{
    public Task ShowErrorAsync(string message)
    {
        return PopupHost.ShowAsync(new ErrorPopup(message));
    }

    public Task ShowOptionsAsync(string title, string[] options, Action<string> onSelect)
    {
        return PopupHost.ShowAsync(new OptionsPopup(title, options, onSelect));
    }

    public Task ShowEditFieldAsync(string title, string initialValue, Func<string, Task> onSave)
    {
        return PopupHost.ShowAsync(new EditFieldPopup(title, initialValue, onSave));
    }

    public Task ShowEditFieldAsync(string title, string initialValue, Keyboard keyboard, Func<string, Task> onSave, int? maxLength = null)
    {
        return maxLength.HasValue
            ? PopupHost.ShowAsync(new EditFieldPopup(title, initialValue, keyboard, maxLength.Value, onSave))
            : PopupHost.ShowAsync(new EditFieldPopup(title, initialValue, keyboard, onSave));
    }
}
