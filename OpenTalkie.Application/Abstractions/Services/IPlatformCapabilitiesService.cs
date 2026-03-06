using OpenTalkie.Application.Models;

namespace OpenTalkie.Application.Abstractions.Services;

public interface IPlatformCapabilitiesService
{
    PlatformCapabilities GetCapabilities();
}
