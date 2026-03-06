using Mediator;
using OpenTalkie.Application.Abstractions.Repositories;

namespace OpenTalkie.Application.Settings.Queries;

public readonly record struct GetMicrophoneSettingsQuery : IQuery<MicrophoneSettingsState>;

public sealed class GetMicrophoneSettingsQueryHandler(IMicrophoneRepository repository)
    : IQueryHandler<GetMicrophoneSettingsQuery, MicrophoneSettingsState>
{
    public ValueTask<MicrophoneSettingsState> Handle(
        GetMicrophoneSettingsQuery query,
        CancellationToken cancellationToken)
    {
        return ValueTask.FromResult(repository.GetSettings());
    }
}
