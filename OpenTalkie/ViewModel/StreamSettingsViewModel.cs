using CommunityToolkit.Maui.Views;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OpenTalkie.View.Popups;

namespace OpenTalkie.ViewModel;

public partial class StreamSettingsViewModel(AppShell mainPage) : ObservableObject
{
    [ObservableProperty]
    private Endpoint? endpoint;

    [RelayCommand]
    private async Task EditName()
    {
        if (Endpoint == null) return;

        string currentValue = Endpoint.Name;
        var popup = new EditFieldPopup(
            "Edit Name",
            currentValue,
            async (result) =>
            {
                if (string.IsNullOrEmpty(result))
                {
                    var errorPopup = new ErrorPopup("Empty string");
                    await mainPage.ShowPopupAsync(errorPopup);
                    return;
                }

                if (result != null)
                {
                    Endpoint.Name = result;
                    OnPropertyChanged(nameof(Endpoint));
                }
            });

        await mainPage.ShowPopupAsync(popup);
    }

    [RelayCommand]
    private async Task EditHostname()
    {
        if (Endpoint == null) return;

        string currentValue = Endpoint.Hostname;
        var popup = new EditFieldPopup(
            "Edit Hostname",
            currentValue,
            async (result) =>
            {
                if (string.IsNullOrEmpty(result))
                {
                    var errorPopup = new ErrorPopup("Empty string");
                    await mainPage.ShowPopupAsync(errorPopup);
                    return;
                }

                if (result != null)
                {
                    Endpoint.Hostname = result;
                    OnPropertyChanged(nameof(Endpoint));
                }
            });

        await mainPage.ShowPopupAsync(popup);
    }

    [RelayCommand]
    private async Task EditPort()
    {
        if (Endpoint == null) return;

        string currentValue = Endpoint.Port.ToString();
        var popup = new EditFieldPopup(
            "Edit Port",
            currentValue,
            Keyboard.Numeric,
            maxLength: 5,
            async (result) =>
            {
                if (result != null)
                {
                    if (int.TryParse(result, out int port))
                    {
                        Endpoint.Port = port;
                        OnPropertyChanged(nameof(Endpoint));
                    }
                    else
                    {
                        var errorPopup = new ErrorPopup("Invalid port number");
                        await mainPage.ShowPopupAsync(errorPopup);
                    }
                }
            });

        await mainPage.ShowPopupAsync(popup);
    }
}