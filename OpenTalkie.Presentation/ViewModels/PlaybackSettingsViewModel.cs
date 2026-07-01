using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Mediator;
using OpenTalkie.Application.Settings;
using OpenTalkie.Application.Settings.Commands;
using OpenTalkie.Application.Settings.Queries;
using OpenTalkie.Presentation.Abstractions.Services;

namespace OpenTalkie.Presentation.ViewModels;

public partial class PlaybackSettingsViewModel : ObservableObject
{
    private readonly IMediator _mediator;
    private readonly IUserDialogService _dialogService;
    private readonly ILogger<PlaybackSettingsViewModel> _logger;

    [ObservableProperty]
    public partial string SelectedChannelOut { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string SelectedSampleRate { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string SelectedEncoding { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string SelectedBufferSize { get; set; } = string.Empty;

    [ObservableProperty]
    public partial float Volume { get; set; }

    public PlaybackSettingsViewModel(
        IMediator mediator,
        IUserDialogService dialogService,
        ILogger<PlaybackSettingsViewModel> logger)
    {
        _mediator = mediator;
        _dialogService = dialogService;
        _logger = logger;
        _ = ReloadStateAsync();
    }

    [RelayCommand]
    public async Task VolumeChanged()
    {
        var result = await _mediator.Send(new SetPlaybackVolumeCommand(Volume / 100.0f));
        if (!result.IsSuccess)
        {
            if (_logger.IsEnabled(LogLevel.Warning))
            {
                _logger.LogWarning("Failed to update playback volume: {ErrorMessage}.", result.ErrorMessage);
            }
            await ShowErrorAsync(_dialogService, result.ErrorMessage);
            await ReloadStateAsync();
        }
    }

    [RelayCommand]
    private async Task EditField(string fieldName)
    {
        var option = MapOption(fieldName);

        if (option == null)
            return;

        var options = await _mediator.Send(new GetPlaybackSettingOptionsQuery(option.Value));
        var currentValue = GetCurrentValue(option.Value);
        var labels = options.Select(item => item.DisplayName).ToArray();

        await _dialogService.ShowOptionsAsync(
            $"Choose {fieldName}",
            labels,
            async result =>
            {
                var selectedOption = options.FirstOrDefault(item => item.DisplayName == result);
                if (string.IsNullOrWhiteSpace(selectedOption.Value) || selectedOption.DisplayName == currentValue)
                    return;

                var updateResult = await _mediator.Send(new SetPlaybackSettingOptionCommand(option.Value, selectedOption.Value));
                if (!updateResult.IsSuccess)
                {
                    if (_logger.IsEnabled(LogLevel.Warning))
                        _logger.LogWarning("Failed to update playback setting {Option}: {ErrorMessage}.", option.Value, updateResult.ErrorMessage);

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
                    _logger.LogWarning("Rejected playback buffer size value '{Value}'.", result);
                    await ShowErrorAsync(_dialogService, "Something's wrong with the number");
                    return;
                }

                var updateResult = await _mediator.Send(new SetPlaybackBufferSizeCommand(bufferSize));
                if (!updateResult.IsSuccess)
                {
                    if (_logger.IsEnabled(LogLevel.Warning))
                        _logger.LogWarning("Failed to update playback buffer size: {ErrorMessage}.", updateResult.ErrorMessage);

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
        var result = await _mediator.Send(new SetPlaybackVolumeCommand(1f));
        if (!result.IsSuccess)
        {
            if (_logger.IsEnabled(LogLevel.Warning))
                _logger.LogWarning("Failed to reset playback volume: {ErrorMessage}.", result.ErrorMessage);

            await ShowErrorAsync(_dialogService, result.ErrorMessage);
            await ReloadStateAsync();
        }
    }

    private async Task ReloadStateAsync()
    {
        try
        {
            var state = await _mediator.Send(new GetPlaybackSettingsQuery());
            ApplyState(state);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to reload playback settings.");
            await ShowErrorAsync(_dialogService, ex.Message);
        }
    }

    private void ApplyState(PlaybackSettingsState state)
    {
        SelectedChannelOut = state.SelectedChannelOut.DisplayName;
        SelectedSampleRate = state.SelectedSampleRate.DisplayName;
        SelectedEncoding = state.SelectedEncoding.DisplayName;
        SelectedBufferSize = state.SelectedBufferSize.ToString();
        Volume = state.VolumeGain * 100;
    }

    private static PlaybackSettingOption? MapOption(string fieldName)
    {
        return fieldName switch
        {
            "ChannelOut" => PlaybackSettingOption.ChannelOut,
            "SampleRate" => PlaybackSettingOption.SampleRate,
            "Encoding" => PlaybackSettingOption.Encoding,
            _ => null
        };
    }

    private string GetCurrentValue(PlaybackSettingOption option)
    {
        return option switch
        {
            PlaybackSettingOption.ChannelOut => SelectedChannelOut,
            PlaybackSettingOption.SampleRate => SelectedSampleRate,
            PlaybackSettingOption.Encoding => SelectedEncoding,
            _ => string.Empty
        };
    }

    private void SetCurrentValue(PlaybackSettingOption option, string value)
    {
        switch (option)
        {
            case PlaybackSettingOption.ChannelOut:
                SelectedChannelOut = value;
                break;
            case PlaybackSettingOption.SampleRate:
                SelectedSampleRate = value;
                break;
            case PlaybackSettingOption.Encoding:
                SelectedEncoding = value;
                break;
        }
    }

    private static async Task ShowErrorAsync(IUserDialogService dialogService, string? errorMessage)
    {
        await dialogService.ShowErrorAsync(errorMessage ?? "Unable to update cast settings.");
    }
}
