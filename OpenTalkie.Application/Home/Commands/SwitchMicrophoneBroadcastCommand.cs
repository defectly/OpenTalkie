using Mediator;
using OpenTalkie.Application.Abstractions.Services;

namespace OpenTalkie.Application.Home.Commands;

public readonly record struct SwitchMicrophoneBroadcastCommand : ICommand<OperationResult>;

public sealed class SwitchMicrophoneBroadcastCommandHandler(
    IMicrophoneBroadcastService microphoneBroadcastService,
    IMicrophonePermissionService microphonePermissionService)
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
                return OperationResult.Fail("Microphone permission denied");
            }
        }

        return await microphoneBroadcastService.SwitchAsync();
    }
}
