using Mediator;
using OpenTalkie.Application.Abstractions.Repositories;

namespace OpenTalkie.Application.Settings.Queries;

public readonly record struct GetPlaybackSettingsQuery : IQuery<PlaybackSettingsState>;

public sealed class GetPlaybackSettingsQueryHandler(IPlaybackRepository repository)
    : IQueryHandler<GetPlaybackSettingsQuery, PlaybackSettingsState>
{
    public ValueTask<PlaybackSettingsState> Handle(
        GetPlaybackSettingsQuery query,
        CancellationToken cancellationToken)
    {
        return ValueTask.FromResult(repository.GetSettings());
    }
}
