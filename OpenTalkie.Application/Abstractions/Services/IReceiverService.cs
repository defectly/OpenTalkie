using OpenTalkie.Application.Streams;

namespace OpenTalkie.Application.Abstractions.Services;

public interface IReceiverService
{
    StreamSessionStatus Status { get; }
    event Action<StreamSessionStatus>? StatusChanged;
    void Start();
    void Stop();
    void Switch();
}
