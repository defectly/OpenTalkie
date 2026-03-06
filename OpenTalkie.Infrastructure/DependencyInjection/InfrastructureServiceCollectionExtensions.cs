using Microsoft.Extensions.DependencyInjection;
using OpenTalkie.Application.Abstractions.Repositories;
using OpenTalkie.Application.Abstractions.Services;
using OpenTalkie.Infrastructure.Repositories;
using OpenTalkie.Infrastructure.Services;

namespace OpenTalkie.Infrastructure.DependencyInjection;

public static partial class InfrastructureServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructureLayer(this IServiceCollection services)
    {
        AddPlatformInfrastructure(services);

        services.AddSingleton<IEndpointRepository, EndpointRepository>();
        services.AddSingleton<IEndpointCatalogService, EndpointCatalogService>();
        services.AddSingleton<IEndpointAddressValidator, EndpointAddressValidator>();

        services.AddSingleton<IMicrophoneBroadcastService, MicrophoneBroadcastService>();
        services.AddSingleton<IPlaybackBroadcastService, PlaybackBroadcastService>();
        services.AddSingleton<IReceiverService, ReceiverService>();
        services.AddSingleton<IDenoiseAvailabilityService, DenoiseAvailabilityService>();

        return services;
    }

    static partial void AddPlatformInfrastructure(IServiceCollection services);
}
