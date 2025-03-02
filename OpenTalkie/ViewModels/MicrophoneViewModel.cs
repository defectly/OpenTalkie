using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;

namespace OpenTalkie.ViewModels;

public class MicrophoneViewModel
{
    private readonly IMicrophoneRepository _microphoneRepository;

    public ObservableCollection<SettingViewModel> Settings { get; set; } = [];

    public MicrophoneViewModel(IMicrophoneRepository microphoneRepository)
    {
        Settings.Add(new SettingViewModel("MicrophoneSource", microphoneRepository.GetAudioSources()));
        Settings.Add(new SettingViewModel("MicrophoneChannel", microphoneRepository.GetInputChannels()));
        Settings.Add(new SettingViewModel("MicrophoneSampleRate", microphoneRepository.GetSampleRates()));
        Settings.Add(new SettingViewModel("MicrophoneEncoding", microphoneRepository.GetEncodings()));

        Settings[0].SelectedValue = microphoneRepository.GetSelectedSource();
        Settings[1].SelectedValue = microphoneRepository.GetSelectedInputChannel();
        Settings[2].SelectedValue = microphoneRepository.GetSelectedSampleRate();
        Settings[3].SelectedValue = microphoneRepository.GetSelectedEncoding();

        _microphoneRepository = microphoneRepository;
    }

    [RelayCommand]
    private void UpdateRepository()
    {
        _microphoneRepository
            .SetSelectedSource(Settings.Single(s => s.Name == "MicrophoneSource").SelectedValue);
        _microphoneRepository
            .SetSelectedInputChannel(Settings.Single(s => s.Name == "MicrophoneChannel").SelectedValue);
        _microphoneRepository
            .SetSelectedSampleRate(Settings.Single(s => s.Name == "MicrophoneSampleRate").SelectedValue);
        _microphoneRepository
            .SetSelectedEncoding(Settings.Single(s => s.Name == "MicrophoneEncoding").SelectedValue);
    }
}
