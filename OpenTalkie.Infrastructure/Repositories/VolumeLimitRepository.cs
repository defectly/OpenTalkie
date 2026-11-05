using OpenTalkie.Application.Abstractions.Repositories;
using OpenTalkie.Domain.Enums;

namespace OpenTalkie.Infrastructure.Repositories;

public sealed class VolumeLimitRepository : IVolumeLimitRepository
{
    public float GetMaximum(EndpointType streamType)
    {
        float maximum = Preferences.Get($"{streamType}MaximumVolume", Preferences.Get("MaximumVolume", 1f));
        return float.IsFinite(maximum) && maximum >= 1f ? maximum : 1f;
    }

    public void SetMaximum(EndpointType streamType, float maximum)
    {
        Preferences.Set($"{streamType}MaximumVolume", maximum);
    }
}
