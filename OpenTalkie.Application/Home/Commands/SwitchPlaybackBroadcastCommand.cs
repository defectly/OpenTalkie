using Mediator;
using OpenTalkie.Application.Abstractions.Services;

namespace OpenTalkie.Application.Home.Commands;

public readonly record struct SwitchPlaybackBroadcastCommand : ICommand<OperationResult>;

public sealed class SwitchPlaybackBroadcastCommandHandler(
    IPlaybackBroadcastService playbackBroadcastService,
    IMicrophonePermissionService microphonePermissionService)
    : ICommandHandler<SwitchPlaybackBroadcastCommand, OperationResult>
{
    public async ValueTask<OperationResult> Handle(
        SwitchPlaybackBroadcastCommand command,
        CancellationToken cancellationToken)
    {
        if (playbackBroadcastService.Status.Phase is StreamSessionPhase.Stopped or StreamSessionPhase.Faulted)
        {
            if (!await microphonePermissionService.RequestMicrophonePermissionAsync())
            {
                return OperationResult.Fail("Microphone permission denied");
            }

            if (!await playbackBroadcastService.RequestPermissionAsync())
            {
                return OperationResult.Fail("Screen capture permission denied");
            }
        }

        return playbackBroadcastService.Switch();
    }
}
