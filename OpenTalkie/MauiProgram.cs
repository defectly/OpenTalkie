using CommunityToolkit.Maui;
using OpenTalkie.Common.Repositories;
using OpenTalkie.Common.Repositories.Interfaces;
using OpenTalkie.Common.Services;
using OpenTalkie.Common.Services.Interfaces;
using OpenTalkie.View;
using OpenTalkie.ViewModel;
using System.Reflection;
using Microsoft.Extensions.Logging;

#if ANDROID
using OpenTalkie.Platforms.Android.Common.Repositories;
using OpenTalkie.Platforms.Android.Common.Services;
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
            })
            .UseScreenAudioCapturing();

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
        services.AddSingleton<IPlaybackRepository, PlaybackRepository>();
#endif
        services.AddSingleton<IEndpointRepository, EndpointRepository>();
    }

    public static void RegisterServices(IServiceCollection services)
    {

#if ANDROID
        services.AddSingleton<IMicrophoneService, MicrophoneService>();
        services.AddSingleton<IPlaybackService, PlaybackService>();
#endif
        services.AddSingleton<MicrophoneBroadcastService>();
        services.AddSingleton<PlaybackBroadcastService>();
    }
    private static void RegisterViewModels(IServiceCollection services)
    {
        services.AddTransient<HomeViewModel>();
        services.AddTransient<MicrophoneStreamsViewModel>();
        services.AddTransient<PlaybackStreamsViewModel>();
        services.AddTransient<StreamSettingsViewModel>();
        services.AddTransient<MicSettingsViewModel>();
        services.AddTransient<PlaybackSettingsViewModel>();
        services.AddTransient<SettingsViewModel>();
    }
    private static void RegisterViews(IServiceCollection services)
    {
        services.AddTransient<HomePage>();
        services.AddTransient<MicrophoneStreamsPage>();
        services.AddTransient<PlaybackStreamsPage>();
        services.AddTransient<StreamSettingsPage>();
        services.AddTransient<MicSettingsPage>();
        services.AddTransient<PlaybackSettingsPage>();
        services.AddTransient<SettingsPage>();
    }
}
