using Mediator;
using OpenTalkie.Application.Abstractions.Repositories;

namespace OpenTalkie.Application.Settings.Commands;

public readonly record struct SetMicrophonePacingCommand(bool IsEnabled)
    : ICommand<OperationResult>;

public sealed class SetMicrophonePacingCommandHandler(IMicrophoneRepository repository)
    : ICommandHandler<SetMicrophonePacingCommand, OperationResult>
{
    public ValueTask<OperationResult> Handle(
        SetMicrophonePacingCommand command,
        CancellationToken cancellationToken)
    {
        repository.SetPacingEnabled(command.IsEnabled);
        return ValueTask.FromResult(OperationResult.Success());
    }
}
