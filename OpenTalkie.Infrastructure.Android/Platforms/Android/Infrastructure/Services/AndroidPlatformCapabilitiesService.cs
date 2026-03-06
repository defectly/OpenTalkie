using OpenTalkie.Application.Abstractions.Services;
using OpenTalkie.Application.Models;

namespace OpenTalkie.Infrastructure.Android.Platforms.Android.Infrastructure.Services;

public sealed class AndroidPlatformCapabilitiesService : IPlatformCapabilitiesService
{
    public PlatformCapabilities GetCapabilities() => new(OperatingSystem.IsAndroidVersionAtLeast(29));
}
