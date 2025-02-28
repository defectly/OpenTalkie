using Microsoft.Extensions.Logging;
using OpenTalkie.Repositories;

#if ANDROID
using OpenTalkie.Platforms.Android;
#endif

namespace OpenTalkie;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });

#if DEBUG
        builder.Logging.AddDebug();
#endif
        RegisterRepositories(builder);
        RegisterServices(builder);

        builder.Services.AddSingleton<App>();
        builder.Services.AddTransient<MainPage>();

        return builder.Build();
    }

    public static void RegisterRepositories(MauiAppBuilder builder)
    {
#if ANDROID
        builder.Services.AddSingleton<IParameterRepository, ParameterRepository>();
#endif
        builder.Services.AddSingleton<EndpointRepository>();
    }

    public static void RegisterServices(MauiAppBuilder builder)
    {

#if ANDROID
        builder.Services.AddSingleton<IMicrophoneService, MicrophoneService>();
#endif
        builder.Services.AddSingleton<BroadcastService>();
    }
}
