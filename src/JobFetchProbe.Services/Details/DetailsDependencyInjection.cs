using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace JobFetchProbe.Services.Details;

public static class DetailsDependencyInjection
{
    /// <summary>Registers the details step. Needs AddJobServices for IJobService.</summary>
    public static IServiceCollection AddDetailsServices(this IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System);

        // Transient: it holds IJobService, which holds the factory-made typed HttpClient.
        services.AddTransient<IDetailsService, DetailsService>();
        return services;
    }
}
