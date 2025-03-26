using Microsoft.Extensions.Logging;
using OpenTalkie.ViewModel;
using OpenTalkie.View;
using CommunityToolkit.Maui;
using System.Reflection;
using OpenTalkie.Common.Repositories.Interfaces;
using OpenTalkie.Common.Repositories;
using OpenTalkie.Common.Services;


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
            .UseMauiCommunityToolkit()
            .UseMauiApp<App>()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });

#if DEBUG
        builder.Logging.AddDebug();
#endif
        builder.Services.AddAutoMapper(Assembly.GetExecutingAssembly());
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
        services.AddSingleton<IEndpointRepository, EndpointRepository>();
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
        services.AddTransient<HomeViewModel>();
        services.AddTransient<StreamsViewModel>();
        services.AddTransient<StreamSettingsViewModel>();
        services.AddTransient<MicSettingsViewModel>();
    }
    private static void RegisterViews(IServiceCollection services)
    {
        services.AddTransient<HomePage>();
        services.AddTransient<StreamsPage>();
        services.AddTransient<StreamSettingsPage>();
        services.AddTransient<MicSettingsPage>();
    }
}
