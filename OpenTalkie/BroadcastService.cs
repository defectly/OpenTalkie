using OpenTalkie.Repositories;
using System.Collections.Concurrent;

namespace OpenTalkie;

public class BroadcastService
{
    private CancellationTokenSource _cancellationTokenSource;
    private readonly IMicrophoneService _microphoneService;
    private readonly EndpointRepository _endpointRepository;

    public List<Endpoint> Endpoints => _endpointRepository.Endpoints;
    public bool BroadcastState { get; private set; }

    public BroadcastService(IMicrophoneService microphoneService, EndpointRepository endpointRepository)
    {
        _microphoneService = microphoneService;
        _endpointRepository = endpointRepository;
    }

    public void Switch()
    {
        if (BroadcastState)
        {
            _cancellationTokenSource.Cancel();
            BroadcastState = !BroadcastState;
        }
        else
        {
            _cancellationTokenSource = new();
            Task.Run(() => StartSendingLoop(_cancellationTokenSource.Token));
            BroadcastState = !BroadcastState;
        }
    }

    private void StartSendingLoop(CancellationToken cancellationToken)
    {
        Sender sender = new(_microphoneService.ToSampleProvider(), _endpointRepository.Endpoints);

        float[] vbanBuffer = new float[_microphoneService.BufferSize / 2];

        while (true)
        {
            if (cancellationToken.IsCancellationRequested)
                return;

            sender.Read(vbanBuffer, 0, vbanBuffer.Length);
        }
    }
}
