namespace OpenTalkie.Application.Abstractions.Services;

public interface IEndpointAddressValidator
{
    bool CanResolveHost(string hostname);
}
