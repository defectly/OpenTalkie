using CommunityToolkit.Maui.Views; // For ShowPopupAsync
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OpenTalkie.Common.Enums;
using OpenTalkie.View.Popups; // For EditFieldPopup and ErrorPopup

namespace OpenTalkie.ViewModel;

[QueryProperty(nameof(StreamType), "StreamType")]
public partial class AddStreamViewModel : ObservableObject
{

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DisplayName))]
    private string? name = null;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DisplayHostname))]
    private string? hostname = null;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DisplayPort))]
    private int port = 6980;

    [ObservableProperty]
    private bool isDenoiseEnabled;

    [ObservableProperty]
    private bool isEnabled = true;

    public EndpointType StreamType { get; set; }

    // Properties for display in Labels
    public string DisplayName => string.IsNullOrWhiteSpace(Name) ? "Tap to set name" : Name;
    public string DisplayHostname => string.IsNullOrWhiteSpace(Hostname) ? "Tap to set hostname" : Hostname;
    public string DisplayPort => Port.ToString();


    public AddStreamViewModel() { }

    [RelayCommand]
    private async Task EditName()
    {
        string currentValue = Name ?? "";
        var popup = new EditFieldPopup(
            "Set Stream Name",
            currentValue,
            async (result) =>
            {
                if (string.IsNullOrWhiteSpace(result))
                {
                    await Application.Current.MainPage.ShowPopupAsync(new ErrorPopup("Name cannot be empty."));
                    return;
                }
                if (result.Length > 16)
                {
                    await Application.Current.MainPage.ShowPopupAsync(new ErrorPopup("Name cannot be longer than 16 characters."));
                    return;
                }
                Name = result;
            });
        await Application.Current.MainPage.ShowPopupAsync(popup);
    }

    [RelayCommand]
    private async Task EditHostname()
    {
        string currentValue = Hostname ?? "";
        var popup = new EditFieldPopup(
            "Set Hostname/IP",
            currentValue,
            async (result) =>
            {
                if (string.IsNullOrWhiteSpace(result))
                {
                    await Application.Current.MainPage.ShowPopupAsync(new ErrorPopup("Hostname cannot be empty."));
                    return;
                }
                if (!Uri.CheckHostName(result).HasFlag(UriHostNameType.IPv4) &&
                    !Uri.CheckHostName(result).HasFlag(UriHostNameType.IPv6) &&
                    !Uri.CheckHostName(result).HasFlag(UriHostNameType.Dns))
                {
                    await Application.Current.MainPage.ShowPopupAsync(new ErrorPopup("Invalid hostname format."));
                    return;
                }
                Hostname = result;
            });
        await Application.Current.MainPage.ShowPopupAsync(popup);
    }

    [RelayCommand]
    private async Task EditPort()
    {
        string currentValue = Port.ToString();
        var popup = new EditFieldPopup(
            "Set Port",
            currentValue,
            Keyboard.Numeric,
            maxLength: 5,
            async (result) =>
            {
                if (string.IsNullOrWhiteSpace(result) || !int.TryParse(result, out int newPort) || newPort <= 0 || newPort > 65535)
                {
                    await Application.Current.MainPage.ShowPopupAsync(new ErrorPopup("Invalid port number (must be 1-65535)."));
                    return;
                }
                Port = newPort;
            });
        await Application.Current.MainPage.ShowPopupAsync(popup);
    }

    [RelayCommand]
    private async Task Save()
    {
        // Final validation check before saving
        if (string.IsNullOrWhiteSpace(Name) || string.IsNullOrWhiteSpace(Hostname) || Port <= 0 || Port > 65535)
        {
            await Application.Current.MainPage.ShowPopupAsync(new ErrorPopup("All fields must be valid before saving. Please check Name, Hostname, and Port."));
            return;
        }

        // Name truncation is handled by Endpoint constructor. Length validation is in EditName popup callback.
        // Endpoint constructor handles SocketException internally (UdpClient will be null if hostname invalid).
        Endpoint newEndpoint;
        try
        {
            newEndpoint = new Endpoint(StreamType, Name, Hostname, Port, IsDenoiseEnabled)
            {
                IsEnabled = this.IsEnabled
            };
        }
        catch (Exception ex) // Catch other unexpected errors during Endpoint creation
        {
            System.Diagnostics.Debug.WriteLine($"Unexpected error creating Endpoint: {ex}"); // Keep debug for now
            await Application.Current.MainPage.ShowPopupAsync(new ErrorPopup($"An unexpected error occurred during stream creation: {ex.Message}"));
            return;
        }


        var navigationParameters = new Dictionary<string, object>
        {
            { "NewEndpoint", newEndpoint }
        };
        await Shell.Current.GoToAsync("..", navigationParameters);
    }

    [RelayCommand]
    private async Task Cancel()
    {
        await Shell.Current.GoToAsync("..");
    }
}
