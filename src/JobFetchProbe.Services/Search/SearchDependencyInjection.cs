using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace JobFetchProbe.Services.Search;

public static class SearchDependencyInjection
{
    /// <summary>Registers the search step. Kept separate from AddJobServices so the job step is untouched.</summary>
    public static IServiceCollection AddSearchServices(this IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton<ISearchPageParser, SearchPageParser>();

        // Transient for the same reason as IJobService: it depends on the factory-made typed HttpClient.
        services.AddTransient<ISearchService, SearchService>();
        return services;
    }
}
