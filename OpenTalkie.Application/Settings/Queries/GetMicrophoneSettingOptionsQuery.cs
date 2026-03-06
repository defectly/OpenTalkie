using Mediator;
using OpenTalkie.Application.Abstractions.Repositories;

namespace OpenTalkie.Application.Settings.Queries;

public readonly record struct GetMicrophoneSettingOptionsQuery(MicrophoneSettingOption Option)
    : IQuery<IReadOnlyList<SettingOptionItem>>;

public sealed class GetMicrophoneSettingOptionsQueryHandler(IMicrophoneRepository repository)
    : IQueryHandler<GetMicrophoneSettingOptionsQuery, IReadOnlyList<SettingOptionItem>>
{
    public ValueTask<IReadOnlyList<SettingOptionItem>> Handle(
        GetMicrophoneSettingOptionsQuery query,
        CancellationToken cancellationToken)
    {
        return ValueTask.FromResult(repository.GetOptions(query.Option));
    }
}
