using System.Net;
using JobFetchProbe.Infrastructure.Files;
using JobFetchProbe.Infrastructure.Http;
using JobFetchProbe.Infrastructure.Storage;
using JobFetchProbe.Services.Abstractions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace JobFetchProbe.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<HttpClientSettings>()
            .Bind(configuration.GetSection(HttpClientSettings.SectionName))
            .Validate(s => s.TimeoutSeconds is > 0 and <= 120, "HttpClient:TimeoutSeconds must be 1-120")
            .Validate(s => !string.IsNullOrWhiteSpace(s.UserAgent), "HttpClient:UserAgent is required");

        services.AddOptions<OutputSettings>()
            .Bind(configuration.GetSection(OutputSettings.SectionName))
            .Validate(s => !string.IsNullOrWhiteSpace(s.Folder), "Output:Folder is required");

        // Typed client through IHttpClientFactory. The factory owns and pools the message handlers,
        // so HttpClient instances are cheap, never need disposing, and do not leak sockets.
        var lifetime = TimeSpan.FromMinutes(
            configuration.GetSection(HttpClientSettings.SectionName).Get<HttpClientSettings>()?.HandlerLifetimeMinutes ?? 5);

        services.AddHttpClient<IPageFetcher, HttpPageFetcher>((sp, client) =>
            {
                var s = sp.GetRequiredService<IOptions<HttpClientSettings>>().Value;
                client.Timeout = TimeSpan.FromSeconds(s.TimeoutSeconds);
                client.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", s.UserAgent);
                client.DefaultRequestHeaders.Accept.ParseAdd("text/html");
            })
            .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
            {
                UseCookies = false,
                AllowAutoRedirect = true,
                MaxAutomaticRedirections = 5,
                AutomaticDecompression = DecompressionMethods.All,
            })
            .SetHandlerLifetime(lifetime);

        services.AddSingleton<ISavedPageReader, SavedPageReader>();
        services.AddSingleton<IJobRepository, JsonJobRepository>();
        return services;
    }
}
