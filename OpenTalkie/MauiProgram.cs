using CommunityToolkit.Maui;
using Microsoft.Extensions.Logging;
using OpenTalkie.Application.DependencyInjection;
using OpenTalkie.DependencyInjection;
using OpenTalkie.Infrastructure.Android.DependencyInjection;
using OpenTalkie.Infrastructure.DependencyInjection;
#if ANDROID
using Microsoft.Maui.Handlers;
using Microsoft.Maui.Platform;
#endif

namespace OpenTalkie;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
#if ANDROID
        SliderHandler.Mapper.AppendToMapping("TrackThickness", (handler, slider) =>
        {
            handler.PlatformView.ProgressDrawable = handler.PlatformView.Context?.GetDrawable(Resource.Drawable.slider_track);
            handler.PlatformView.UpdateMinimumTrackColor(slider);
            handler.PlatformView.UpdateMaximumTrackColor(slider);
        });
#endif
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiCommunityToolkit()
            .UseMauiApp<App>()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            })
            .UseScreenAudioCapturing();

#if DEBUG
        builder.Logging.AddDebug();
#endif

        builder.Services.AddApplicationLayer();
        builder.Services.AddInfrastructureLayer();
#if ANDROID
        builder.Services.AddAndroidInfrastructureLayer();
#endif
        builder.Services.AddPresentationLayer();

        builder.Services.AddSingleton<App>();
        builder.Services.AddSingleton<AppShell>();

        return builder.Build();
    }
}
