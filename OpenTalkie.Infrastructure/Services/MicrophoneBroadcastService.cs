using OpenTalkie.Application.Abstractions.Services;
using OpenTalkie.Domain.Enums;
using OpenTalkie.Infrastructure.Streaming;
using System.Collections.ObjectModel;

namespace OpenTalkie.Infrastructure.Services;

public sealed class MicrophoneBroadcastService : IMicrophoneBroadcastService
{
    private readonly IMicrophoneCapturingService _microphoneService;
    private readonly IEndpointCatalogService _endpointCatalogService;
    private readonly ObservableCollection<Endpoint> _endpoints = [];
    private CancellationTokenSource? _cancellationTokenSource;
    private Task? _sendLoopTask;
    private AsyncSender? _asyncSender;
    private StreamSessionStatus _status = StreamSessionStatus.Stopped();

    public MicrophoneBroadcastService(
        IMicrophoneCapturingService microphoneService,
        IEndpointCatalogService endpointCatalogService)
    {
        _microphoneService = microphoneService;
        _endpointCatalogService = endpointCatalogService;
        SyncEndpoints();
        _endpointCatalogService.EndpointsChanged += OnEndpointsChanged;
    }

    public StreamSessionStatus Status => _status;

    public event Action<StreamSessionStatus>? StatusChanged;

    public async Task<OperationResult> SwitchAsync()
    {
        if (_status.Phase is StreamSessionPhase.Starting or StreamSessionPhase.Stopping)
        {
            return OperationResult.Fail("Microphone broadcast is already transitioning.");
        }

        if (_status.Phase == StreamSessionPhase.Running)
        {
            return await StopAsync();
        }

        return await StartAsync();
    }

    private async Task<OperationResult> StartAsync()
    {
        SetStatus(StreamSessionStatus.Starting());

        try
        {
            await _microphoneService.StartAsync();

            _cancellationTokenSource = new CancellationTokenSource();
            _asyncSender = new AsyncSender(_microphoneService, _endpoints);
            _sendLoopTask = Task.Run(() => StartSendingLoopAsync(_cancellationTokenSource.Token));

            SetStatus(StreamSessionStatus.Running());
            return OperationResult.Success();
        }
        catch (Exception ex)
        {
            try { _microphoneService.Stop(); } catch { }
            _asyncSender?.Dispose();
            _asyncSender = null;
            _cancellationTokenSource?.Dispose();
            _cancellationTokenSource = null;
            SetStatus(StreamSessionStatus.Faulted(ex.Message));
            return OperationResult.Fail(ex.Message);
        }
    }

    private async Task<OperationResult> StopAsync()
    {
        SetStatus(StreamSessionStatus.Stopping());

        _cancellationTokenSource?.Cancel();
        try { _microphoneService.Stop(); } catch { }

        if (_sendLoopTask != null)
        {
            try { await _sendLoopTask; } catch { }
        }

        _asyncSender?.Dispose();
        _asyncSender = null;
        _cancellationTokenSource?.Dispose();
        _cancellationTokenSource = null;
        _sendLoopTask = null;

        SetStatus(StreamSessionStatus.Stopped());
        return OperationResult.Success();
    }

    private async Task StartSendingLoopAsync(CancellationToken cancellationToken)
    {
        if (_asyncSender == null)
        {
            return;
        }

        try
        {
            var waveFormat = _microphoneService.GetWaveFormat();
            int bytesPerSample = waveFormat.BitsPerSample / 8;
            int chunkSize = 256 * bytesPerSample * waveFormat.Channels;
            int bufferSize = chunkSize * 4;
            byte[] vbanBuffer = new byte[bufferSize];

            while (!cancellationToken.IsCancellationRequested)
            {
                await _asyncSender.ReadAsync(vbanBuffer, 0, vbanBuffer.Length);
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return;
            }

            try { _microphoneService.Stop(); } catch { }
            SetStatus(StreamSessionStatus.Faulted(ex.Message));
        }
    }

    private void OnEndpointsChanged(EndpointType endpointType)
    {
        if (endpointType == EndpointType.Microphone)
        {
            SyncEndpoints();
        }
    }

    private void SyncEndpoints()
    {
        var endpoints = _endpointCatalogService.GetEndpoints(EndpointType.Microphone);
        _endpoints.Clear();
        for (int i = 0; i < endpoints.Count; i++)
        {
            _endpoints.Add(endpoints[i]);
        }
    }

    private void SetStatus(StreamSessionStatus status)
    {
        _status = status;
        StatusChanged?.Invoke(status);
    }
}
