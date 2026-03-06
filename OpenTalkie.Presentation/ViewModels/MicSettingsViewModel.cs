using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Mediator;
using OpenTalkie.Application.Settings;
using OpenTalkie.Application.Settings.Commands;
using OpenTalkie.Application.Settings.Queries;
using OpenTalkie.Presentation.Abstractions.Services;

namespace OpenTalkie.Presentation.ViewModels;

public partial class MicSettingsViewModel : ObservableObject
{
    private readonly IMediator _mediator;
    private readonly IUserDialogService _dialogService;

    [ObservableProperty]
    public partial string SelectedSource { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string SelectedInputChannel { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string SelectedSampleRate { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string SelectedEncoding { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string SelectedBufferSize { get; set; } = string.Empty;

    [ObservableProperty]
    public partial float Volume { get; set; }

    [ObservableProperty]
    public partial string PrefferedAudioInputDevice { get; set; } = string.Empty;

    public MicSettingsViewModel(IMediator mediator, IUserDialogService dialogService)
    {
        _mediator = mediator;
        _dialogService = dialogService;
        _ = ReloadStateAsync();
    }

    [RelayCommand]
    public async Task VolumeChanged()
    {
        var result = await _mediator.Send(new SetMicrophoneVolumeCommand(Volume / 100.0f));
        if (!result.IsSuccess)
        {
            await ShowErrorAsync(_dialogService, result.ErrorMessage);
            await ReloadStateAsync();
        }
    }

    [RelayCommand]
    private async Task EditField(string fieldName)
    {
        var option = MapOption(fieldName);
        if (option == null)
        {
            return;
        }

        var options = await _mediator.Send(new GetMicrophoneSettingOptionsQuery(option.Value));
        var currentValue = GetCurrentValue(option.Value);
        var labels = options.Select(item => item.DisplayName).ToArray();

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

                var updateResult = await _mediator.Send(new SetMicrophoneSettingOptionCommand(option.Value, selectedOption.Value));
                if (!updateResult.IsSuccess)
                {
                    await ShowErrorAsync(_dialogService, updateResult.ErrorMessage);
                    await ReloadStateAsync();
                    return;
                }

                SetCurrentValue(option.Value, selectedOption.DisplayName);
            });
    }

    [RelayCommand]
    private async Task EditBufferSize()
    {
        await _dialogService.ShowEditFieldAsync(
            "Change buffer size",
            SelectedBufferSize,
            Keyboard.Numeric,
            async result =>
            {
                if (!int.TryParse(result, out int bufferSize) || bufferSize <= 0)
                {
                    await ShowErrorAsync(_dialogService, "Something's wrong with the number");
                    return;
                }

                var updateResult = await _mediator.Send(new SetMicrophoneBufferSizeCommand(bufferSize));
                if (!updateResult.IsSuccess)
                {
                    await ShowErrorAsync(_dialogService, updateResult.ErrorMessage);
                    await ReloadStateAsync();
                    return;
                }

                SelectedBufferSize = bufferSize.ToString();
            });
    }

    [RelayCommand]
    private async Task ResetVolume()
    {
        Volume = 100.0f;
        var result = await _mediator.Send(new SetMicrophoneVolumeCommand(1f));
        if (!result.IsSuccess)
        {
            await ShowErrorAsync(_dialogService, result.ErrorMessage);
            await ReloadStateAsync();
        }
    }

    private async Task ReloadStateAsync()
    {
        try
        {
            var state = await _mediator.Send(new GetMicrophoneSettingsQuery());
            ApplyState(state);
        }
        catch (Exception ex)
        {
            await ShowErrorAsync(_dialogService, ex.Message);
        }
    }

    private void ApplyState(MicrophoneSettingsState state)
    {
        SelectedSource = state.SelectedSource.DisplayName;
        SelectedInputChannel = state.SelectedInputChannel.DisplayName;
        SelectedSampleRate = state.SelectedSampleRate.DisplayName;
        SelectedEncoding = state.SelectedEncoding.DisplayName;
        SelectedBufferSize = state.SelectedBufferSize.ToString();
        Volume = state.VolumeGain * 100;
        PrefferedAudioInputDevice = state.PreferredAudioInputDevice.DisplayName;
    }

    private static MicrophoneSettingOption? MapOption(string fieldName)
    {
        return fieldName switch
        {
            "Source" => MicrophoneSettingOption.Source,
            "InputChannel" => MicrophoneSettingOption.InputChannel,
            "SampleRate" => MicrophoneSettingOption.SampleRate,
            "Encoding" => MicrophoneSettingOption.Encoding,
            "PrefferedAudioInputDevice" => MicrophoneSettingOption.PreferredAudioInputDevice,
            _ => null
        };
    }

    private string GetCurrentValue(MicrophoneSettingOption option)
    {
        return option switch
        {
            MicrophoneSettingOption.Source => SelectedSource,
            MicrophoneSettingOption.InputChannel => SelectedInputChannel,
            MicrophoneSettingOption.SampleRate => SelectedSampleRate,
            MicrophoneSettingOption.Encoding => SelectedEncoding,
            MicrophoneSettingOption.PreferredAudioInputDevice => PrefferedAudioInputDevice,
            _ => string.Empty
        };
    }

    private void SetCurrentValue(MicrophoneSettingOption option, string value)
    {
        switch (option)
        {
            case MicrophoneSettingOption.Source:
                SelectedSource = value;
                break;
            case MicrophoneSettingOption.InputChannel:
                SelectedInputChannel = value;
                break;
            case MicrophoneSettingOption.SampleRate:
                SelectedSampleRate = value;
                break;
            case MicrophoneSettingOption.Encoding:
                SelectedEncoding = value;
                break;
            case MicrophoneSettingOption.PreferredAudioInputDevice:
                PrefferedAudioInputDevice = value;
                break;
        }
    }

    private static async Task ShowErrorAsync(IUserDialogService dialogService, string? errorMessage)
    {
        await dialogService.ShowErrorAsync(errorMessage ?? "Unable to update microphone settings.");
    }
}
