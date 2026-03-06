using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Mediator;
using OpenTalkie.Application.Abstractions.Services;
using OpenTalkie.Application.Streams.Commands;
using OpenTalkie.Domain.Enums;
using OpenTalkie.Domain.VBAN;
using OpenTalkie.Presentation.Abstractions.Services;

namespace OpenTalkie.Presentation.ViewModels;

public partial class AddStreamViewModel : ObservableObject
{
    private readonly IMediator _mediator;
    private readonly IDenoiseAvailabilityService _denoiseAvailabilityService;
    private readonly IUserDialogService _dialogService;
    private readonly INavigationService _navigationService;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DisplayName))]
    public partial string? Name { get; set; } = null;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DisplayHostname))]
    public partial string? Hostname { get; set; } = null;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DisplayPort))]
    public partial int Port { get; set; } = 6980;

    [ObservableProperty]
    public partial bool IsDenoiseEnabled { get; set; }

    [ObservableProperty]
    public partial bool AllowMobileData { get; set; }

    [ObservableProperty]
    public partial bool IsEnabled { get; set; } = true;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DisplayQuality))]
    public partial VBanQuality SelectedQuality { get; set; } = VBanQuality.VBAN_QUALITY_FAST;
    public EndpointType StreamType { get; set; }

    // Properties for display in Labels
    public string DisplayName => string.IsNullOrWhiteSpace(Name) ? "Tap to set name" : Name;
    public string DisplayHostname => string.IsNullOrWhiteSpace(Hostname) ? "Tap to set hostname" : Hostname;
    public string DisplayPort => Port.ToString();
    public string DisplayQuality => SelectedQuality switch
    {
        VBanQuality.VBAN_QUALITY_OPTIMAL => "Optimal",
        VBanQuality.VBAN_QUALITY_FAST => "Fast",
        VBanQuality.VBAN_QUALITY_MEDIUM => "Medium",
        VBanQuality.VBAN_QUALITY_SLOW => "Slow",
        VBanQuality.VBAN_QUALITY_VERYSLOW => "Very Slow",
        _ => "Fast"
    };


    public AddStreamViewModel(
        IMediator mediator,
        IDenoiseAvailabilityService denoiseAvailabilityService,
        IUserDialogService dialogService,
        INavigationService navigationService)
    {
        _mediator = mediator;
        _denoiseAvailabilityService = denoiseAvailabilityService;
        _dialogService = dialogService;
        _navigationService = navigationService;
    }

    [RelayCommand]
    private async Task EditName()
    {
        string currentValue = Name ?? "";
        await _dialogService.ShowEditFieldAsync(
            "Set Stream Name",
            currentValue,
            async (result) =>
            {
                if (string.IsNullOrWhiteSpace(result))
                {
                    await _dialogService.ShowErrorAsync("Name cannot be empty.");
                    return;
                }
                if (result.Length > 16)
                {
                    await _dialogService.ShowErrorAsync("Name cannot be longer than 16 characters.");
                    return;
                }
                Name = result;
            });
    }

    [RelayCommand]
    private async Task EditHostname()
    {
        string currentValue = Hostname ?? "";
        await _dialogService.ShowEditFieldAsync(
            "Set Hostname/IP",
            currentValue,
            async (result) =>
            {
                if (string.IsNullOrWhiteSpace(result))
                {
                    await _dialogService.ShowErrorAsync("Hostname cannot be empty.");
                    return;
                }
                if (!Uri.CheckHostName(result).HasFlag(UriHostNameType.IPv4) &&
                    !Uri.CheckHostName(result).HasFlag(UriHostNameType.IPv6) &&
                    !Uri.CheckHostName(result).HasFlag(UriHostNameType.Dns))
                {
                    await _dialogService.ShowErrorAsync("Invalid hostname format.");
                    return;
                }
                Hostname = result;
            });
    }

    [RelayCommand]
    private async Task EditPort()
    {
        string currentValue = Port.ToString();
        await _dialogService.ShowEditFieldAsync(
            "Set Port",
            currentValue,
            Keyboard.Numeric,
            async (result) =>
            {
                if (string.IsNullOrWhiteSpace(result) || !int.TryParse(result, out int newPort) || newPort <= 0 || newPort > 65535)
                {
                    await _dialogService.ShowErrorAsync("Invalid port number (must be 1-65535).");
                    return;
                }
                Port = newPort;
            },
            maxLength: 5);
    }

    [RelayCommand]
    private async Task EditQuality()
    {
        var options = new[] { "Optimal", "Fast", "Medium", "Slow", "Very Slow" };
        await _dialogService.ShowOptionsAsync(
            "Select Net Quality",
            options,
            (choice) =>
            {
                SelectedQuality = choice switch
                {
                    "Optimal" => VBanQuality.VBAN_QUALITY_OPTIMAL,
                    "Fast" => VBanQuality.VBAN_QUALITY_FAST,
                    "Medium" => VBanQuality.VBAN_QUALITY_MEDIUM,
                    "Slow" => VBanQuality.VBAN_QUALITY_SLOW,
                    "Very Slow" => VBanQuality.VBAN_QUALITY_VERYSLOW,
                    _ => VBanQuality.VBAN_QUALITY_FAST
                };
                OnPropertyChanged(nameof(DisplayQuality));
            });
    }

    [RelayCommand]
    private async Task Save()
    {
        if (string.IsNullOrWhiteSpace(Name) || string.IsNullOrWhiteSpace(Hostname) || Port <= 0 || Port > 65535)
        {
            await _dialogService.ShowErrorAsync("All fields must be valid before saving. Please check Name, Hostname, and Port.");
            return;
        }

        if (IsDenoiseEnabled)
        {
            var availability = _denoiseAvailabilityService.CheckAvailability();
            if (!availability.IsSuccess)
            {
                await _dialogService.ShowErrorAsync(availability.ErrorMessage ?? "RNNoise is unavailable.");
                return;
            }
        }

        var createResult = await _mediator.Send(new CreateStreamEndpointCommand(
            StreamType,
            Name!,
            Hostname!,
            Port,
            IsDenoiseEnabled,
            AllowMobileData,
            IsEnabled,
            SelectedQuality));

        if (!createResult.IsSuccess)
        {
            await _dialogService.ShowErrorAsync(createResult.ErrorMessage ?? "Failed to create stream endpoint.");
            return;
        }

        await _navigationService.GoBackAsync();
    }

    [RelayCommand]
    private async Task Cancel()
    {
        await _navigationService.GoBackAsync();
    }
}



