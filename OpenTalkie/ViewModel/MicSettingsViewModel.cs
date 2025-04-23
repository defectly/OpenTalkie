using CommunityToolkit.Maui.Views;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OpenTalkie.Common.Repositories.Interfaces;
using OpenTalkie.View.Popups;

namespace OpenTalkie.ViewModel;

public partial class MicSettingsViewModel : ObservableObject
{
    private readonly IMicrophoneRepository _microphoneRepository;
    private readonly AppShell _mainPage;

    [ObservableProperty]
    private string selectedSource;

    [ObservableProperty]
    private string selectedInputChannel;

    [ObservableProperty]
    private string selectedSampleRate;

    [ObservableProperty]
    private string selectedEncoding;

    [ObservableProperty]
    private string selectedBufferSize;

    [ObservableProperty]
    private float volume;

    public MicSettingsViewModel(AppShell mainPage, IMicrophoneRepository microphoneRepository)
    {
        _mainPage = mainPage;
        _microphoneRepository = microphoneRepository;

        SelectedSource = _microphoneRepository.GetSelectedSource();
        SelectedInputChannel = _microphoneRepository.GetSelectedInputChannel();
        SelectedSampleRate = _microphoneRepository.GetSelectedSampleRate();
        SelectedEncoding = _microphoneRepository.GetSelectedEncoding();
        SelectedBufferSize = _microphoneRepository.GetSelectedBufferSize();
        Volume = _microphoneRepository.GetSelectedVolume() * 100;
    }

    partial void OnVolumeChanged(float value)
    {
        _microphoneRepository.SetSelectedVolume(value / 100.0f);
    }

    [RelayCommand]
    private async Task EditField(string fieldName)
    {
        string[] options = fieldName switch
        {
            "Source" => [.. _microphoneRepository.GetAudioSources()],
            "InputChannel" => [.. _microphoneRepository.GetInputChannels()],
            "SampleRate" => [.. _microphoneRepository.GetSampleRates()],
            "Encoding" => [.. _microphoneRepository.GetEncodings()],
            _ => []
        };

        string currentValue = fieldName switch
        {
            "Source" => SelectedSource,
            "InputChannel" => SelectedInputChannel,
            "SampleRate" => SelectedSampleRate,
            "Encoding" => SelectedEncoding,
            _ => ""
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
                        case "Source":
                            SelectedSource = result;
                            _microphoneRepository.SetSelectedSource(result);
                            break;
                        case "InputChannel":
                            SelectedInputChannel = result;
                            _microphoneRepository.SetSelectedInputChannel(result);
                            break;
                        case "SampleRate":
                            SelectedSampleRate = result;
                            _microphoneRepository.SetSelectedSampleRate(result);
                            break;
                        case "Encoding":
                            SelectedEncoding = result;
                            _microphoneRepository.SetSelectedEncoding(result);
                            break;
                    }
                }
            });

        await _mainPage.ShowPopupAsync(popup);
    }

    [RelayCommand]
    private async Task EditBufferSize()
    {
        string currentValue = SelectedBufferSize ?? _microphoneRepository.GetSelectedBufferSize();

        var popup = new EditFieldPopup(
            "Change buffer size",
            currentValue,
            Keyboard.Numeric,
            async (result) =>
            {
                if (result != null && int.TryParse(result, out int bufferSize) && bufferSize > 0)
                {
                    SelectedBufferSize = result;
                    _microphoneRepository.SetSelectedBufferSize(result);
                }
                else if (result != null)
                {
                    var errorPopup = new ErrorPopup("Something's wrong with the number");
                    await _mainPage.ShowPopupAsync(errorPopup);
                }
            });

        await _mainPage.ShowPopupAsync(popup);
    }

    [RelayCommand]
    private void ResetVolume()
    {
        Volume = 100.0f;
    }
}