using Android.Media;
using OpenTalkie.Application.Abstractions.Repositories;
using OpenTalkie.Domain.VBAN;

namespace OpenTalkie.Infrastructure.Android.Platforms.Android.Infrastructure.Repositories;

public sealed class PlaybackRepository : IPlaybackRepository
{
    public event Action<float>? VolumeChanged;

    public PlaybackSettingsState GetSettings()
    {
        var channelOut = (ChannelOut)Preferences.Get("PlaybackChannelOut", (int)ChannelOut.Stereo);
        var sampleRate = Preferences.Get("PlaybackSampleRate", 48000);
        var encoding = (Encoding)Preferences.Get("PlaybackEncoding", (int)Encoding.Default);
        var bufferSize = Preferences.Get("PlaybackBufferSize", 1920);
        var volume = Preferences.Get("PlaybackVolume", 1f);

        var encodingBits = MapFromAndroidEncoding(encoding).ToString();

        return new PlaybackSettingsState(
            new SettingOptionItem(channelOut.ToString(), channelOut.ToString()),
            new SettingOptionItem(sampleRate.ToString(), sampleRate.ToString()),
            new SettingOptionItem(encodingBits, encodingBits),
            bufferSize,
            volume);
    }

    public IReadOnlyList<SettingOptionItem> GetOptions(PlaybackSettingOption option)
    {
        return option switch
        {
            PlaybackSettingOption.ChannelOut => Enum.GetNames<ChannelOut>().Select(CreateOption).ToArray(),
            PlaybackSettingOption.SampleRate => VBANConsts.SAMPLERATES.Order().Select(rate => CreateOption(rate.ToString())).ToArray(),
            PlaybackSettingOption.Encoding => GetEncodings(),
            _ => []
        };
    }

    public void SetBufferSize(int bufferSize)
    {
        Preferences.Set("PlaybackBufferSize", bufferSize);
    }

    public void SetOption(PlaybackSettingOption option, string value)
    {
        switch (option)
        {
            case PlaybackSettingOption.ChannelOut:
                Preferences.Set("PlaybackChannelOut", (int)Enum.Parse<ChannelOut>(value));
                break;
            case PlaybackSettingOption.SampleRate:
                Preferences.Set("PlaybackSampleRate", int.Parse(value));
                break;
            case PlaybackSettingOption.Encoding:
                Preferences.Set("PlaybackEncoding", (int)MapToAndroidEncoding(int.Parse(value)));
                break;
            default:
                throw new NotSupportedException($"Unsupported playback setting option: {option}");
        }
    }

    public void SetSelectedVolume(float gain)
    {
        Preferences.Set("PlaybackVolume", gain);
        VolumeChanged?.Invoke(gain);
    }

    private static IReadOnlyList<SettingOptionItem> GetEncodings()
    {
        return OperatingSystem.IsAndroidVersionAtLeast(31)
            ? [CreateOption("8"), CreateOption("16"), CreateOption("24"), CreateOption("32")]
            : [CreateOption("8"), CreateOption("16")];
    }

    private static int MapFromAndroidEncoding(Encoding encoding)
    {
        if (encoding == Encoding.Default || encoding == Encoding.Pcm16bit || encoding == Encoding.Invalid)
            return 16;
        if (encoding == Encoding.Pcm8bit)
            return 8;

        if (OperatingSystem.IsAndroidVersionAtLeast(31))
        {
            if (encoding == Encoding.Pcm24bitPacked)
                return 24;
            if (encoding == Encoding.Pcm32bit)
                return 32;
        }

        throw new NotSupportedException($"No such encoding supported: {encoding}");
    }

    private static Encoding MapToAndroidEncoding(int encoding)
    {
        return encoding switch
        {
            8 => Encoding.Pcm8bit,
            16 => Encoding.Pcm16bit,
            24 => OperatingSystem.IsAndroidVersionAtLeast(31) ? Encoding.Pcm24bitPacked : throw new NotSupportedException("Pcm24bitPacked supported on sdk 31 or higher"),
            32 => OperatingSystem.IsAndroidVersionAtLeast(31) ? Encoding.Pcm32bit : throw new NotSupportedException("Pcm32bit supported on sdk 31 or higher"),
            _ => throw new NotSupportedException($"No such encoding supported: {encoding}")
        };
    }

    private static SettingOptionItem CreateOption(string value)
    {
        return new(value, value);
    }
}
