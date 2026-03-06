namespace OpenTalkie.Application.Models;

public readonly record struct PlatformCapabilities(bool IsPlaybackCaptureSupported)
{
    public static PlatformCapabilities None => new(false);
}
