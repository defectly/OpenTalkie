using Mediator;
using OpenTalkie.Application.Abstractions.Repositories;

namespace OpenTalkie.Application.Settings.Commands;

public readonly record struct SetReceiverVolumeCommand(float VolumeGain) : ICommand<OperationResult>;

public sealed class SetReceiverVolumeCommandHandler(IReceiverRepository repository)
    : ICommandHandler<SetReceiverVolumeCommand, OperationResult>
{
    public ValueTask<OperationResult> Handle(
        SetReceiverVolumeCommand command,
        CancellationToken cancellationToken)
    {
        OperationResult validation = SettingsValidation.ValidateVolume(command.VolumeGain);
        if (!validation.IsSuccess)
        {
            return ValueTask.FromResult(validation);
        }

        repository.SetSelectedVolume(command.VolumeGain);
        return ValueTask.FromResult(OperationResult.Success());
    }
}
