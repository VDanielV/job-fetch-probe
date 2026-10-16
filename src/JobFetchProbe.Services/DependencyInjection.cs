using JobFetchProbe.Services.Details;
using JobFetchProbe.Services.Inbox;
using JobFetchProbe.Services.Parsing;
using JobFetchProbe.Services.Search;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace JobFetchProbe.Services;

public static class DependencyInjection
{
    /// <summary>Registers both steps: step 1 (inbox: saved search pages) and step 2 (job descriptions).</summary>
    public static IServiceCollection AddServices(this IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton<IJobPageParser, JobPageParser>();
        services.AddSingleton<ISearchPageParser, SearchPageParser>();

        // Transient, because these depend (directly or not) on the typed HttpClient, which must not be held by a
        // singleton: that would pin one handler forever and defeat the factory's handler rotation.
        services.AddTransient<IJobService, JobService>();
        services.AddTransient<ISearchService, SearchService>();
        services.AddTransient<IInboxService, InboxService>();
        services.AddTransient<IDetailsService, DetailsService>();
        return services;
    }
}
