using OpenTalkie.Common.Repositories.Interfaces;

namespace OpenTalkie.Platforms.Android.Common.Repositories;

public class ReceiverRepository : IReceiverRepository
{
    public Action<float> VolumeChanged { get; set; }

    public float GetSelectedVolume()
    {
        return Preferences.Get("ReceiverVolume", 1f);
    }

    public void SetSelectedVolume(float gain)
    {
        Preferences.Set("ReceiverVolume", gain);
        VolumeChanged?.Invoke(gain);
    }
}

