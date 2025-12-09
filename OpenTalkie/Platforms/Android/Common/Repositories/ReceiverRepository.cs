using Android.Content;
using Android.Media;
using OpenTalkie.Common.Repositories.Interfaces;

namespace OpenTalkie.Platforms.Android.Common.Repositories;

public class ReceiverRepository : IReceiverRepository
{
    public Action<float> VolumeChanged { get; set; }
    public Action<string> PrefferedAudioOutputDeviceChanged { get; set; }

    public float GetSelectedVolume()
    {
        return Preferences.Get("ReceiverVolume", 1f);
    }

    public void SetSelectedVolume(float gain)
    {
        Preferences.Set("ReceiverVolume", gain);
        VolumeChanged?.Invoke(gain);
    }

    public string GetPrefferedDevice()
    {
        return Preferences.Get("ReceiverPrefferedAudioOutputDevice", "Default");
    }

    public string[] GetAvailableAudioOutputDevices()
    {
        if (!OperatingSystem.IsAndroidVersionAtLeast(23))
            return ["Default"];

        var context = Platform.AppContext;
        var audioManager = (AudioManager?)context.GetSystemService(Context.AudioService);
        if (audioManager == null)
            return ["Default"];

        var devices = audioManager.GetDevices(GetDevicesTargets.Outputs);
        if (devices is null)
            return ["Default"];

        return Enumerable.Concat(["Default"], devices.Select(d => d.Type.ToString())).ToArray();
    }

    public void SetPrefferedDevice(string prefferedDevice)
    {
        if (string.IsNullOrWhiteSpace(prefferedDevice))
            return;

        if (prefferedDevice == "Default")
            Preferences.Set("ReceiverPrefferedAudioOutputDevice", "Default");
        else
            Preferences.Set("ReceiverPrefferedAudioOutputDevice", prefferedDevice);

        PrefferedAudioOutputDeviceChanged?.Invoke(prefferedDevice);
    }
}

