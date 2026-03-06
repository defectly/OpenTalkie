using Mediator;
using OpenTalkie.Application.Abstractions.Repositories;

namespace OpenTalkie.Application.Settings.Queries;

public readonly record struct GetReceiverSettingsQuery : IQuery<ReceiverSettingsState>;

public sealed class GetReceiverSettingsQueryHandler(IReceiverRepository repository)
    : IQueryHandler<GetReceiverSettingsQuery, ReceiverSettingsState>
{
    public ValueTask<ReceiverSettingsState> Handle(
        GetReceiverSettingsQuery query,
        CancellationToken cancellationToken)
    {
        return ValueTask.FromResult(repository.GetSettings());
    }
}
