using System.Net;
using JobFetchProbe.Application.Abstractions;
using JobFetchProbe.Infrastructure.Extraction;
using JobFetchProbe.Infrastructure.Http;
using JobFetchProbe.Infrastructure.Reporting;
using Microsoft.Extensions.DependencyInjection;

namespace JobFetchProbe.Infrastructure;

public static class DependencyInjection
{
    /// <summary>
    /// Registers the real implementations of Application's interfaces.
    /// One typed HttpClient, no retry or resilience handler, no cookies, no proxy configuration.
    /// </summary>
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, InfrastructureOptions options)
    {
        options.Validate();
        services.AddSingleton(options);

        services
            .AddHttpClient<IPageFetcher, HttpPageFetcher>(client =>
                client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds))
            .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
            {
                AllowAutoRedirect = false, // redirects are walked by hand so every hop is recorded
                UseCookies = false,
                AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Brotli,
            });

        services.AddSingleton<IJobExtractor, FixtureJobExtractor>();
        services.AddSingleton<IReportWriter, JsonReportWriter>();
        return services;
    }
}
