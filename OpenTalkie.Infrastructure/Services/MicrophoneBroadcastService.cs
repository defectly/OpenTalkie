using OpenTalkie.Application.Abstractions.Services;
using OpenTalkie.Domain.Enums;
using OpenTalkie.Infrastructure.Streaming;
using System.Collections.ObjectModel;

namespace OpenTalkie.Infrastructure.Services;

public sealed class MicrophoneBroadcastService : IMicrophoneBroadcastService
{
    private readonly IMicrophoneCapturingService _microphoneService;
    private readonly IEndpointCatalogService _endpointCatalogService;
    private readonly ILogger<MicrophoneBroadcastService> _logger;
    private readonly ILoggerFactory _loggerFactory;
    private readonly ObservableCollection<Endpoint> _endpoints = [];
    private CancellationTokenSource? _cancellationTokenSource;
    private Task? _sendLoopTask;
    private AsyncSender? _asyncSender;
    private StreamSessionStatus _status = StreamSessionStatus.Stopped();

    public MicrophoneBroadcastService(
        IMicrophoneCapturingService microphoneService,
        IEndpointCatalogService endpointCatalogService,
        ILogger<MicrophoneBroadcastService> logger,
        ILoggerFactory loggerFactory)
    {
        _microphoneService = microphoneService;
        _endpointCatalogService = endpointCatalogService;
        _logger = logger;
        _loggerFactory = loggerFactory;
        SyncEndpoints();
        _endpointCatalogService.EndpointsChanged += OnEndpointsChanged;
    }

    public StreamSessionStatus Status => _status;

    public event Action<StreamSessionStatus>? StatusChanged;

    public async Task<OperationResult> SwitchAsync()
    {
        if (_status.Phase is StreamSessionPhase.Starting or StreamSessionPhase.Stopping)
        {
            if (_logger.IsEnabled(LogLevel.Warning))
                _logger.LogWarning("Microphone broadcast switch ignored while phase is {Phase}.", _status.Phase);

            return OperationResult.Fail("Microphone broadcast is already transitioning.");
        }

        if (_status.Phase == StreamSessionPhase.Running)
            return await StopAsync();

        return await StartAsync();
    }

    private async Task<OperationResult> StartAsync()
    {
        SetStatus(StreamSessionStatus.Starting());

        if (_logger.IsEnabled(LogLevel.Information))
            _logger.LogInformation("Starting microphone broadcast with {EndpointCount} endpoint(s).", _endpoints.Count);

        try
        {
            await _microphoneService.StartAsync();

            _cancellationTokenSource = new CancellationTokenSource();
            _asyncSender = new AsyncSender(_microphoneService, _endpoints, _loggerFactory);
            _sendLoopTask = Task.Run(() => StartSendingLoopAsync(_cancellationTokenSource.Token));

            SetStatus(StreamSessionStatus.Running());
            _logger.LogInformation("Microphone broadcast started.");
            return OperationResult.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Microphone broadcast failed to start.");

            try
            {
                _microphoneService.Stop();
            }
            catch (Exception stopEx)
            {
                _logger.LogWarning(stopEx, "Microphone cleanup after failed start failed.");
            }

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
        _logger.LogInformation("Stopping microphone broadcast.");

        _cancellationTokenSource?.Cancel();
        try
        {
            _microphoneService.Stop();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Microphone service stop failed.");
        }

        if (_sendLoopTask != null)
        {
            try
            {
                await _sendLoopTask;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Microphone send loop stopped with an error.");
            }
        }

        _asyncSender?.Dispose();
        _asyncSender = null;
        _cancellationTokenSource?.Dispose();
        _cancellationTokenSource = null;
        _sendLoopTask = null;

        SetStatus(StreamSessionStatus.Stopped());
        _logger.LogInformation("Microphone broadcast stopped.");
        return OperationResult.Success();
    }

    private async Task StartSendingLoopAsync(CancellationToken cancellationToken)
    {
        if (_asyncSender == null)
            return;

        try
        {
            var waveFormat = _microphoneService.GetWaveFormat();
            int bytesPerSample = waveFormat.BitsPerSample / 8;
            int chunkSize = 256 * bytesPerSample * waveFormat.Channels;
            int bufferSize = chunkSize * 4;
            byte[] vbanBuffer = new byte[bufferSize];
            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug(
                    "Microphone send loop started with {SampleRate} Hz, {BitsPerSample}-bit, {Channels} channel(s), {BufferSize} byte buffer.",
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
            if (cancellationToken.IsCancellationRequested)
            {
                return;
            }

            _logger.LogError(ex, "Microphone send loop failed.");
            try { _microphoneService.Stop(); } catch (Exception stopEx) { _logger.LogWarning(stopEx, "Microphone cleanup after send loop failure failed."); }
            SetStatus(StreamSessionStatus.Faulted(ex.Message));
        }
    }

    private void OnEndpointsChanged(EndpointType endpointType)
    {
        if (endpointType == EndpointType.Microphone)
        {
            SyncEndpoints();

            if (_logger.IsEnabled(LogLevel.Debug))
                _logger.LogDebug("Microphone endpoints changed; {EndpointCount} endpoint(s) configured.", _endpoints.Count);
        }
    }

    private void SyncEndpoints()
    {
        var endpoints = _endpointCatalogService.GetEndpoints(EndpointType.Microphone);
        _endpoints.Clear();

        for (int i = 0; i < endpoints.Count; i++)
            _endpoints.Add(endpoints[i]);
    }

    private void SetStatus(StreamSessionStatus status)
    {
        _status = status;

        if (_logger.IsEnabled(LogLevel.Debug))
            _logger.LogDebug("Microphone broadcast status changed to {Phase}.", status.Phase);

        StatusChanged?.Invoke(status);
    }
}
