using Android.Content;
using Android.Media;
using OpenTalkie.Application.Abstractions.Repositories;
using OpenTalkie.Domain.VBAN;

namespace OpenTalkie.Infrastructure.Android.Platforms.Android.Infrastructure.Repositories;

public sealed class MicrophoneRepository : IMicrophoneRepository
{
    public event Action<float>? VolumeChanged;
    public event Action<string>? PreferredAudioInputDeviceChanged;

    public MicrophoneSettingsState GetSettings()
    {
        var source = (AudioSource)Preferences.Get("MicrophoneSource", (int)AudioSource.Default);
        var inputChannel = (ChannelIn)Preferences.Get("MicrophoneInputChannel", (int)ChannelIn.Default);
        var sampleRate = Preferences.Get("MicrophoneSampleRate", 48000);
        var encoding = (Encoding)Preferences.Get("MicrophoneEncoding", (int)Encoding.Default);
        var bufferSize = Preferences.Get("MicrophoneBufferSize", 960);
        var volume = Preferences.Get("MicrophoneVolume", 1f);
        var preferredDevice = Preferences.Get("MicrophonePrefferedAudioInputDevice", "Default");

        return new MicrophoneSettingsState(
            new SettingOptionItem(source.ToString(), source.ToString()),
            new SettingOptionItem(inputChannel.ToString(), inputChannel.ToString()),
            new SettingOptionItem(sampleRate.ToString(), sampleRate.ToString()),
            new SettingOptionItem(MapFromAndroidEncoding(encoding).ToString(), MapFromAndroidEncoding(encoding).ToString()),
            bufferSize,
            volume,
            new SettingOptionItem(preferredDevice, preferredDevice));
    }

    public IReadOnlyList<SettingOptionItem> GetOptions(MicrophoneSettingOption option)
    {
        return option switch
        {
            MicrophoneSettingOption.Source => Enum.GetNames<AudioSource>().Select(CreateOption).ToArray(),
            MicrophoneSettingOption.InputChannel => Enum.GetNames<ChannelIn>().Select(CreateOption).ToArray(),
            MicrophoneSettingOption.SampleRate => VBANConsts.SAMPLERATES.Order().Select(rate => CreateOption(rate.ToString())).ToArray(),
            MicrophoneSettingOption.Encoding => GetEncodings(),
            MicrophoneSettingOption.PreferredAudioInputDevice => GetAvailableAudioInputDevices(),
            _ => []
        };
    }

    public void SetBufferSize(int bufferSize)
    {
        Preferences.Set("MicrophoneBufferSize", bufferSize);
    }

    public void SetOption(MicrophoneSettingOption option, string value)
    {
        switch (option)
        {
            case MicrophoneSettingOption.Source:
                Preferences.Set("MicrophoneSource", (int)Enum.Parse<AudioSource>(value));
                break;
            case MicrophoneSettingOption.InputChannel:
                Preferences.Set("MicrophoneInputChannel", (int)Enum.Parse<ChannelIn>(value));
                break;
            case MicrophoneSettingOption.SampleRate:
                Preferences.Set("MicrophoneSampleRate", int.Parse(value));
                break;
            case MicrophoneSettingOption.Encoding:
                Preferences.Set("MicrophoneEncoding", (int)MapToAndroidEncoding(int.Parse(value)));
                break;
            case MicrophoneSettingOption.PreferredAudioInputDevice:
                Preferences.Set("MicrophonePrefferedAudioInputDevice", value);
                PreferredAudioInputDeviceChanged?.Invoke(value);
                break;
            default:
                throw new NotSupportedException($"Unsupported microphone setting option: {option}");
        }
    }

    public void SetSelectedVolume(float gain)
    {
        Preferences.Set("MicrophoneVolume", gain);
        VolumeChanged?.Invoke(gain);
    }

    private static IReadOnlyList<SettingOptionItem> GetEncodings()
    {
        return OperatingSystem.IsAndroidVersionAtLeast(31)
            ? [CreateOption("8"), CreateOption("16"), CreateOption("24"), CreateOption("32")]
            : [CreateOption("8"), CreateOption("16")];
    }

    private static IReadOnlyList<SettingOptionItem> GetAvailableAudioInputDevices()
    {
        if (!OperatingSystem.IsAndroidVersionAtLeast(23))
        {
            return [CreateOption("Default")];
        }

        var context = Platform.AppContext;
        var audioManager = (AudioManager?)context.GetSystemService(Context.AudioService);
        if (audioManager == null)
        {
            return [CreateOption("Default")];
        }

        var devices = audioManager.GetDevices(GetDevicesTargets.Inputs);
        if (devices is null)
        {
            return [CreateOption("Default")];
        }

        return [.. Enumerable.Concat([CreateOption("Default")], devices.Select(device => CreateOption(device.Type.ToString())))];
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
