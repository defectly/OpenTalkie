using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Mediator;
using System.Globalization;
using OpenTalkie.Application.Abstractions.Repositories;
using OpenTalkie.Domain.Enums;
using OpenTalkie.Abstractions.Services;
using OpenTalkie.Application.Settings.Commands;
using OpenTalkie.Application.Settings.Queries;

namespace OpenTalkie.Presentation.ViewModels;

public partial class ReceiverSettingsViewModel : ObservableObject
{
    private readonly IMediator _mediator;
    private readonly IVolumeLimitRepository _volumeLimits;
    private readonly IUserDialogService _dialogService;

    [ObservableProperty]
    public partial double Volume { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(MaximumVolumePercent))]
    public partial float MaximumVolumeGain { get; set; } = 1f;

    public double MaximumVolumePercent => MaximumVolumeGain * 100d;

    [ObservableProperty]
    public partial string PrefferedAudioOutputDevice { get; set; } = string.Empty;

    public ReceiverSettingsViewModel(IMediator mediator, IUserDialogService dialogService, IVolumeLimitRepository volumeLimits)
    {
        _mediator = mediator;
        _volumeLimits = volumeLimits;
        _dialogService = dialogService;
        _ = ReloadStateAsync();
    }

    [RelayCommand]
    public async Task VolumeChanged()
    {
        var result = await _mediator.Send(new SetReceiverVolumeCommand((float)(Volume / 100d)));
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
            return;

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
                    return;

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

    [RelayCommand]
    private async Task EditMaximumVolume()
    {
        await _dialogService.ShowEditFieldAsync(
            "Maximum volume multiplier (at least 1)",
            MaximumVolumeGain.ToString(CultureInfo.CurrentCulture),
            Keyboard.Numeric,
            async value =>
            {
                if (!float.TryParse(value, NumberStyles.Float, CultureInfo.CurrentCulture, out float maximum)
                    && !float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out maximum))
                {
                    await _dialogService.ShowErrorAsync("Enter a finite multiplier of 1 or greater.");
                    return;
                }

                var result = await _mediator.Send(new SetVolumeMaximumCommand(EndpointType.Receiver, maximum));
                if (!result.IsSuccess)
                    await ShowErrorAsync(_dialogService, result.ErrorMessage);
                await ReloadStateAsync();
            });
    }

    public async Task ReloadStateAsync()
    {
        try
        {
            MaximumVolumeGain = _volumeLimits.GetMaximum(EndpointType.Receiver);
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
        Volume = state.VolumeGain * 100d;
        PrefferedAudioOutputDevice = state.PreferredAudioOutputDevice.DisplayName;
    }

    private static async Task ShowErrorAsync(IUserDialogService dialogService, string? errorMessage)
    {
        await dialogService.ShowErrorAsync(errorMessage ?? "Unable to update receiver settings.");
    }
}
