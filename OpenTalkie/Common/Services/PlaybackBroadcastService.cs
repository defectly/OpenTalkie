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
    private readonly IPlaybackService _playbackService;
    private readonly IEndpointRepository _endpointRepository;
    private AsyncSender? _asyncSender;
    public ObservableCollection<Endpoint> Endpoints;
    public bool BroadcastState { get; private set; }
    public Action<bool> BroadcastStateChanged;

    public PlaybackBroadcastService(IPlaybackService playbackService, IEndpointRepository endpointRepository, IMapper mapper)
    {
        _playbackService = playbackService;
        _endpointRepository = endpointRepository;
        _mapper = mapper;

        Endpoints = mapper.Map<ObservableCollection<Endpoint>>(_endpointRepository.List().Where(e => e.Type == EndpointType.Playback));
        Endpoints.CollectionChanged += EndpointsCollectionChanged;

        for (int i = 0; i < Endpoints.Count; i++)
            Endpoints[i].PropertyChanged += EndpointPropertyChanged;

        BroadcastStateChanged += OnBroadcastStateChange;
    }

    private void EndpointsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.Action == NotifyCollectionChangedAction.Remove)
        {
            if (e.OldItems != null)
            {
                for (int i = 0; i < e.OldItems.Count; i++)
                {
                    var endpoint = (Endpoint)e.OldItems[i]!;
                    _endpointRepository.RemoveAsync(endpoint.Id);
                }
            }
        }
        else if (e.Action == NotifyCollectionChangedAction.Add)
        {
            if (e.NewItems != null)
            {
                for (int i = 0; i < e.NewItems.Count; i++)
                {
                    var endpoint = (Endpoint)e.NewItems[i]!;
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

    public bool Switch()
    {
        if (BroadcastState)
        {
            _cancellationTokenSource?.Cancel();
            BroadcastStateChanged?.Invoke(!BroadcastState);
            _playbackService.Stop();
            _asyncSender = null;
            return true;
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
                Application.Current.MainPage.ShowPopupAsync(errorPopup);
                return false;
            }

            _cancellationTokenSource = new();

            var thread = new Thread(() => _ = StartSendingLoopAsync(_cancellationTokenSource.Token))
            {
                IsBackground = true
            };

            thread.Start();
            BroadcastStateChanged?.Invoke(!BroadcastState);
            return true;
        }
    }

    private async Task StartSendingLoopAsync(CancellationToken cancellationToken)
    {
        _asyncSender ??= new(_playbackService, Endpoints);

        var waveFormat = _playbackService.GetWaveFormat();
        int bytesPerSample = waveFormat.BitsPerSample / 8;
        int chunkSize = 256 * bytesPerSample * waveFormat.Channels; // VBAN chunk size
        int bufferSize = chunkSize * 4; // 4 chunks per read
        byte[] vbanBuffer = new byte[bufferSize];

        while (true)
        {
            if (cancellationToken.IsCancellationRequested)
                return;

            await _asyncSender.ReadAsync(vbanBuffer, 0, vbanBuffer.Length);
        }
    }
    private void OnBroadcastStateChange(bool isActive) => BroadcastState = isActive;
}
