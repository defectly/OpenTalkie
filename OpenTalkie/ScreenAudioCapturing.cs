using OpenTalkie.Platforms.Android;

namespace OpenTalkie;

public static class ScreenAudioCapturing
{
    static IScreenAudioCapturing? defaultImplementation;

    /// <summary>
    /// Provides the default implementation for static usage of this API.
    /// </summary>
    public static IScreenAudioCapturing Default =>
        defaultImplementation ??= new MediaProjectionProvider();

    internal static void SetDefault(IScreenAudioCapturing? implementation) =>
        defaultImplementation = implementation;
}
