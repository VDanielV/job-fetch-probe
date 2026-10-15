using JobFetchProbe.Services.Abstractions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace JobFetchProbe.Infrastructure.Search;

public static class SearchDependencyInjection
{
    /// <summary>
    /// Registers the search step's storage and settings. Reuses the job step's IPageFetcher (typed HttpClient)
    /// and ISavedPageReader, so AddInfrastructure must also be called.
    /// </summary>
    public static IServiceCollection AddSearchInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<SearchSettings>()
            .Bind(configuration.GetSection(SearchSettings.SectionName))
            .Validate(s => !string.IsNullOrWhiteSpace(s.OutputFolder), "Search:OutputFolder is required")
            .Validate(
                s => string.IsNullOrWhiteSpace(s.DefaultUrl) ||
                     (Uri.TryCreate(s.DefaultUrl, UriKind.Absolute, out var u) && (u.Scheme == Uri.UriSchemeHttps || u.Scheme == Uri.UriSchemeHttp)),
                "Search:DefaultUrl must be empty or an absolute http(s) link");

        services.AddSingleton<ISearchRepository, JsonSearchRepository>();
        return services;
    }
}
