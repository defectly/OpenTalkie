using Mediator;
using OpenTalkie.Application.Abstractions.Services;

namespace OpenTalkie.Application.Home.Commands;

public readonly record struct SwitchReceiverCommand : ICommand<OperationResult>;

public sealed class SwitchReceiverCommandHandler(IReceiverService receiverService)
    : ICommandHandler<SwitchReceiverCommand, OperationResult>
{
    public ValueTask<OperationResult> Handle(
        SwitchReceiverCommand command,
        CancellationToken cancellationToken)
    {
        receiverService.Switch();
        return ValueTask.FromResult(OperationResult.Success());
    }
}
