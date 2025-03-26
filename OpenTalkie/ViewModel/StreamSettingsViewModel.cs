using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace OpenTalkie.ViewModel;

public partial class StreamSettingsViewModel : ObservableObject
{
    [ObservableProperty]
    private Endpoint endpoint;

    [RelayCommand]
    private async Task EditName()
    {
        if (Endpoint == null) return;

        string currentValue = Endpoint.Name;
        var result = await Application.Current.MainPage
            .DisplayPromptAsync("Edit", "Enter Name", "OK", "Cancel", currentValue);

        if (result != null)
        {
            Endpoint.Name = result;
            OnPropertyChanged(nameof(Endpoint));
        }
    }

    [RelayCommand]
    private async Task EditHostname()
    {
        if (Endpoint == null) return;

        string currentValue = Endpoint.Hostname;
        var result = await Application.Current.MainPage
            .DisplayPromptAsync("Edit", "Enter Hostname", "OK", "Cancel", currentValue);

        if (result != null)
        {
            Endpoint.Hostname = result;
            OnPropertyChanged(nameof(Endpoint));
        }
    }

    [RelayCommand]
    private async Task EditPort()
    {
        if (Endpoint == null) return;

        string currentValue = Endpoint.Port.ToString();
        var result = await Application.Current.MainPage
            .DisplayPromptAsync("Edit", "Enter Port", "OK", "Cancel", currentValue, keyboard: Keyboard.Numeric);

        if (result != null && int.TryParse(result, out int port))
        {
            Endpoint.Port = port;
            OnPropertyChanged(nameof(Endpoint));
        }
    }
}