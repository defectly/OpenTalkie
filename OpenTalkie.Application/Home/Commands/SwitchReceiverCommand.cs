using Mediator;
using OpenTalkie.Application.Abstractions.Services;

namespace OpenTalkie.Application.Home.Commands;

public readonly record struct SwitchReceiverCommand : ICommand<OperationResult>;

public sealed class SwitchReceiverCommandHandler(
    IReceiverService receiverService,
    ILogger<SwitchReceiverCommandHandler> logger)
    : ICommandHandler<SwitchReceiverCommand, OperationResult>
{
    public ValueTask<OperationResult> Handle(
        SwitchReceiverCommand command,
        CancellationToken cancellationToken)
    {
        if (logger.IsEnabled(LogLevel.Information))
            logger.LogInformation("Switching receiver from phase {Phase}.", receiverService.Status.Phase);

        receiverService.Switch();
        return ValueTask.FromResult(OperationResult.Success());
    }
}
