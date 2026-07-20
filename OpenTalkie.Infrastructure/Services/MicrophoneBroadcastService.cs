using OpenTalkie.Application.Abstractions.Repositories;
using OpenTalkie.Application.Abstractions.Services;
using OpenTalkie.Domain.Enums;
using OpenTalkie.Infrastructure.Streaming;
using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Diagnostics;

namespace OpenTalkie.Infrastructure.Services;

public sealed class MicrophoneBroadcastService : IMicrophoneBroadcastService
{
    private const int PrebufferMilliseconds = 120;
    private const int BufferQueueCapacity = 64;

    private readonly IMicrophoneCapturingService _microphoneService;
    private readonly IEndpointCatalogService _endpointCatalogService;
    private readonly IMicrophoneRepository _microphoneRepository;
    private readonly ObservableCollection<Endpoint> _endpoints = [];
    private CancellationTokenSource? _cancellationTokenSource;
    private BlockingCollection<CapturedAudioBuffer>? _audioBuffers;
    private ManualResetEventSlim? _prebufferReady;
    private Task? _captureLoopTask;
    private Task? _sendLoopTask;
    private AsyncSender? _asyncSender;
    private int _queuedBytes;
    private StreamSessionStatus _status = StreamSessionStatus.Stopped();

    public MicrophoneBroadcastService(
        IMicrophoneCapturingService microphoneService,
        IEndpointCatalogService endpointCatalogService,
        IMicrophoneRepository microphoneRepository)
    {
        _microphoneService = microphoneService;
        _endpointCatalogService = endpointCatalogService;
        _microphoneRepository = microphoneRepository;
        SyncEndpoints();
        _endpointCatalogService.EndpointsChanged += OnEndpointsChanged;
    }

    public StreamSessionStatus Status => _status;

    public event Action<StreamSessionStatus>? StatusChanged;

    public async Task<OperationResult> SwitchAsync()
    {
        if (_status.Phase is StreamSessionPhase.Starting or StreamSessionPhase.Stopping)
            return OperationResult.Fail("Microphone broadcast is already transitioning.");

        if (_status.Phase == StreamSessionPhase.Running)
            return await StopAsync();

        return await StartAsync();
    }

    private async Task<OperationResult> StartAsync()
    {
        SetStatus(StreamSessionStatus.Starting());

        try
        {
            await _microphoneService.StartAsync();

            var waveFormat = _microphoneService.GetWaveFormat();
            int bytesPerSample = waveFormat.BitsPerSample / 8;
            int bytesPerSecond = waveFormat.SampleRate * waveFormat.Channels * bytesPerSample;
            int bufferSize = 256 * bytesPerSample * waveFormat.Channels * 4;
            int prebufferBytes = bytesPerSecond * PrebufferMilliseconds / 1000;
            bool isPacingEnabled = _microphoneRepository.GetSettings().IsPacingEnabled;

            _cancellationTokenSource = new CancellationTokenSource();
            _queuedBytes = 0;
            _asyncSender = new AsyncSender(_microphoneService, _endpoints);

            var cancellationToken = _cancellationTokenSource.Token;
            if (isPacingEnabled)
            {
                _audioBuffers = new BlockingCollection<CapturedAudioBuffer>(BufferQueueCapacity);
                _prebufferReady = new ManualResetEventSlim(false);
                _captureLoopTask = StartLongRunningTask(
                    () => CaptureLoop(bufferSize, prebufferBytes, cancellationToken));
                _sendLoopTask = StartLongRunningTask(
                    () => SendLoop(bytesPerSecond, cancellationToken));
            }
            else
            {
                _sendLoopTask = StartLongRunningTask(
                    () => DirectSendLoop(bufferSize, cancellationToken));
            }

            SetStatus(StreamSessionStatus.Running());
            return OperationResult.Success();
        }
        catch (Exception ex)
        {
            try { _microphoneService.Stop(); } catch { }
            DisposePipeline();
            SetStatus(StreamSessionStatus.Faulted(ex.Message));
            return OperationResult.Fail(ex.Message);
        }
    }

    private async Task<OperationResult> StopAsync()
    {
        SetStatus(StreamSessionStatus.Stopping());
        _cancellationTokenSource?.Cancel();

        try { _microphoneService.Stop(); } catch { }
        try { _audioBuffers?.CompleteAdding(); } catch { }

        var tasks = new[] { _captureLoopTask, _sendLoopTask }
            .Where(task => task != null)
            .Cast<Task>()
            .ToArray();

        if (tasks.Length > 0)
        {
            try { await Task.WhenAll(tasks); } catch { }
        }

        DisposePipeline();
        SetStatus(StreamSessionStatus.Stopped());
        return OperationResult.Success();
    }

    private void CaptureLoop(int bufferSize, int prebufferBytes, CancellationToken cancellationToken)
    {
        byte[]? rentedBuffer = null;

        try
        {
            _microphoneService.ConfigureCurrentThreadPriority();

            while (!cancellationToken.IsCancellationRequested)
            {
                rentedBuffer = ArrayPool<byte>.Shared.Rent(bufferSize);
                int bytesRead = _microphoneService
                    .ReadAsync(rentedBuffer, 0, bufferSize)
                    .GetAwaiter()
                    .GetResult();

                if (bytesRead <= 0)
                {
                    ArrayPool<byte>.Shared.Return(rentedBuffer);
                    rentedBuffer = null;

                    if (bytesRead < 0)
                        throw new IOException($"Microphone read failed with code {bytesRead}.");

                    continue;
                }

                int queuedBytes = Interlocked.Add(ref _queuedBytes, bytesRead);
                try
                {
                    _audioBuffers!.Add(new CapturedAudioBuffer(rentedBuffer, bytesRead), cancellationToken);
                }
                catch
                {
                    Interlocked.Add(ref _queuedBytes, -bytesRead);
                    throw;
                }

                rentedBuffer = null;
                if (queuedBytes >= prebufferBytes)
                    _prebufferReady!.Set();
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            HandleLoopFault(ex, cancellationToken);
        }
        finally
        {
            if (rentedBuffer != null)
                ArrayPool<byte>.Shared.Return(rentedBuffer);

            try { _audioBuffers?.CompleteAdding(); } catch { }
        }
    }

    private void DirectSendLoop(int bufferSize, CancellationToken cancellationToken)
    {
        byte[]? rentedBuffer = null;

        try
        {
            _microphoneService.ConfigureCurrentThreadPriority();
            rentedBuffer = ArrayPool<byte>.Shared.Rent(bufferSize);

            while (!cancellationToken.IsCancellationRequested)
            {
                _asyncSender!
                    .ReadAsync(rentedBuffer, 0, bufferSize)
                    .GetAwaiter()
                    .GetResult();
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            HandleLoopFault(ex, cancellationToken);
        }
        finally
        {
            if (rentedBuffer != null)
                ArrayPool<byte>.Shared.Return(rentedBuffer);
        }
    }

    private void SendLoop(int bytesPerSecond, CancellationToken cancellationToken)
    {
        try
        {
            _microphoneService.ConfigureCurrentThreadPriority();
            _prebufferReady!.Wait(cancellationToken);

            long nextSendTimestamp = Stopwatch.GetTimestamp();

            while (!cancellationToken.IsCancellationRequested && !_audioBuffers!.IsCompleted)
            {
                bool queueWasEmpty = Volatile.Read(ref _queuedBytes) <= 0;
                CapturedAudioBuffer capturedBuffer;

                try
                {
                    capturedBuffer = _audioBuffers.Take(cancellationToken);
                }
                catch (InvalidOperationException)
                {
                    break;
                }

                Interlocked.Add(ref _queuedBytes, -capturedBuffer.Count);

                try
                {
                    if (queueWasEmpty)
                        nextSendTimestamp = Stopwatch.GetTimestamp();

                    WaitUntil(nextSendTimestamp, cancellationToken);
                    _asyncSender!
                        .ProcessAsync(capturedBuffer.Buffer, 0, capturedBuffer.Count)
                        .GetAwaiter()
                        .GetResult();

                    nextSendTimestamp += (long)(
                        capturedBuffer.Count * (double)Stopwatch.Frequency / bytesPerSecond);
                }
                finally
                {
                    ArrayPool<byte>.Shared.Return(capturedBuffer.Buffer);
                }
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            HandleLoopFault(ex, cancellationToken);
        }
    }

    private void HandleLoopFault(Exception exception, CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
            return;

        _cancellationTokenSource?.Cancel();
        try { _microphoneService.Stop(); } catch { }
        SetStatus(StreamSessionStatus.Faulted(exception.Message));
    }

    private void DisposePipeline()
    {
        if (_audioBuffers != null)
        {
            while (_audioBuffers.TryTake(out var capturedBuffer))
                ArrayPool<byte>.Shared.Return(capturedBuffer.Buffer);

            _audioBuffers.Dispose();
        }

        _prebufferReady?.Dispose();
        _asyncSender?.Dispose();
        _cancellationTokenSource?.Dispose();

        _audioBuffers = null;
        _prebufferReady = null;
        _asyncSender = null;
        _cancellationTokenSource = null;
        _captureLoopTask = null;
        _sendLoopTask = null;
        _queuedBytes = 0;
    }

    private static Task StartLongRunningTask(Action action) =>
        Task.Factory.StartNew(
            action,
            CancellationToken.None,
            TaskCreationOptions.LongRunning | TaskCreationOptions.DenyChildAttach,
            TaskScheduler.Default);

    private static void WaitUntil(long targetTimestamp, CancellationToken cancellationToken)
    {
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            long remainingTicks = targetTimestamp - Stopwatch.GetTimestamp();

            if (remainingTicks <= 0)
                return;

            double remainingMilliseconds = remainingTicks * 1000d / Stopwatch.Frequency;
            if (remainingMilliseconds > 2)
                Thread.Sleep(Math.Max(1, (int)remainingMilliseconds - 1));
            else
                Thread.SpinWait(64);
        }
    }

    private void OnEndpointsChanged(EndpointType endpointType)
    {
        if (endpointType == EndpointType.Microphone)
            SyncEndpoints();
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
        StatusChanged?.Invoke(status);
    }

    private readonly record struct CapturedAudioBuffer(byte[] Buffer, int Count);
}
