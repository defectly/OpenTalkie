using Mediator;
using OpenTalkie.Application.Abstractions.Services;

namespace OpenTalkie.Application.Home.Commands;

public readonly record struct SwitchPlaybackBroadcastCommand : ICommand<OperationResult>;

public sealed class SwitchPlaybackBroadcastCommandHandler(
    IPlaybackBroadcastService playbackBroadcastService,
    IMicrophonePermissionService microphonePermissionService,
    ILogger<SwitchPlaybackBroadcastCommandHandler> logger)
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
                logger.LogWarning("Playback broadcast switch failed because microphone permission was denied.");
                return OperationResult.Fail("Microphone permission denied");
            }

            if (!await playbackBroadcastService.RequestPermissionAsync())
            {
                logger.LogWarning("Playback broadcast switch failed because screen capture permission was denied.");
                return OperationResult.Fail("Screen capture permission denied");
            }
        }

        if (logger.IsEnabled(LogLevel.Information))
            logger.LogInformation("Switching playback broadcast from phase {Phase}.", playbackBroadcastService.Status.Phase);

        return playbackBroadcastService.Switch();
    }
}
