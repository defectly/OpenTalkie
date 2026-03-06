using OpenTalkie.Presentation.Abstractions.Services;

namespace OpenTalkie.Services;

public sealed class ShellNavigationService : INavigationService
{
    public Task NavigateToAsync(string route, IDictionary<string, object>? parameters = null)
    {
        if (Shell.Current == null)
        {
            return Task.CompletedTask;
        }

        return parameters is { Count: > 0 }
            ? Shell.Current.GoToAsync(route, parameters)
            : Shell.Current.GoToAsync(route);
    }

    public Task GoBackAsync()
    {
        if (Shell.Current == null)
        {
            return Task.CompletedTask;
        }

        return Shell.Current.GoToAsync("..");
    }
}
