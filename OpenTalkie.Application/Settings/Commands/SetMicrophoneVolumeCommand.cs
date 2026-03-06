using Mediator;
using OpenTalkie.Application.Abstractions.Repositories;

namespace OpenTalkie.Application.Settings.Commands;

public readonly record struct SetMicrophoneVolumeCommand(float VolumeGain) : ICommand<OperationResult>;

public sealed class SetMicrophoneVolumeCommandHandler(IMicrophoneRepository repository)
    : ICommandHandler<SetMicrophoneVolumeCommand, OperationResult>
{
    public ValueTask<OperationResult> Handle(
        SetMicrophoneVolumeCommand command,
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
