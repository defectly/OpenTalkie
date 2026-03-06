using Mediator;
using OpenTalkie.Application.Abstractions.Repositories;

namespace OpenTalkie.Application.Settings.Commands;

public readonly record struct SetMicrophoneBufferSizeCommand(int Value)
    : ICommand<OperationResult>;

public sealed class SetMicrophoneBufferSizeCommandHandler(IMicrophoneRepository repository)
    : ICommandHandler<SetMicrophoneBufferSizeCommand, OperationResult>
{
    public ValueTask<OperationResult> Handle(SetMicrophoneBufferSizeCommand command, CancellationToken cancellationToken)
    {
        if (command.Value <= 0)
        {
            return ValueTask.FromResult(OperationResult.Fail("Buffer size must be greater than zero."));
        }

        repository.SetBufferSize(command.Value);
        return ValueTask.FromResult(OperationResult.Success());
    }
}
