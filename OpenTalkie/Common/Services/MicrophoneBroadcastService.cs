using AutoMapper;
using OpenTalkie.Common.Dto;
using OpenTalkie.Common.Enums;
using OpenTalkie.Common.Repositories.Interfaces;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using Microsoft.Maui.ApplicationModel;
using OpenTalkie.Common.Services.Interfaces;

namespace OpenTalkie.Common.Services;

public class MicrophoneBroadcastService
{
    private readonly IMapper _mapper;
    private CancellationTokenSource _cancellationTokenSource;
    private readonly IMicrophoneService _microphoneService;
    private readonly IEndpointRepository _endpointRepository;
    private AsyncSender? _asyncSender;
    public ObservableCollection<Endpoint> Endpoints;
    public bool BroadcastState { get; private set; }

    public MicrophoneBroadcastService(IMicrophoneService microphoneService, IEndpointRepository endpointRepository, IMapper mapper)
    {
        _microphoneService = microphoneService;
        _endpointRepository = endpointRepository;
        _mapper = mapper;

        Endpoints = mapper.Map<ObservableCollection<Endpoint>>(_endpointRepository.List().Where(e => e.Type == EndpointType.Microphone));
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

    public void Switch()
    {
        if (BroadcastState)
        {
            _cancellationTokenSource.Cancel();
            BroadcastState = !BroadcastState;
            _microphoneService.Stop();
            _asyncSender = null;
        }
        else
        {
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
        _microphoneService.Start();

        _asyncSender ??= new(_microphoneService, Endpoints);

        byte[] vbanBuffer = new byte[_microphoneService.BufferSize];

        while (true)
        {
            if (cancellationToken.IsCancellationRequested)
                return;

            await _asyncSender.ReadAsync(vbanBuffer, 0, vbanBuffer.Length);
        }
    }
}
