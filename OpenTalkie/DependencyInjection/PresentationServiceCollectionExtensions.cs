using Microsoft.Extensions.DependencyInjection;
using OpenTalkie.Presentation.Abstractions.Services;
using OpenTalkie.Presentation.DependencyInjection;
using OpenTalkie.Presentation.Views;
using OpenTalkie.Services;

namespace OpenTalkie.DependencyInjection;

public static class PresentationServiceCollectionExtensions
{
    public static IServiceCollection AddPresentationLayer(this IServiceCollection services)
    {
        services.AddPresentationCoreLayer();

        services.AddSingleton<INavigationService, ShellNavigationService>();
        services.AddSingleton<IUserDialogService, UserDialogService>();

        services.AddTransient<HomePage>();
        services.AddTransient<MicrophoneStreamsPage>();
        services.AddTransient<PlaybackStreamsPage>();
        services.AddTransient<ReceiversPage>();
        services.AddTransient<StreamSettingsPage>();
        services.AddTransient<MicSettingsPage>();
        services.AddTransient<PlaybackSettingsPage>();
        services.AddTransient<ReceiverSettingsPage>();
        services.AddTransient<SettingsPage>();
        services.AddTransient<AddStreamPage>();

        return services;
    }
}
