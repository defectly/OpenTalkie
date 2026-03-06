using Mediator;
using OpenTalkie.Application.Abstractions.Repositories;

namespace OpenTalkie.Application.Settings.Commands;

public readonly record struct SetPlaybackBufferSizeCommand(int Value)
    : ICommand<OperationResult>;

public sealed class SetPlaybackBufferSizeCommandHandler(IPlaybackRepository repository)
    : ICommandHandler<SetPlaybackBufferSizeCommand, OperationResult>
{
    public ValueTask<OperationResult> Handle(SetPlaybackBufferSizeCommand command, CancellationToken cancellationToken)
    {
        if (command.Value <= 0)
        {
            return ValueTask.FromResult(OperationResult.Fail("Buffer size must be greater than zero."));
        }

        repository.SetBufferSize(command.Value);
        return ValueTask.FromResult(OperationResult.Success());
    }
}
