using OpenTalkie.Application.Streams;

namespace OpenTalkie.Application.Abstractions.Services;

public interface IMicrophoneBroadcastService
{
    StreamSessionStatus Status { get; }
    event Action<StreamSessionStatus>? StatusChanged;
    Task<OperationResult> SwitchAsync();
}
