using Microsoft.Extensions.DependencyInjection;

namespace OpenTalkie.Application.DependencyInjection;

public static class ApplicationServiceCollectionExtensions
{
    public static IServiceCollection AddApplicationLayer(this IServiceCollection services)
    {
        services.AddMediator(options =>
        {
            options.ServiceLifetime = ServiceLifetime.Singleton;
        });

        return services;
    }
}
