using CommunityToolkit.Maui.Views;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OpenTalkie.VBAN;
using OpenTalkie.View.Popups;

namespace OpenTalkie.ViewModel;

public partial class StreamSettingsViewModel : ObservableObject
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DisplayQuality))]
    private Endpoint? endpoint;

    public string DisplayQuality => Endpoint?.Quality switch
    {
        VBanQuality.VBAN_QUALITY_OPTIMAL => "Optimal",
        VBanQuality.VBAN_QUALITY_FAST => "Fast",
        VBanQuality.VBAN_QUALITY_MEDIUM => "Medium",
        VBanQuality.VBAN_QUALITY_SLOW => "Slow",
        VBanQuality.VBAN_QUALITY_VERYSLOW => "Very Slow",
        _ => string.Empty
    };

    partial void OnEndpointChanged(Endpoint? value)
    {
        if (value != null)
        {
            value.PropertyChanged -= EndpointOnPropertyChanged;
            value.PropertyChanged += EndpointOnPropertyChanged;
        }
        OnPropertyChanged(nameof(DisplayQuality));
    }

    private void EndpointOnPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(Endpoint.Quality))
        {
            OnPropertyChanged(nameof(DisplayQuality));
        }
    }

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
                    await Application.Current.MainPage.ShowPopupAsync(errorPopup);
                    return;
                }

                if (result != null)
                {
                    Endpoint.Name = result;
                    OnPropertyChanged(nameof(Endpoint));
                }
            });

        await Application.Current.MainPage.ShowPopupAsync(popup);
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
                    await Application.Current.MainPage.ShowPopupAsync(errorPopup);
                    return;
                }

                if (result != null)
                {
                    Endpoint.Hostname = result;
                    OnPropertyChanged(nameof(Endpoint));
                }
            });

        await Application.Current.MainPage.ShowPopupAsync(popup);
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
                        await Application.Current.MainPage.ShowPopupAsync(errorPopup);
                    }
                }
            });

        await Application.Current.MainPage.ShowPopupAsync(popup);
    }

    [RelayCommand]
    private async Task EditQuality()
    {
        if (Endpoint == null) return;

        var options = new[] { "Optimal", "Fast", "Medium", "Slow", "Very Slow" };
        var popup = new OptionsPopup(
            "Select Net Quality",
            options,
            async (choice) =>
            {
                VBanQuality newQ = choice switch
                {
                    "Optimal" => VBanQuality.VBAN_QUALITY_OPTIMAL,
                    "Fast" => VBanQuality.VBAN_QUALITY_FAST,
                    "Medium" => VBanQuality.VBAN_QUALITY_MEDIUM,
                    "Slow" => VBanQuality.VBAN_QUALITY_SLOW,
                    "Very Slow" => VBanQuality.VBAN_QUALITY_VERYSLOW,
                    _ => Endpoint.Quality
                };
                Endpoint.Quality = newQ;
                OnPropertyChanged(nameof(DisplayQuality));
            });

        await Application.Current.MainPage.ShowPopupAsync(popup);
    }
}
