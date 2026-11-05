using Mediator;
using OpenTalkie.Application.Abstractions.Repositories;
using OpenTalkie.Application.Settings.Queries;
using OpenTalkie.Domain.Enums;

namespace OpenTalkie.Application.Settings.Commands;

public readonly record struct SetVolumeMaximumCommand(EndpointType StreamType, float Maximum)
    : ICommand<OperationResult>;

public sealed class SetVolumeMaximumCommandHandler(
    IMediator mediator,
    IVolumeLimitRepository volumeLimits)
    : ICommandHandler<SetVolumeMaximumCommand, OperationResult>
{
    public async ValueTask<OperationResult> Handle(SetVolumeMaximumCommand command, CancellationToken cancellationToken)
    {
        if (!Enum.IsDefined(command.StreamType) || !float.IsFinite(command.Maximum) || command.Maximum < 1f)
            return OperationResult.Fail("Maximum volume multiplier must be a finite number of 1 or greater.");

        try
        {
            OperationResult globalResult = command.StreamType switch
            {
                EndpointType.Microphone => await mediator.Send(new SetMicrophoneVolumeCommand(Math.Min(
                    (await mediator.Send(new GetMicrophoneSettingsQuery(), cancellationToken)).VolumeGain, command.Maximum)), cancellationToken),
                EndpointType.Playback => await mediator.Send(new SetPlaybackVolumeCommand(Math.Min(
                    (await mediator.Send(new GetPlaybackSettingsQuery(), cancellationToken)).VolumeGain, command.Maximum)), cancellationToken),
                EndpointType.Receiver => await mediator.Send(new SetReceiverVolumeCommand(Math.Min(
                    (await mediator.Send(new GetReceiverSettingsQuery(), cancellationToken)).VolumeGain, command.Maximum)), cancellationToken),
                _ => OperationResult.Fail("Unknown stream type.")
            };
            if (!globalResult.IsSuccess)
                return globalResult;

            volumeLimits.SetMaximum(command.StreamType, command.Maximum);
            return OperationResult.Success();
        }
        catch (Exception ex)
        {
            return OperationResult.Fail($"Failed to update maximum volume: {ex.Message}");
        }
    }
}
