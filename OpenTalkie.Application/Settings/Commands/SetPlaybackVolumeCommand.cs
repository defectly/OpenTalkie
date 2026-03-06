using Mediator;
using OpenTalkie.Application.Abstractions.Repositories;

namespace OpenTalkie.Application.Settings.Commands;

public readonly record struct SetPlaybackVolumeCommand(float VolumeGain) : ICommand<OperationResult>;

public sealed class SetPlaybackVolumeCommandHandler(IPlaybackRepository repository)
    : ICommandHandler<SetPlaybackVolumeCommand, OperationResult>
{
    public ValueTask<OperationResult> Handle(
        SetPlaybackVolumeCommand command,
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
