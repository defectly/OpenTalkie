using OpenTalkie.Application.Abstractions.Services;
using OpenTalkie.Domain.Enums;
using OpenTalkie.Infrastructure.Streaming;
using System.Collections.ObjectModel;

namespace OpenTalkie.Infrastructure.Services;

public sealed class PlaybackBroadcastService : IPlaybackBroadcastService
{
    private readonly IPlaybackService _playbackService;
    private readonly IEndpointCatalogService _endpointCatalogService;
    private readonly ObservableCollection<Endpoint> _endpoints = [];
    private CancellationTokenSource? _cancellationTokenSource;
    private Task? _sendLoopTask;
    private AsyncSender? _asyncSender;
    private StreamSessionStatus _status = StreamSessionStatus.Stopped();

    public PlaybackBroadcastService(IPlaybackService playbackService, IEndpointCatalogService endpointCatalogService)
    {
        _playbackService = playbackService;
        _endpointCatalogService = endpointCatalogService;
        SyncEndpoints();
        _endpointCatalogService.EndpointsChanged += OnEndpointsChanged;
    }

    public StreamSessionStatus Status => _status;

    public event Action<StreamSessionStatus>? StatusChanged;

    public async Task<bool> RequestPermissionAsync()
    {
        return await _playbackService.RequestPermissionAsync();
    }

    public OperationResult Switch()
    {
        if (_status.Phase is StreamSessionPhase.Starting or StreamSessionPhase.Stopping)
        {
            return OperationResult.Fail("Playback broadcast is already transitioning.");
        }

        return _status.Phase == StreamSessionPhase.Running
            ? Stop()
            : Start();
    }

    private OperationResult Start()
    {
        SetStatus(StreamSessionStatus.Starting());

        try
        {
            _playbackService.Start();
            _cancellationTokenSource = new CancellationTokenSource();
            _asyncSender = new AsyncSender(_playbackService, _endpoints);
            _sendLoopTask = Task.Run(() => StartSendingLoopAsync(_cancellationTokenSource.Token));
            SetStatus(StreamSessionStatus.Running());
            return OperationResult.Success();
        }
        catch (Exception ex)
        {
            try { _playbackService.Stop(); } catch { }
            _asyncSender?.Dispose();
            _asyncSender = null;
            _cancellationTokenSource?.Dispose();
            _cancellationTokenSource = null;
            SetStatus(StreamSessionStatus.Faulted(ex.Message));
            return OperationResult.Fail(ex.Message);
        }
    }

    private OperationResult Stop()
    {
        SetStatus(StreamSessionStatus.Stopping());

        _cancellationTokenSource?.Cancel();
        _playbackService.Stop();
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
            var waveFormat = _playbackService.GetWaveFormat();
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
            try { _playbackService.Stop(); } catch { }
            SetStatus(StreamSessionStatus.Faulted(ex.Message));
        }
    }

    private void OnEndpointsChanged(EndpointType endpointType)
    {
        if (endpointType == EndpointType.Playback)
        {
            SyncEndpoints();
        }
    }

    private void SyncEndpoints()
    {
        var endpoints = _endpointCatalogService.GetEndpoints(EndpointType.Playback);
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
