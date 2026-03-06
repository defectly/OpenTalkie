using Mediator;
using OpenTalkie.Application.Abstractions.Repositories;

namespace OpenTalkie.Application.Settings.Queries;

public readonly record struct GetReceiverAudioOutputDevicesQuery : IQuery<IReadOnlyList<SettingOptionItem>>;

public sealed class GetReceiverAudioOutputDevicesQueryHandler(IReceiverRepository repository)
    : IQueryHandler<GetReceiverAudioOutputDevicesQuery, IReadOnlyList<SettingOptionItem>>
{
    public ValueTask<IReadOnlyList<SettingOptionItem>> Handle(
        GetReceiverAudioOutputDevicesQuery query,
        CancellationToken cancellationToken)
    {
        return ValueTask.FromResult(repository.GetAudioOutputOptions());
    }
}
