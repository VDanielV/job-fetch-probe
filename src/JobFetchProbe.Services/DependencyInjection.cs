using JobFetchProbe.Services.Parsing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace JobFetchProbe.Services;

public static class DependencyInjection
{
    public static IServiceCollection AddJobServices(this IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton<IJobPageParser, JobPageParser>();

        // Transient, because it depends on the typed HttpClient, which must not be held by a singleton
        // (that would pin one handler forever and defeat the factory's handler rotation).
        services.AddTransient<IJobService, JobService>();
        return services;
    }
}
