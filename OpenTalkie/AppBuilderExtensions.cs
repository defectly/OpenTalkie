﻿using Microsoft.Maui.LifecycleEvents;
using OpenTalkie.Platforms.Android.Common.Services.Playback;

namespace OpenTalkie;

public static class AppBuilderExtensions
{
    /// <summary>
    /// Initializes the .NET MAUI Screen Recording Library
    /// </summary>
    /// <param name="builder"><see cref="MauiAppBuilder"/> generated by <see cref="MauiApp"/>.</param>
    /// <returns><see cref="MauiAppBuilder"/> initialized for <see cref="ScreenAudioCapturing"/>.</returns>
    public static MauiAppBuilder UseScreenAudioCapturing(this MauiAppBuilder builder)
    {
        builder.Services.AddSingleton<IScreenAudioCapturing>(ScreenAudioCapturing.Default);

#if ANDROID
        builder.ConfigureLifecycleEvents(events =>
        {
            events.AddAndroid(android =>
            {
                android.OnActivityResult((activity, requestCode, resultCode, data) =>
                {
                    if (requestCode == MediaProjectionProvider.RequestMediaProjectionCode)
                    {
                        var instance = (MediaProjectionProvider)ScreenAudioCapturing.Default;
                        switch (resultCode)
                        {
                            case Android.App.Result.Ok:
                                instance.OnScreenCapturePermissionGranted((int)resultCode, data);
                                break;
                            case Android.App.Result.Canceled:
                                instance.OnScreenCapturePermissionDenied();
                                break;
                        }
                    }
                });
            });
        });
#endif

        return builder;
    }
}
