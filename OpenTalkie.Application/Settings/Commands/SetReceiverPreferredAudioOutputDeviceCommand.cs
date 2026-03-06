using Mediator;
using OpenTalkie.Application.Abstractions.Repositories;

namespace OpenTalkie.Application.Settings.Commands;

public readonly record struct SetReceiverPreferredAudioOutputDeviceCommand(string Value) : ICommand<OperationResult>;

public sealed class SetReceiverPreferredAudioOutputDeviceCommandHandler(IReceiverRepository repository)
    : ICommandHandler<SetReceiverPreferredAudioOutputDeviceCommand, OperationResult>
{
    public ValueTask<OperationResult> Handle(
        SetReceiverPreferredAudioOutputDeviceCommand command,
        CancellationToken cancellationToken)
    {
        OperationResult validation = SettingsValidation.ValidateNotEmpty(command.Value, "Output device");
        if (!validation.IsSuccess)
        {
            return ValueTask.FromResult(validation);
        }

        repository.SetPreferredDevice(command.Value);
        return ValueTask.FromResult(OperationResult.Success());
    }
}
