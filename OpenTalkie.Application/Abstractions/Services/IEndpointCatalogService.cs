using OpenTalkie.Domain.Enums;

namespace OpenTalkie.Application.Abstractions.Services;

public interface IEndpointCatalogService
{
    event Action<EndpointType>? EndpointsChanged;

    IReadOnlyList<Endpoint> GetEndpoints(EndpointType streamType);
    Endpoint? GetEndpoint(EndpointType streamType, Guid endpointId);
    void AddEndpoint(Endpoint endpoint);
    bool UpdateEndpoint(Endpoint endpoint);
    bool RemoveEndpoint(EndpointType streamType, Guid endpointId);
}
