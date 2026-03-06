using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui;
using Microsoft.Maui.Hosting;
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
                    if (requestCode != MediaProjectionProvider.RequestMediaProjectionCode)
                    {
                        return;
                    }

                    var provider = IPlatformApplication.Current?.Services.GetService<MediaProjectionProvider>();
                    if (provider == null)
                    {
                        return;
                    }

                    switch (resultCode)
                    {
                        case Android.App.Result.Ok:
                            provider.OnScreenCapturePermissionGranted((int)resultCode, data);
                            break;
                        case Android.App.Result.Canceled:
                            provider.OnScreenCapturePermissionDenied();
                            break;
                    }
                });
            });
        });
    }
}


