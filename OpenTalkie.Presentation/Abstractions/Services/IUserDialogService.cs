namespace OpenTalkie.Presentation.Abstractions.Services;

public interface IUserDialogService
{
    Task ShowErrorAsync(string message);

    Task ShowOptionsAsync(string title, string[] options, Action<string> onSelect);

    Task ShowEditFieldAsync(string title, string initialValue, Func<string, Task> onSave);

    Task ShowEditFieldAsync(string title, string initialValue, Keyboard keyboard, Func<string, Task> onSave, int? maxLength = null);
}
