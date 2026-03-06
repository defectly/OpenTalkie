using OpenTalkie.Application.Abstractions.Services;
using System.Net;

namespace OpenTalkie.Infrastructure.Services;

public sealed class EndpointAddressValidator : IEndpointAddressValidator
{
    public bool CanResolveHost(string hostname)
    {
        try
        {
            if (IPAddress.TryParse(hostname, out _))
            {
                return true;
            }

            var addresses = Dns.GetHostAddresses(hostname);
            return addresses.Length > 0;
        }
        catch
        {
            return false;
        }
    }
}
