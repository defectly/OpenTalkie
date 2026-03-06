using Mediator;
using OpenTalkie.Application.Abstractions.Repositories;

namespace OpenTalkie.Application.Settings.Queries;

public readonly record struct GetPlaybackSettingOptionsQuery(PlaybackSettingOption Option)
    : IQuery<IReadOnlyList<SettingOptionItem>>;

public sealed class GetPlaybackSettingOptionsQueryHandler(IPlaybackRepository repository)
    : IQueryHandler<GetPlaybackSettingOptionsQuery, IReadOnlyList<SettingOptionItem>>
{
    public ValueTask<IReadOnlyList<SettingOptionItem>> Handle(
        GetPlaybackSettingOptionsQuery query,
        CancellationToken cancellationToken)
    {
        return ValueTask.FromResult(repository.GetOptions(query.Option));
    }
}
