using Mediator;
using OpenTalkie.Application.Abstractions.Services;

namespace OpenTalkie.Application.Home.Commands;

public readonly record struct SwitchMicrophoneBroadcastCommand : ICommand<OperationResult>;

public sealed class SwitchMicrophoneBroadcastCommandHandler(
    IMicrophoneBroadcastService microphoneBroadcastService,
    IMicrophonePermissionService microphonePermissionService,
    ILogger<SwitchMicrophoneBroadcastCommandHandler> logger)
    : ICommandHandler<SwitchMicrophoneBroadcastCommand, OperationResult>
{
    public async ValueTask<OperationResult> Handle(
        SwitchMicrophoneBroadcastCommand command,
        CancellationToken cancellationToken)
    {
        if (microphoneBroadcastService.Status.Phase is StreamSessionPhase.Stopped or StreamSessionPhase.Faulted)
        {
            if (!await microphonePermissionService.RequestMicrophonePermissionAsync())
            {
                logger.LogWarning("Microphone broadcast switch failed because microphone permission was denied.");
                return OperationResult.Fail("Microphone permission denied");
            }
        }

        if (logger.IsEnabled(LogLevel.Information))
            logger.LogInformation("Switching microphone broadcast from phase {Phase}.", microphoneBroadcastService.Status.Phase);

        return await microphoneBroadcastService.SwitchAsync();
    }
}
