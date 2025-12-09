using CommunityToolkit.Maui.Views;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OpenTalkie.Common.Repositories.Interfaces;
using OpenTalkie.View.Popups;

namespace OpenTalkie.ViewModel;

public partial class ReceiverSettingsViewModel : ObservableObject
{
    private readonly IReceiverRepository _receiverRepository;

    [ObservableProperty]
    private float volume;

    [ObservableProperty]
    private string prefferedAudioOutputDevice;

    public ReceiverSettingsViewModel(IReceiverRepository receiverRepository)
    {
        _receiverRepository = receiverRepository;
        Volume = _receiverRepository.GetSelectedVolume() * 100f;
        PrefferedAudioOutputDevice = _receiverRepository.GetPrefferedDevice();
    }

    [RelayCommand]
    public void VolumeChanged()
    {
        _receiverRepository.SetSelectedVolume(Volume / 100.0f);
    }

    [RelayCommand]
    private void ResetVolume()
    {
        Volume = 100.0f;
        _receiverRepository.SetSelectedVolume(Volume / 100.0f);
    }

    [RelayCommand]
    private async Task EditField(string fieldName)
    {
        string[] options = fieldName switch
        {
            "PrefferedAudioOutputDevice" => [.. _receiverRepository.GetAvailableAudioOutputDevices()],
            _ => []
        };

        string currentValue = fieldName switch
        {
            "PrefferedAudioOutputDevice" => _receiverRepository.GetPrefferedDevice() ?? string.Empty,
            _ => string.Empty
        };

        var popup = new OptionsPopup(
            $"Choose {fieldName}",
            options,
            (result) =>
            {
                if (!string.IsNullOrWhiteSpace(result) && result != currentValue)
                {
                    switch (fieldName)
                    {
                        case "PrefferedAudioOutputDevice":
                            _receiverRepository.SetPrefferedDevice(result);
                            PrefferedAudioOutputDevice = _receiverRepository.GetPrefferedDevice() ?? string.Empty;
                            break;
                    }
                }
            });

        if (Application.Current?.MainPage is not null)
            await Application.Current.MainPage.ShowPopupAsync(popup);
    }
}

