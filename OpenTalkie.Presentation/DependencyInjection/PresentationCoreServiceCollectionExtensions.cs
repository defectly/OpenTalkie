using OpenTalkie.Presentation.ViewModels;

namespace OpenTalkie.Presentation.DependencyInjection;

public static class PresentationCoreServiceCollectionExtensions
{
    public static IServiceCollection AddPresentationCoreLayer(this IServiceCollection services)
    {
        services.AddTransient<HomeViewModel>();
        services.AddTransient<MicrophoneStreamsViewModel>();
        services.AddTransient<PlaybackStreamsViewModel>();
        services.AddTransient<ReceiversViewModel>();
        services.AddTransient<StreamSettingsViewModel>();
        services.AddTransient<MicSettingsViewModel>();
        services.AddTransient<PlaybackSettingsViewModel>();
        services.AddTransient<ReceiverSettingsViewModel>();
        services.AddTransient<SettingsViewModel>();
        services.AddTransient<AddStreamViewModel>();

        return services;
    }
}
