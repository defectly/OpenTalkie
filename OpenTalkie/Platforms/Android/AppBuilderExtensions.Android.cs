using Microsoft.Extensions.Logging;
using Microsoft.Maui.LifecycleEvents;
using OpenTalkie.Application.Abstractions.Services;
using OpenTalkie.Infrastructure.Android.Platforms.Android.Infrastructure.Services.Playback;

namespace OpenTalkie;

public static partial class AppBuilderExtensions
{
    static partial void ConfigureScreenAudioCapturingPlatform(MauiAppBuilder builder)
    {
        builder.Services.AddSingleton<MediaProjectionProvider>();
        builder.Services.AddSingleton<IScreenAudioCapturing>(sp => sp.GetRequiredService<MediaProjectionProvider>());

        builder.ConfigureLifecycleEvents(events =>
        {
            events.AddAndroid(android =>
            {
                android.OnActivityResult((activity, requestCode, resultCode, data) =>
                {
                    var logger = IPlatformApplication.Current?.Services.GetService<ILoggerFactory>()?.CreateLogger("OpenTalkie.AndroidLifecycle");
                    if (requestCode != MediaProjectionProvider.RequestMediaProjectionCode)
                    {
                        if (logger?.IsEnabled(LogLevel.Trace) == true)
                            logger.LogTrace("Ignoring Android activity result with request code {RequestCode}.", requestCode);

                        return;
                    }

                    var provider = IPlatformApplication.Current?.Services.GetService<MediaProjectionProvider>();
                    if (provider == null)
                    {
                        logger?.LogError("MediaProjectionProvider was unavailable while handling screen capture activity result.");
                        return;
                    }

                    switch (resultCode)
                    {
                        case Android.App.Result.Ok:
                            logger?.LogInformation("Android screen capture activity result granted.");
                            provider.OnScreenCapturePermissionGranted((int)resultCode, data);
                            break;
                        case Android.App.Result.Canceled:
                            logger?.LogWarning("Android screen capture activity result canceled.");
                            provider.OnScreenCapturePermissionDenied();
                            break;
                        default:
                            if (logger?.IsEnabled(LogLevel.Warning) == true)
                                logger.LogWarning("Android screen capture activity result returned unexpected code {ResultCode}.", resultCode);
                            break;
                    }
                });
            });
        });
    }
}


