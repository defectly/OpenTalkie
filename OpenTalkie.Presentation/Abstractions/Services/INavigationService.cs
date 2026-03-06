namespace OpenTalkie.Presentation.Abstractions.Services;

public interface INavigationService
{
    Task NavigateToAsync(string route, IDictionary<string, object>? parameters = null);

    Task GoBackAsync();
}
