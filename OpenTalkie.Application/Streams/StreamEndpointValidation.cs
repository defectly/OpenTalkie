using OpenTalkie.Domain.Enums;
using OpenTalkie.Domain.Rules;

namespace OpenTalkie.Application.Streams;

internal static class StreamEndpointValidation
{
    public static string? ValidateName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return "Name cannot be empty.";
        }

        if (name.Length > 16)
        {
            return "Name cannot be longer than 16 characters.";
        }

        return null;
    }

    public static string? ValidateHostname(string hostname)
    {
        if (string.IsNullOrWhiteSpace(hostname))
        {
            return "Hostname cannot be empty.";
        }

        var hostType = Uri.CheckHostName(hostname);
        if (hostType is not UriHostNameType.IPv4 and not UriHostNameType.IPv6 and not UriHostNameType.Dns)
        {
            return "Invalid hostname format.";
        }

        return null;
    }

    public static string? ValidatePort(int port)
    {
        return port <= 0 || port > 65535
            ? "Invalid port number (must be 1-65535)."
            : null;
    }

    public static string? ValidateVolume(float volume)
    {
        return volume < 0f || volume > 4f
            ? "Volume must be in range 0.0 - 4.0."
            : null;
    }

    public static bool HasIdentityCollision(
        IEnumerable<Endpoint> endpoints,
        EndpointType streamType,
        Guid? excludingEndpointId,
        string streamName,
        string hostname,
        int port)
    {
        return EndpointIdentityRules.HasCollision(
            endpoints,
            streamType,
            excludingEndpointId,
            streamName,
            hostname,
            port);
    }
}
