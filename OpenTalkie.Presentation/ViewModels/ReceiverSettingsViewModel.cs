using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Mediator;
using OpenTalkie.Application.Settings.Commands;
using OpenTalkie.Application.Settings.Queries;
using OpenTalkie.Presentation.Abstractions.Services;

namespace OpenTalkie.Presentation.ViewModels;

public partial class ReceiverSettingsViewModel : ObservableObject
{
    private readonly IMediator _mediator;
    private readonly IUserDialogService _dialogService;

    [ObservableProperty]
    public partial float Volume { get; set; }

    [ObservableProperty]
    public partial string PrefferedAudioOutputDevice { get; set; } = string.Empty;

    public ReceiverSettingsViewModel(IMediator mediator, IUserDialogService dialogService)
    {
        _mediator = mediator;
        _dialogService = dialogService;
        _ = ReloadStateAsync();
    }

    [RelayCommand]
    public async Task VolumeChanged()
    {
        var result = await _mediator.Send(new SetReceiverVolumeCommand(Volume / 100.0f));
        if (!result.IsSuccess)
        {
            await ShowErrorAsync(_dialogService, result.ErrorMessage);
            await ReloadStateAsync();
        }
    }

    [RelayCommand]
    private async Task ResetVolume()
    {
        Volume = 100.0f;
        var result = await _mediator.Send(new SetReceiverVolumeCommand(1f));
        if (!result.IsSuccess)
        {
            await ShowErrorAsync(_dialogService, result.ErrorMessage);
            await ReloadStateAsync();
        }
    }

    [RelayCommand]
    private async Task EditField(string fieldName)
    {
        if (fieldName != "PrefferedAudioOutputDevice")
        {
            return;
        }

        var options = await _mediator.Send(new GetReceiverAudioOutputDevicesQuery());
        var labels = options.Select(item => item.DisplayName).ToArray();
        string currentValue = PrefferedAudioOutputDevice;

        await _dialogService.ShowOptionsAsync(
            $"Choose {fieldName}",
            labels,
            async result =>
            {
                var selectedOption = options.FirstOrDefault(item => item.DisplayName == result);
                if (string.IsNullOrWhiteSpace(selectedOption.Value) || selectedOption.DisplayName == currentValue)
                {
                    return;
                }

                var updateResult = await _mediator.Send(new SetReceiverPreferredAudioOutputDeviceCommand(selectedOption.Value));
                if (!updateResult.IsSuccess)
                {
                    await ShowErrorAsync(_dialogService, updateResult.ErrorMessage);
                    await ReloadStateAsync();
                    return;
                }

                PrefferedAudioOutputDevice = selectedOption.DisplayName;
            });
    }

    private async Task ReloadStateAsync()
    {
        try
        {
            var state = await _mediator.Send(new GetReceiverSettingsQuery());
            ApplyState(state);
        }
        catch (Exception ex)
        {
            await ShowErrorAsync(_dialogService, ex.Message);
        }
    }

    private void ApplyState(ReceiverSettingsState state)
    {
        Volume = state.VolumeGain * 100f;
        PrefferedAudioOutputDevice = state.PreferredAudioOutputDevice.DisplayName;
    }

    private static async Task ShowErrorAsync(IUserDialogService dialogService, string? errorMessage)
    {
        await dialogService.ShowErrorAsync(errorMessage ?? "Unable to update receiver settings.");
    }
}
