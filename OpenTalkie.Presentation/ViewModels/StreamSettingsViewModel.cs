using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Mediator;
using OpenTalkie.Application.Abstractions.Services;
using OpenTalkie.Application.Streams.Commands;
using OpenTalkie.Domain.Enums;
using OpenTalkie.Domain.Models;
using OpenTalkie.Domain.VBAN;
using OpenTalkie.Presentation.Abstractions.Services;

namespace OpenTalkie.Presentation.ViewModels;

public partial class StreamSettingsViewModel : ObservableObject
{
    private readonly IMediator _mediator;
    private readonly IEndpointCatalogService _endpointCatalogService;
    private readonly IDenoiseAvailabilityService _denoiseAvailabilityService;
    private readonly IUserDialogService _dialogService;
    private Endpoint? _currentEndpoint;
    private bool _isApplyingEndpointState;
    private Guid _endpointId;
    private EndpointType _streamType;

    public StreamSettingsViewModel(
        IMediator mediator,
        IEndpointCatalogService endpointCatalogService,
        IDenoiseAvailabilityService denoiseAvailabilityService,
        IUserDialogService dialogService)
    {
        _mediator = mediator;
        _endpointCatalogService = endpointCatalogService;
        _denoiseAvailabilityService = denoiseAvailabilityService;
        _dialogService = dialogService;
        _endpointCatalogService.EndpointsChanged += OnEndpointsChanged;
    }

    public Guid EndpointId
    {
        get => _endpointId;
        set
        {
            SetProperty(ref _endpointId, value);
            ReloadEndpoint();
        }
    }

    public EndpointType StreamType
    {
        get => _streamType;
        set
        {
            SetProperty(ref _streamType, value);
            ReloadEndpoint();
        }
    }

    [ObservableProperty]
    public partial string Name { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string Hostname { get; set; } = string.Empty;

    [ObservableProperty]
    public partial int Port { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DisplayQuality))]
    public partial VBanQuality Quality { get; set; }

    [ObservableProperty]
    public partial bool IsDenoiseEnabled { get; set; }

    [ObservableProperty]
    public partial bool AllowMobileData { get; set; }

    [ObservableProperty]
    public partial float Volume { get; set; }

    public string DisplayQuality => Quality switch
    {
        VBanQuality.VBAN_QUALITY_OPTIMAL => "Optimal",
        VBanQuality.VBAN_QUALITY_FAST => "Fast",
        VBanQuality.VBAN_QUALITY_MEDIUM => "Medium",
        VBanQuality.VBAN_QUALITY_SLOW => "Slow",
        VBanQuality.VBAN_QUALITY_VERYSLOW => "Very Slow",
        _ => string.Empty
    };

    [RelayCommand]
    private async Task EditName()
    {
        if (_currentEndpoint == null)
        {
            return;
        }

        await _dialogService.ShowEditFieldAsync(
            "Edit Name",
            Name,
            async result =>
            {
                if (string.IsNullOrWhiteSpace(result))
                {
                    await _dialogService.ShowErrorAsync("Name cannot be empty.");
                    return;
                }

                await TryUpdateEndpointAsync(new UpdateStreamEndpointCommand(StreamType, EndpointId, Name: result));
            });
    }

    [RelayCommand]
    private async Task EditHostname()
    {
        if (_currentEndpoint == null)
        {
            return;
        }

        await _dialogService.ShowEditFieldAsync(
            "Edit Hostname",
            Hostname,
            async result =>
            {
                if (string.IsNullOrWhiteSpace(result))
                {
                    await _dialogService.ShowErrorAsync("Hostname cannot be empty.");
                    return;
                }

                await TryUpdateEndpointAsync(new UpdateStreamEndpointCommand(StreamType, EndpointId, Hostname: result));
            });
    }

    [RelayCommand]
    private async Task EditPort()
    {
        if (_currentEndpoint == null)
        {
            return;
        }

        await _dialogService.ShowEditFieldAsync(
            "Edit Port",
            Port.ToString(),
            Keyboard.Numeric,
            async result =>
            {
                if (string.IsNullOrWhiteSpace(result) || !int.TryParse(result, out int port) || port <= 0 || port > 65535)
                {
                    await _dialogService.ShowErrorAsync("Invalid port number (must be 1-65535).");
                    return;
                }

                await TryUpdateEndpointAsync(new UpdateStreamEndpointCommand(StreamType, EndpointId, Port: port));
            },
            maxLength: 5);
    }

    [RelayCommand]
    private async Task EditQuality()
    {
        if (_currentEndpoint == null)
        {
            return;
        }

        var options = new[] { "Optimal", "Fast", "Medium", "Slow", "Very Slow" };
        await _dialogService.ShowOptionsAsync(
            "Select Net Quality",
            options,
            choice =>
            {
                var newQuality = choice switch
                {
                    "Optimal" => VBanQuality.VBAN_QUALITY_OPTIMAL,
                    "Fast" => VBanQuality.VBAN_QUALITY_FAST,
                    "Medium" => VBanQuality.VBAN_QUALITY_MEDIUM,
                    "Slow" => VBanQuality.VBAN_QUALITY_SLOW,
                    "Very Slow" => VBanQuality.VBAN_QUALITY_VERYSLOW,
                    _ => Quality
                };

                _ = TryUpdateEndpointAsync(new UpdateStreamEndpointCommand(StreamType, EndpointId, Quality: newQuality));
            });
    }

    [RelayCommand]
    private async Task DenoiseToggled()
    {
        if (_isApplyingEndpointState || _currentEndpoint == null)
        {
            return;
        }

        if (IsDenoiseEnabled)
        {
            var availability = _denoiseAvailabilityService.CheckAvailability();
            if (!availability.IsSuccess)
            {
                ApplyEndpoint(_currentEndpoint);
                await _dialogService.ShowErrorAsync(availability.ErrorMessage ?? "RNNoise is unavailable.");
                return;
            }
        }

        if (!await TryUpdateEndpointAsync(new UpdateStreamEndpointCommand(StreamType, EndpointId, IsDenoiseEnabled: IsDenoiseEnabled)))
        {
            ApplyEndpoint(_currentEndpoint);
        }
    }

    [RelayCommand]
    private async Task MobileDataToggled()
    {
        if (_isApplyingEndpointState || _currentEndpoint == null)
        {
            return;
        }

        if (!await TryUpdateEndpointAsync(new UpdateStreamEndpointCommand(StreamType, EndpointId, AllowMobileData: AllowMobileData)))
        {
            ApplyEndpoint(_currentEndpoint);
        }
    }

    [RelayCommand]
    private async Task VolumeChanged()
    {
        if (_isApplyingEndpointState || _currentEndpoint == null)
        {
            return;
        }

        if (!await TryUpdateEndpointAsync(new UpdateStreamEndpointCommand(StreamType, EndpointId, Volume: Volume)))
        {
            ApplyEndpoint(_currentEndpoint);
        }
    }

    [RelayCommand]
    private async Task ResetVolume()
    {
        if (_currentEndpoint == null)
        {
            return;
        }

        Volume = 1f;
        if (!await TryUpdateEndpointAsync(new UpdateStreamEndpointCommand(StreamType, EndpointId, Volume: 1f)))
        {
            ApplyEndpoint(_currentEndpoint);
        }
    }

    private void ReloadEndpoint()
    {
        if (EndpointId == Guid.Empty)
        {
            return;
        }

        _currentEndpoint = _endpointCatalogService.GetEndpoint(StreamType, EndpointId);
        if (_currentEndpoint != null)
        {
            ApplyEndpoint(_currentEndpoint);
        }
    }

    private void ApplyEndpoint(Endpoint endpoint)
    {
        _isApplyingEndpointState = true;
        Name = endpoint.Name;
        Hostname = endpoint.Hostname;
        Port = endpoint.Port;
        Quality = endpoint.Quality;
        IsDenoiseEnabled = endpoint.IsDenoiseEnabled;
        AllowMobileData = endpoint.AllowMobileData;
        Volume = endpoint.Volume;
        _isApplyingEndpointState = false;
    }

    private void OnEndpointsChanged(EndpointType endpointType)
    {
        if (endpointType == StreamType)
        {
            ReloadEndpoint();
        }
    }

    private async Task<bool> TryUpdateEndpointAsync(UpdateStreamEndpointCommand command)
    {
        OperationResult result = await _mediator.Send(command);

        if (result.IsSuccess)
        {
            ReloadEndpoint();
            return true;
        }

        await _dialogService.ShowErrorAsync(result.ErrorMessage ?? "Failed to update stream endpoint.");
        return false;
    }
}
