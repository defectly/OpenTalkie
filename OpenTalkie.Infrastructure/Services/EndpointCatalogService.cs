using OpenTalkie.Application.Abstractions.Repositories;
using OpenTalkie.Application.Abstractions.Services;
using OpenTalkie.Domain.Enums;

namespace OpenTalkie.Infrastructure.Services;

public sealed class EndpointCatalogService : IEndpointCatalogService
{
    private readonly Dictionary<EndpointType, List<Endpoint>> _endpointsByType;

    public EndpointCatalogService(IEndpointRepository endpointRepository)
    {
        _endpointsByType = endpointRepository
            .List()
            .GroupBy(endpoint => endpoint.Type)
            .ToDictionary(group => group.Key, group => group.ToList());
    }

    public event Action<EndpointType>? EndpointsChanged;

    public IReadOnlyList<Endpoint> GetEndpoints(EndpointType streamType)
    {
        return _endpointsByType.TryGetValue(streamType, out var endpoints)
            ? endpoints.AsReadOnly()
            : [];
    }

    public Endpoint? GetEndpoint(EndpointType streamType, Guid endpointId)
    {
        return _endpointsByType.TryGetValue(streamType, out var endpoints)
            ? endpoints.FirstOrDefault(endpoint => endpoint.Id == endpointId)
            : null;
    }

    public void AddEndpoint(Endpoint endpoint)
    {
        if (!_endpointsByType.TryGetValue(endpoint.Type, out var endpoints))
        {
            endpoints = [];
            _endpointsByType[endpoint.Type] = endpoints;
        }

        endpoints.Add(endpoint);
        EndpointsChanged?.Invoke(endpoint.Type);
    }

    public bool UpdateEndpoint(Endpoint endpoint)
    {
        if (!_endpointsByType.TryGetValue(endpoint.Type, out var endpoints))
        {
            return false;
        }

        var index = endpoints.FindIndex(existing => existing.Id == endpoint.Id);
        if (index < 0)
        {
            return false;
        }

        endpoints[index] = endpoint;
        EndpointsChanged?.Invoke(endpoint.Type);
        return true;
    }

    public bool RemoveEndpoint(EndpointType streamType, Guid endpointId)
    {
        if (!_endpointsByType.TryGetValue(streamType, out var endpoints))
        {
            return false;
        }

        var removed = endpoints.RemoveAll(endpoint => endpoint.Id == endpointId) > 0;
        if (removed)
        {
            EndpointsChanged?.Invoke(streamType);
        }

        return removed;
    }
}
