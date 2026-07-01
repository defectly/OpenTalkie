using OpenTalkie.Application.Abstractions.Services;
using System.Net;

namespace OpenTalkie.Infrastructure.Services;

public sealed class EndpointAddressValidator : IEndpointAddressValidator
{
    private readonly ILogger<EndpointAddressValidator> logger;

    public EndpointAddressValidator(ILogger<EndpointAddressValidator> logger)
    {
        this.logger = logger;
    }

    public bool CanResolveHost(string hostname)
    {
        try
        {
            if (IPAddress.TryParse(hostname, out _))
            {
                return true;
            }

            var addresses = Dns.GetHostAddresses(hostname);
            var resolved = addresses.Length > 0;

            if (logger.IsEnabled(LogLevel.Debug))
                logger.LogDebug("Resolved host {Host} to {AddressCount} address(es).", hostname, addresses.Length);

            return resolved;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to resolve host {Host}.", hostname);
            return false;
        }
    }
}
