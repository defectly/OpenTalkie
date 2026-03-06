using OpenTalkie.Application.Abstractions.Repositories;
using OpenTalkie.Application.Abstractions.Services;
using OpenTalkie.Infrastructure.Android.Platforms.Android.Infrastructure.Repositories;
using OpenTalkie.Infrastructure.Android.Platforms.Android.Infrastructure.Services;
using OpenTalkie.Infrastructure.Android.Platforms.Android.Infrastructure.Services.Microphone;
using OpenTalkie.Infrastructure.Android.Platforms.Android.Infrastructure.Services.Output;
using OpenTalkie.Infrastructure.Android.Platforms.Android.Infrastructure.Services.Playback;
using OpenTalkie.Infrastructure.Android.Platforms.Android.Infrastructure.Services.Receiver;

namespace OpenTalkie.Infrastructure.Android.DependencyInjection;

public static class AndroidInfrastructureServiceCollectionExtensions
{
    public static IServiceCollection AddAndroidInfrastructureLayer(this IServiceCollection services)
    {
        services.AddSingleton<IMicrophoneRepository, MicrophoneRepository>();
        services.AddSingleton<IPlaybackRepository, PlaybackRepository>();
        services.AddSingleton<IReceiverRepository, ReceiverRepository>();

        services.AddSingleton<IMicrophoneCapturingService, MicrophoneCapturingService>();
        services.AddSingleton<IPlaybackService, PlaybackService>();
        services.AddSingleton<IWakeLockService, WakeLockService>();
        services.AddSingleton<IMicrophonePermissionService, MicrophonePermissionService>();
        services.AddSingleton<IPlatformCapabilitiesService, AndroidPlatformCapabilitiesService>();
        services.AddSingleton<IAudioOutputService, AudioOutputService>();
        services.AddSingleton<IReceiverForegroundServiceController, ReceiverForegroundServiceController>();

        return services;
    }
}
