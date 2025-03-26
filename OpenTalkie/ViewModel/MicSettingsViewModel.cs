using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OpenTalkie.Common.Repositories.Interfaces;

namespace OpenTalkie.ViewModel;

public partial class MicSettingsViewModel : ObservableObject
{
    private readonly IMicrophoneRepository _microphoneRepository;

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

    public MicSettingsViewModel(IMicrophoneRepository microphoneRepository)
    {
        _microphoneRepository = microphoneRepository;

        SelectedSource = _microphoneRepository.GetSelectedSource();
        SelectedInputChannel = _microphoneRepository.GetSelectedInputChannel();
        SelectedSampleRate = _microphoneRepository.GetSelectedSampleRate();
        SelectedEncoding = _microphoneRepository.GetSelectedEncoding();
        SelectedBufferSize = _microphoneRepository.GetSelectedBufferSize();
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

        string result = await Application.Current.MainPage.DisplayActionSheet($"Choose {fieldName}", "Cancel", null, options);
        if (result != null && result != "Cancel" && result != currentValue)
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
    }

    [RelayCommand]
    private async Task EditBufferSize()
    {
        string currentValue = SelectedBufferSize ?? _microphoneRepository.GetSelectedBufferSize();
        string result = await Application.Current.MainPage.DisplayPromptAsync
        (
            "Change buffer size",
            "Enter buffer size in bytes:",
            "OK",
            "Cancel",
            currentValue,
            keyboard: Keyboard.Numeric
        );

        if (result != null && int.TryParse(result, out int bufferSize) && bufferSize > 0)
        {
            SelectedBufferSize = result;
            _microphoneRepository.SetSelectedBufferSize(result);
        }
        else if (result != null)
        {
            await Application.Current.MainPage.DisplayAlert("Error", "something's wrong with number", "OK");
        }
    }
}