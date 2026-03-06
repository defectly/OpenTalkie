using Android.Content;
using Android.Media;
using OpenTalkie.Application.Abstractions.Repositories;

namespace OpenTalkie.Infrastructure.Android.Platforms.Android.Infrastructure.Repositories;

public sealed class ReceiverRepository : IReceiverRepository
{
    public event Action<float>? VolumeChanged;
    public event Action<string>? PreferredAudioOutputDeviceChanged;

    public ReceiverSettingsState GetSettings()
    {
        var preferredDevice = Preferences.Get("ReceiverPrefferedAudioOutputDevice", "Default");
        return new ReceiverSettingsState(
            Preferences.Get("ReceiverVolume", 1f),
            new SettingOptionItem(preferredDevice, preferredDevice));
    }

    public IReadOnlyList<SettingOptionItem> GetAudioOutputOptions()
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

        var devices = audioManager.GetDevices(GetDevicesTargets.Outputs);
        if (devices is null)
        {
            return [CreateOption("Default")];
        }

        return [.. Enumerable.Concat([CreateOption("Default")], devices.Select(device => CreateOption(device.Type.ToString())))];
    }

    public void SetPreferredDevice(string preferredDevice)
    {
        if (string.IsNullOrWhiteSpace(preferredDevice))
        {
            return;
        }

        Preferences.Set("ReceiverPrefferedAudioOutputDevice", preferredDevice);
        PreferredAudioOutputDeviceChanged?.Invoke(preferredDevice);
    }

    public void SetSelectedVolume(float gain)
    {
        Preferences.Set("ReceiverVolume", gain);
        VolumeChanged?.Invoke(gain);
    }

    private static SettingOptionItem CreateOption(string value)
    {
        return new(value, value);
    }
}
