using OpenTalkie.Application.Settings;

namespace OpenTalkie.Application.Abstractions.Repositories;

public interface IReceiverRepository
{
    event Action<float>? VolumeChanged;
    event Action<string>? PreferredAudioOutputDeviceChanged;

    ReceiverSettingsState GetSettings();
    IReadOnlyList<SettingOptionItem> GetAudioOutputOptions();
    void SetPreferredDevice(string preferredDevice);
    void SetSelectedVolume(float gain);
}
