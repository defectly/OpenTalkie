using Microsoft.Extensions.Logging;
using OpenTalkie.Repositories;
using OpenTalkie.ViewModels;
using OpenTalkie.Views;

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
        builder.Services.AddSingleton<App>();
        builder.Services.AddSingleton<AppShell>();
        builder.Services.AddTransient<MainPage>();

        RegisterRepositories(builder.Services);
        RegisterServices(builder.Services);
        RegisterViewModels(builder.Services);
        RegisterViews(builder.Services);

        return builder.Build();
    }

    public static void RegisterRepositories(IServiceCollection services)
    {
#if ANDROID
        services.AddSingleton<IMicrophoneRepository, MicrophoneRepository>();
#endif
        services.AddSingleton<EndpointRepository>();
    }

    public static void RegisterServices(IServiceCollection services)
    {

#if ANDROID
        services.AddSingleton<IMicrophoneService, MicrophoneService>();
#endif
        services.AddSingleton<BroadcastService>();
    }
    private static void RegisterViewModels(IServiceCollection services)
    {
        services.AddTransient<MicrophoneViewModel>();
    }
    private static void RegisterViews(IServiceCollection services)
    {
        services.AddTransient<MicrophoneView>();
    }
}
