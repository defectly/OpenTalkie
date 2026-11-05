using OpenTalkie.Domain.Enums;

namespace OpenTalkie.Application.Abstractions.Repositories;

public interface IVolumeLimitRepository
{
    float GetMaximum(EndpointType streamType);
    void SetMaximum(EndpointType streamType, float maximum);
}
