using OpenTalkie.Application.Abstractions.Services;
using OpenTalkie.Domain.Enums;
using OpenTalkie.Infrastructure.Streaming;
using System.Collections.ObjectModel;

namespace OpenTalkie.Infrastructure.Services;

public sealed class PlaybackBroadcastService : IPlaybackBroadcastService
{
    private readonly IPlaybackService _playbackService;
    private readonly IEndpointCatalogService _endpointCatalogService;
    private readonly ILogger<PlaybackBroadcastService> _logger;
    private readonly ILoggerFactory _loggerFactory;
    private readonly ObservableCollection<Endpoint> _endpoints = [];
    private CancellationTokenSource? _cancellationTokenSource;
    private Task? _sendLoopTask;
    private AsyncSender? _asyncSender;
    private StreamSessionStatus _status = StreamSessionStatus.Stopped();

    public PlaybackBroadcastService(
        IPlaybackService playbackService,
        IEndpointCatalogService endpointCatalogService,
        ILogger<PlaybackBroadcastService> logger,
        ILoggerFactory loggerFactory)
    {
        _playbackService = playbackService;
        _endpointCatalogService = endpointCatalogService;
        _logger = logger;
        _loggerFactory = loggerFactory;
        SyncEndpoints();
        _endpointCatalogService.EndpointsChanged += OnEndpointsChanged;
    }

    public StreamSessionStatus Status => _status;

    public event Action<StreamSessionStatus>? StatusChanged;

    public async Task<bool> RequestPermissionAsync()
    {
        _logger.LogInformation("Requesting playback capture permission.");
        var granted = await _playbackService.RequestPermissionAsync();
        var logLevel = granted ? LogLevel.Information : LogLevel.Warning;

        if (_logger.IsEnabled(logLevel))
            _logger.Log(logLevel, "Playback capture permission {Result}.", granted ? "granted" : "denied");

        return granted;
    }

    public OperationResult Switch()
    {
        if (_status.Phase is StreamSessionPhase.Starting or StreamSessionPhase.Stopping)
        {
            if (_logger.IsEnabled(LogLevel.Warning))
                _logger.LogWarning("Playback broadcast switch ignored while phase is {Phase}.", _status.Phase);

            return OperationResult.Fail("Playback broadcast is already transitioning.");
        }

        return _status.Phase == StreamSessionPhase.Running
            ? Stop()
            : Start();
    }

    private OperationResult Start()
    {
        SetStatus(StreamSessionStatus.Starting());

        if (_logger.IsEnabled(LogLevel.Information))
            _logger.LogInformation("Starting playback broadcast with {EndpointCount} endpoint(s).", _endpoints.Count);

        try
        {
            _playbackService.Start();
            _cancellationTokenSource = new CancellationTokenSource();
            _asyncSender = new AsyncSender(_playbackService, _endpoints, _loggerFactory);
            _sendLoopTask = Task.Run(() => StartSendingLoopAsync(_cancellationTokenSource.Token));
            SetStatus(StreamSessionStatus.Running());
            _logger.LogInformation("Playback broadcast started.");
            return OperationResult.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Playback broadcast failed to start.");
            try { _playbackService.Stop(); } catch (Exception stopEx) { _logger.LogWarning(stopEx, "Playback cleanup after failed start failed."); }
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
        _logger.LogInformation("Stopping playback broadcast.");

        _cancellationTokenSource?.Cancel();
        _playbackService.Stop();
        _asyncSender?.Dispose();
        _asyncSender = null;
        _cancellationTokenSource?.Dispose();
        _cancellationTokenSource = null;
        _sendLoopTask = null;

        SetStatus(StreamSessionStatus.Stopped());
        _logger.LogInformation("Playback broadcast stopped.");
        return OperationResult.Success();
    }

    private async Task StartSendingLoopAsync(CancellationToken cancellationToken)
    {
        if (_asyncSender == null)
            return;

        try
        {
            var waveFormat = _playbackService.GetWaveFormat();
            int bytesPerSample = waveFormat.BitsPerSample / 8;
            int chunkSize = 256 * bytesPerSample * waveFormat.Channels;
            int bufferSize = chunkSize * 4;
            byte[] vbanBuffer = new byte[bufferSize];

            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug(
                    "Playback send loop started with {SampleRate} Hz, {BitsPerSample}-bit, {Channels} channel(s), {BufferSize} byte buffer.",
                    waveFormat.SampleRate,
                    waveFormat.BitsPerSample,
                    waveFormat.Channels,
                    bufferSize);
            }

            while (!cancellationToken.IsCancellationRequested)
                await _asyncSender.ReadAsync(vbanBuffer, 0, vbanBuffer.Length);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Playback send loop failed.");
            try { _playbackService.Stop(); } catch (Exception stopEx) { _logger.LogWarning(stopEx, "Playback cleanup after send loop failure failed."); }
            SetStatus(StreamSessionStatus.Faulted(ex.Message));
        }
    }

    private void OnEndpointsChanged(EndpointType endpointType)
    {
        if (endpointType == EndpointType.Playback)
        {
            SyncEndpoints();

            if (_logger.IsEnabled(LogLevel.Debug))
                _logger.LogDebug("Playback endpoints changed; {EndpointCount} endpoint(s) configured.", _endpoints.Count);
        }
    }

    private void SyncEndpoints()
    {
        var endpoints = _endpointCatalogService.GetEndpoints(EndpointType.Playback);
        _endpoints.Clear();

        for (int i = 0; i < endpoints.Count; i++)
            _endpoints.Add(endpoints[i]);
    }

    private void SetStatus(StreamSessionStatus status)
    {
        _status = status;

        if (_logger.IsEnabled(LogLevel.Debug))
            _logger.LogDebug("Playback broadcast status changed to {Phase}.", status.Phase);

        StatusChanged?.Invoke(status);
    }
}
