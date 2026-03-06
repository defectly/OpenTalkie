namespace OpenTalkie.Application.Settings;

public enum MicrophoneSettingOption
{
    Source,
    InputChannel,
    SampleRate,
    Encoding,
    PreferredAudioInputDevice
}

public enum PlaybackSettingOption
{
    ChannelOut,
    SampleRate,
    Encoding
}

public readonly record struct SettingOptionItem(string Value, string DisplayName)
{
    public override string ToString() => DisplayName;
}

public readonly record struct MicrophoneSettingsState(
    SettingOptionItem SelectedSource,
    SettingOptionItem SelectedInputChannel,
    SettingOptionItem SelectedSampleRate,
    SettingOptionItem SelectedEncoding,
    int SelectedBufferSize,
    float VolumeGain,
    SettingOptionItem PreferredAudioInputDevice);

public readonly record struct PlaybackSettingsState(
    SettingOptionItem SelectedChannelOut,
    SettingOptionItem SelectedSampleRate,
    SettingOptionItem SelectedEncoding,
    int SelectedBufferSize,
    float VolumeGain);

public readonly record struct ReceiverSettingsState(
    float VolumeGain,
    SettingOptionItem PreferredAudioOutputDevice);
