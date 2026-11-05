using Mediator;
using OpenTalkie.Domain.Enums;
using OpenTalkie.Application.Abstractions.Repositories;

namespace OpenTalkie.Application.Settings.Commands;

public readonly record struct SetReceiverVolumeCommand(float VolumeGain) : ICommand<OperationResult>;

public sealed class SetReceiverVolumeCommandHandler(IReceiverRepository repository, IVolumeLimitRepository volumeLimits)
    : ICommandHandler<SetReceiverVolumeCommand, OperationResult>
{
    public ValueTask<OperationResult> Handle(
        SetReceiverVolumeCommand command,
        CancellationToken cancellationToken)
    {
        OperationResult validation = SettingsValidation.ValidateVolume(command.VolumeGain, volumeLimits.GetMaximum(EndpointType.Receiver));

        if (!validation.IsSuccess)
            return ValueTask.FromResult(validation);

        repository.SetSelectedVolume(command.VolumeGain);
        return ValueTask.FromResult(OperationResult.Success());
    }
}
