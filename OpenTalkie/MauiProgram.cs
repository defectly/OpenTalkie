using CommunityToolkit.Maui;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.Controls.Hosting;
using Microsoft.Maui.Hosting;
using OpenTalkie.Application.DependencyInjection;
using OpenTalkie.DependencyInjection;
using OpenTalkie.Infrastructure.Android.DependencyInjection;
using OpenTalkie.Infrastructure.DependencyInjection;

namespace OpenTalkie;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
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
