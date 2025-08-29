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

public class MicrophoneBroadcastService
{
    private readonly IMapper _mapper;
    private CancellationTokenSource? _cancellationTokenSource;
    private readonly IMicrophoneCapturingService _microphoneService;
    private readonly IEndpointRepository _endpointRepository;
    private AsyncSender? _asyncSender;
    private readonly AppShell _mainPage;
    public ObservableCollection<Endpoint> Endpoints;
    public bool BroadcastState { get; private set; }
    public Action<bool>? BroadcastStateChanged;

    public MicrophoneBroadcastService(IMicrophoneCapturingService microphoneService, IEndpointRepository endpointRepository,
        IMapper mapper, AppShell mainPage)
    {
        _mainPage = mainPage;
        _microphoneService = microphoneService;
        _endpointRepository = endpointRepository;
        _mapper = mapper;

        Endpoints = mapper.Map<ObservableCollection<Endpoint>>(_endpointRepository.List().Where(e => e.Type == EndpointType.Microphone));
        Endpoints.CollectionChanged += EndpointsCollectionChanged;

        for (int i = 0; i < Endpoints.Count; i++)
            Endpoints[i].PropertyChanged += EndpointPropertyChanged;

        BroadcastStateChanged += OnBroadcastStateChange;
    }

    public async Task<bool> Switch()
    {
        if (BroadcastState)
        {
            _cancellationTokenSource?.Cancel();
            BroadcastStateChanged?.Invoke(!BroadcastState);
            _microphoneService.Stop();
            _asyncSender = null;
            return true;
        }
        else
        {
            try
            {
                bool isPermissionGranted = await _microphoneService.StartAsync();

                if (!isPermissionGranted)
                    return false;
            }
            catch (Exception ex)
            {
                var errorPopup = new ErrorPopup(ex.Message);
                _ = _mainPage.ShowPopupAsync(errorPopup);
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
        _asyncSender ??= new(_microphoneService, Endpoints);

        byte[] vbanBuffer = new byte[_microphoneService.GetBufferSize()];

        while (true)
        {
            if (cancellationToken.IsCancellationRequested)
                return;

            await _asyncSender.ReadAsync(vbanBuffer, 0, vbanBuffer.Length);
        }
    }

    private void OnBroadcastStateChange(bool isActive) => BroadcastState = isActive;

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
}
