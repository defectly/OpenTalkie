using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Mediator;
using OpenTalkie.Application.Abstractions.Services;
using OpenTalkie.Application.Streams.Commands;
using OpenTalkie.Domain.Enums;
using OpenTalkie.Presentation.Abstractions.Services;
using System.Collections.ObjectModel;

namespace OpenTalkie.Presentation.ViewModels;

public abstract partial class StreamEndpointsViewModelBase : ObservableObject
{
    private readonly IMediator _mediator;
    private readonly INavigationService _navigationService;
    private readonly IUserDialogService _dialogService;
    private readonly IEndpointCatalogService _endpointCatalogService;
    private readonly EndpointType _streamType;
    private readonly Dictionary<Guid, bool> _lastKnownEnabledState = [];
    private readonly Dictionary<Guid, float> _lastKnownVolume = [];

    protected StreamEndpointsViewModelBase(
        IMediator mediator,
        INavigationService navigationService,
        IUserDialogService dialogService,
        IEndpointCatalogService endpointCatalogService,
        EndpointType streamType)
    {
        _mediator = mediator;
        _navigationService = navigationService;
        _dialogService = dialogService;
        _endpointCatalogService = endpointCatalogService;
        _streamType = streamType;

        ReloadEndpoints();
        _endpointCatalogService.EndpointsChanged += OnEndpointsChanged;
    }

    public ObservableCollection<StreamEndpointItemViewModel> Endpoints { get; } = [];

    [RelayCommand]
    private async Task OpenSettings(StreamEndpointItemViewModel endpoint)
    {
        await _navigationService.NavigateToAsync("StreamSettingsPage", new Dictionary<string, object>
        {
            { "EndpointId", endpoint.EndpointId },
            { "StreamType", endpoint.Type }
        });
    }

    [RelayCommand]
    private async Task DeleteStream(StreamEndpointItemViewModel endpoint)
    {
        if (endpoint == null)
        {
            return;
        }

        var result = await _mediator.Send(new DeleteStreamEndpointCommand(_streamType, endpoint.EndpointId));
        if (!result.IsSuccess)
        {
            await _dialogService.ShowErrorAsync(result.ErrorMessage ?? "Failed to delete stream endpoint.");
        }
    }

    [RelayCommand]
    private async Task AddStream()
    {
        await _navigationService.NavigateToAsync("AddStreamPage", new Dictionary<string, object>
        {
            { "StreamType", _streamType }
        });
    }

    [RelayCommand]
    private async Task StreamEnabledChanged(StreamEndpointItemViewModel endpoint)
    {
        if (endpoint == null)
        {
            return;
        }

        bool previousEnabledState = GetKnownEnabledState(endpoint);
        var result = await _mediator.Send(new UpdateStreamEndpointCommand(
            endpoint.Type,
            endpoint.EndpointId,
            IsEnabled: endpoint.IsEnabled));

        if (!result.IsSuccess)
        {
            endpoint.IsEnabled = previousEnabledState;
            await ShowUpdateErrorIfFailedAsync(result, _dialogService);
            return;
        }

        _lastKnownEnabledState[endpoint.EndpointId] = endpoint.IsEnabled;
    }

    [RelayCommand]
    private async Task StreamVolumeChanged(StreamEndpointItemViewModel endpoint)
    {
        if (endpoint == null)
        {
            return;
        }

        float previousVolume = GetKnownVolume(endpoint);
        var result = await _mediator.Send(new UpdateStreamEndpointCommand(
            endpoint.Type,
            endpoint.EndpointId,
            Volume: endpoint.Volume));

        if (!result.IsSuccess)
        {
            endpoint.Volume = previousVolume;
            await ShowUpdateErrorIfFailedAsync(result, _dialogService);
            return;
        }

        _lastKnownVolume[endpoint.EndpointId] = endpoint.Volume;
    }

    [RelayCommand]
    private async Task ResetVolume(StreamEndpointItemViewModel endpoint)
    {
        if (endpoint == null)
        {
            return;
        }

        float previousVolume = GetKnownVolume(endpoint);
        var result = await _mediator.Send(new UpdateStreamEndpointCommand(
            endpoint.Type,
            endpoint.EndpointId,
            Volume: 1f));

        if (!result.IsSuccess)
        {
            endpoint.Volume = previousVolume;
        }
        else
        {
            _lastKnownVolume[endpoint.EndpointId] = 1f;
        }

        await ShowUpdateErrorIfFailedAsync(result, _dialogService);
    }

    private void OnEndpointsChanged(EndpointType endpointType)
    {
        if (endpointType == _streamType)
        {
            ReloadEndpoints();
        }
    }

    private void ReloadEndpoints()
    {
        var endpoints = _endpointCatalogService.GetEndpoints(_streamType);
        Endpoints.Clear();
        _lastKnownEnabledState.Clear();
        _lastKnownVolume.Clear();

        for (int i = 0; i < endpoints.Count; i++)
        {
            var item = StreamEndpointItemViewModel.FromEndpoint(endpoints[i]);
            Endpoints.Add(item);
            _lastKnownEnabledState[item.EndpointId] = item.IsEnabled;
            _lastKnownVolume[item.EndpointId] = item.Volume;
        }
    }

    private bool GetKnownEnabledState(StreamEndpointItemViewModel endpoint)
    {
        if (_lastKnownEnabledState.TryGetValue(endpoint.EndpointId, out bool value))
        {
            return value;
        }

        _lastKnownEnabledState[endpoint.EndpointId] = endpoint.IsEnabled;
        return endpoint.IsEnabled;
    }

    private float GetKnownVolume(StreamEndpointItemViewModel endpoint)
    {
        if (_lastKnownVolume.TryGetValue(endpoint.EndpointId, out float value))
        {
            return value;
        }

        _lastKnownVolume[endpoint.EndpointId] = endpoint.Volume;
        return endpoint.Volume;
    }

    private static async Task ShowUpdateErrorIfFailedAsync(OperationResult result, IUserDialogService dialogService)
    {
        if (result.IsSuccess)
        {
            return;
        }

        await dialogService.ShowErrorAsync(result.ErrorMessage ?? "Failed to update stream endpoint.");
    }
}
