namespace OpenTalkie.Common.Repositories.Interfaces;

public interface IReceiverRepository
{
    Action<float> VolumeChanged { get; set; }
    float GetSelectedVolume();
    void SetSelectedVolume(float gain);
}

