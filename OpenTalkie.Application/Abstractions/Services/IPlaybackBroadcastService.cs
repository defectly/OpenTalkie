using OpenTalkie.Application.Streams;

namespace OpenTalkie.Application.Abstractions.Services;

public interface IPlaybackBroadcastService
{
    StreamSessionStatus Status { get; }
    event Action<StreamSessionStatus>? StatusChanged;
    Task<bool> RequestPermissionAsync();
    OperationResult Switch();
}
