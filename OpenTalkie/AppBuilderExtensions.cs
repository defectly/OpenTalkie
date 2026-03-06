using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.Hosting;
using OpenTalkie.Application.Abstractions.Services;
using OpenTalkie.Application.Models;

namespace OpenTalkie;

public static partial class AppBuilderExtensions
{
    /// <summary>
    /// Initializes screen audio capturing services.
    /// </summary>
    public static MauiAppBuilder UseScreenAudioCapturing(this MauiAppBuilder builder)
    {
        builder.Services.AddSingleton<IScreenAudioCapturing, UnsupportedScreenAudioCapturing>();
        ConfigureScreenAudioCapturingPlatform(builder);
        return builder;
    }

    static partial void ConfigureScreenAudioCapturingPlatform(MauiAppBuilder builder);

    private sealed class UnsupportedScreenAudioCapturing : IScreenAudioCapturing
    {
        public bool IsSupported => false;

        public Task<bool> RequestCaptureAsync(ScreenAudioCapturingOptions? options = null) => Task.FromResult(false);

        public void StopCapture()
        {
        }
    }
}
