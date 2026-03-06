using OpenTalkie.Application.Settings;

namespace OpenTalkie.Application.Abstractions.Repositories;

public interface IMicrophoneRepository
{
    event Action<float>? VolumeChanged;
    event Action<string>? PreferredAudioInputDeviceChanged;

    MicrophoneSettingsState GetSettings();
    IReadOnlyList<SettingOptionItem> GetOptions(MicrophoneSettingOption option);
    void SetBufferSize(int bufferSize);
    void SetOption(MicrophoneSettingOption option, string value);
    void SetSelectedVolume(float gain);
}
