namespace OpenTalkie.Common.Repositories.Interfaces;

public interface IReceiverRepository
{
    Action<float> VolumeChanged { get; set; }
    Action<string> PrefferedAudioOutputDeviceChanged { get; set; }

    string[] GetAvailableAudioOutputDevices();
    string GetPrefferedDevice();
    float GetSelectedVolume();
    void SetPrefferedDevice(string prefferedDevice);
    void SetSelectedVolume(float gain);
}

