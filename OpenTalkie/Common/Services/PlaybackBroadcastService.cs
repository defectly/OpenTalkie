using AutoMapper;
using CommunityToolkit.Maui.Views;
using OpenTalkie.Common.Dto;
using OpenTalkie.Common.Enums;
using OpenTalkie.Common.Repositories.Interfaces;
using OpenTalkie.Common.Services.Interfaces;
using OpenTalkie.View.Popups;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;

namespace OpenTalkie.Common.Services;

public class PlaybackBroadcastService
{
    private readonly IMapper _mapper;
    private CancellationTokenSource? _cancellationTokenSource;
    private readonly AppShell _mainPage;
    private readonly IPlaybackService _playbackService;
    private readonly IEndpointRepository _endpointRepository;
    private AsyncSender? _asyncSender;
    public ObservableCollection<Endpoint> Endpoints;
    public bool BroadcastState { get; private set; }

    public PlaybackBroadcastService(AppShell mainPage, IPlaybackService playbackService, IEndpointRepository endpointRepository, IMapper mapper)
    {
        _mainPage = mainPage;
        _playbackService = playbackService;
        _endpointRepository = endpointRepository;
        _mapper = mapper;

        Endpoints = mapper.Map<ObservableCollection<Endpoint>>(_endpointRepository.List().Where(e => e.Type == EndpointType.Playback));
        Endpoints.CollectionChanged += EndpointsCollectionChanged;

        foreach (var endpoint in Endpoints)
            endpoint.PropertyChanged += EndpointPropertyChanged;
    }

    private void EndpointsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.Action == NotifyCollectionChangedAction.Remove)
        {
            if (e.OldItems != null)
            {
                foreach (Endpoint endpoint in e.OldItems)
                {
                    _endpointRepository.RemoveAsync(endpoint.Id);
                }
            }
        }
        else if (e.Action == NotifyCollectionChangedAction.Add)
        {
            if (e.NewItems != null)
            {
                foreach (Endpoint endpoint in e.NewItems)
                {
                    endpoint.PropertyChanged += EndpointPropertyChanged;
                    var endpointDto = _mapper.Map<EndpointDto>(endpoint);
                    _endpointRepository.CreateAsync(endpointDto);
                }
            }
        }
    }

    private void EndpointPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender == null)
            throw new NullReferenceException($"Got call from null endpoint");

        var endpoint = (Endpoint)sender;

        var endpointDto = _mapper.Map<EndpointDto>(endpoint);
        _endpointRepository.UpdateAsync(endpointDto);
    }

    public async Task<bool> RequestPermissionAsync()
    {
        return await _playbackService.RequestPermissionAsync();
    }

    public void Switch()
    {
        if (BroadcastState)
        {
            _cancellationTokenSource?.Cancel();
            BroadcastState = !BroadcastState;
            _playbackService.Stop();
            _asyncSender = null;
        }
        else
        {
            try
            {
                _playbackService.Start();
            }
            catch (Exception ex)
            {
                var errorPopup = new ErrorPopup(ex.Message);
                _mainPage.ShowPopupAsync(errorPopup);
                return;
            }

            _cancellationTokenSource = new();

            var thread = new Thread(() => _ = StartSendingLoopAsync(_cancellationTokenSource.Token))
            {
                IsBackground = true
            };

            thread.Start();
            BroadcastState = !BroadcastState;
        }
    }

    private async Task StartSendingLoopAsync(CancellationToken cancellationToken)
    {
        _asyncSender ??= new(_playbackService, Endpoints);

        byte[] vbanBuffer = new byte[_playbackService.GetBufferSize()];

        while (true)
        {
            if (cancellationToken.IsCancellationRequested)
                return;

            await _asyncSender.ReadAsync(vbanBuffer, 0, vbanBuffer.Length);
        }
    }
}
