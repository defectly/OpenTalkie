using OpenTalkie.Domain.Enums;
using OpenTalkie.Domain.Models;

namespace OpenTalkie.Domain.Rules;

public static class EndpointIdentityRules
{
    public static bool HasCollision(
        IEnumerable<Endpoint> endpoints,
        EndpointType streamType,
        Guid? excludingEndpointId,
        string streamName,
        string hostname,
        int port)
    {
        foreach (var endpoint in endpoints)
        {
            if (excludingEndpointId.HasValue && endpoint.Id == excludingEndpointId.Value)
            {
                continue;
            }

            if (Collides(
                streamType,
                streamName,
                hostname,
                port,
                endpoint.Type,
                endpoint.Name,
                endpoint.Hostname,
                endpoint.Port))
            {
                return true;
            }
        }

        return false;
    }

    public static bool Collides(
        EndpointType candidateType,
        string? candidateName,
        string? candidateHostname,
        int candidatePort,
        EndpointType existingType,
        string? existingName,
        string? existingHostname,
        int existingPort)
    {
        if (candidateType != existingType)
        {
            return false;
        }

        if (candidatePort != existingPort)
        {
            return false;
        }

        if (!VbanStreamName16.EqualsName(candidateName, existingName))
        {
            return false;
        }

        if (candidateType == EndpointType.Receiver)
        {
            return true;
        }

        return string.Equals(candidateHostname, existingHostname, StringComparison.OrdinalIgnoreCase);
    }
}
