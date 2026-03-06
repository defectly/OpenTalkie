using OpenTalkie.Application.Settings;

namespace OpenTalkie.Application.Abstractions.Repositories;

public interface IPlaybackRepository
{
    event Action<float>? VolumeChanged;

    PlaybackSettingsState GetSettings();
    IReadOnlyList<SettingOptionItem> GetOptions(PlaybackSettingOption option);
    void SetBufferSize(int bufferSize);
    void SetOption(PlaybackSettingOption option, string value);
    void SetSelectedVolume(float gain);
}
