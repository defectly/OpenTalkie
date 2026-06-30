using CommunityToolkit.Maui.Views;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OpenTalkie.Common.Repositories.Interfaces;
using OpenTalkie.Common.Services;
using OpenTalkie.View.Popups;

namespace OpenTalkie.ViewModel;

public partial class AudioManagerSettingsViewModel : ObservableObject
{
    private readonly IMicrophoneRepository _microphoneRepository;
    private readonly MicrophoneBroadcastService _microphoneBroadcastService;
    private readonly PlaybackBroadcastService _playbackBroadcastService;
    private readonly ReceiverService _receiverService;

    [ObservableProperty]
    private string selectedAudioManagerMode;

    public AudioManagerSettingsViewModel(
        IMicrophoneRepository microphoneRepository,
        MicrophoneBroadcastService microphoneBroadcastService,
        PlaybackBroadcastService playbackBroadcastService,
        ReceiverService receiverService)
    {
        _microphoneRepository = microphoneRepository;
        _microphoneBroadcastService = microphoneBroadcastService;
        _playbackBroadcastService = playbackBroadcastService;
        _receiverService = receiverService;

        SelectedAudioManagerMode = _microphoneRepository.GetSelectedAudioManagerMode();
    }

    [RelayCommand]
    private async Task EditField(string fieldName)
    {
        string[] options = fieldName switch
        {
            "AudioManagerMode" => [.. _microphoneRepository.GetAudioManagerModes()],
            _ => []
        };

        string currentValue = fieldName switch
        {
            "AudioManagerMode" => SelectedAudioManagerMode,
            _ => string.Empty
        };

        var popup = new OptionsPopup(
            $"Choose {fieldName}",
            options,
            (result) =>
            {
                if (result != null && result != currentValue)
                {
                    switch (fieldName)
                    {
                        case "AudioManagerMode":
                            SelectedAudioManagerMode = result;
                            _microphoneRepository.SetSelectedAudioManagerMode(result);
                            break;
                    }
                }
            });

        await Application.Current.MainPage.ShowPopupAsync(popup);
    }

    [RelayCommand]
    private async Task RestartServices()
    {
        try
        {
            bool wasMicRunning = _microphoneBroadcastService.BroadcastState;
            bool wasPlaybackRunning = _playbackBroadcastService.BroadcastState;
            bool wasReceiverRunning = _receiverService.ListeningState;

            if (!wasMicRunning && !wasPlaybackRunning && !wasReceiverRunning)
            {
                var infoPopup = new ErrorPopup("No active services are currently running to restart.");
                await Application.Current.MainPage.ShowPopupAsync(infoPopup);
                return;
            }

            // Stop only the services that are currently active
            if (wasMicRunning)
            {
                await _microphoneBroadcastService.Switch();
            }

            if (wasPlaybackRunning)
            {
                _playbackBroadcastService.Switch();
            }

            if (wasReceiverRunning)
            {
                _receiverService.Switch();
            }

            // Give the underlying Android OS and network sockets 500ms to release resources cleanly
            await Task.Delay(500);

            // Restart only the services that were active prior to the stop sequence
            if (wasMicRunning)
            {
                await _microphoneBroadcastService.Switch();
            }

            if (wasPlaybackRunning)
            {
                // Request a fresh MediaProjection token to avoid the 'could not register audio policy' Error
                if (await _playbackBroadcastService.RequestPermissionAsync())
                {
                    _playbackBroadcastService.Switch();
                }
            }

            if (wasReceiverRunning)
            {
                _receiverService.Switch();
            }

            var successPopup = new ErrorPopup("Active services restarted successfully!");
            await Application.Current.MainPage.ShowPopupAsync(successPopup);
        }
        catch (Exception ex)
        {
            var errorPopup = new ErrorPopup($"Failed to restart services: {ex.Message}");
            await Application.Current.MainPage.ShowPopupAsync(errorPopup);
        }
    }
}