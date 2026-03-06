using Mediator;
using OpenTalkie.Application.Abstractions.Repositories;

namespace OpenTalkie.Application.Settings.Commands;

public readonly record struct SetMicrophoneSettingOptionCommand(MicrophoneSettingOption Option, string Value)
    : ICommand<OperationResult>;

public sealed class SetMicrophoneSettingOptionCommandHandler(IMicrophoneRepository repository)
    : ICommandHandler<SetMicrophoneSettingOptionCommand, OperationResult>
{
    public ValueTask<OperationResult> Handle(
        SetMicrophoneSettingOptionCommand command,
        CancellationToken cancellationToken)
    {
        OperationResult validation = SettingsValidation.ValidateNotEmpty(command.Value, "Setting value");
        if (!validation.IsSuccess)
        {
            return ValueTask.FromResult(validation);
        }

        repository.SetOption(command.Option, command.Value);
        return ValueTask.FromResult(OperationResult.Success());
    }
}
