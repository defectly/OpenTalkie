using AutoMapper;
using OpenTalkie.Common.Dto;
using OpenTalkie.Common.Enums;
using OpenTalkie.Common.Repositories.Interfaces;
using OpenTalkie.Common.Services.Interfaces;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;

namespace OpenTalkie.Common.Services;

public class PlaybackBroadcastService
{
    private readonly IMapper _mapper;
    private CancellationTokenSource _cancellationTokenSource;
    private readonly IPlaybackService _playbackService;
    private readonly IEndpointRepository _endpointRepository;
    public ObservableCollection<Endpoint> Endpoints;
    public bool BroadcastState { get; private set; }

    public PlaybackBroadcastService(IPlaybackService playbackService, IEndpointRepository endpointRepository, IMapper mapper)
    {
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
            _cancellationTokenSource.Cancel();
            _playbackService.Stop();
            BroadcastState = !BroadcastState;
        }
        else
        {
            _playbackService.Start();
            _cancellationTokenSource = new();

            var thread = new Thread(() => StartSendingLoop(_cancellationTokenSource.Token))
            {
                IsBackground = true
            };

            thread.Start();
            BroadcastState = !BroadcastState;
        }
    }

    private void StartSendingLoop(CancellationToken cancellationToken)
    {
        var sampleProvider = _playbackService.ToSampleProvider();
        Sender sender = new(sampleProvider, Endpoints);

        float[] vbanBuffer = new float[_playbackService.GetBufferSize() / 2];

        while (true)
        {
            if (cancellationToken.IsCancellationRequested)
                return;

            sender.Read(vbanBuffer, 0, vbanBuffer.Length);
        }
    }
}
