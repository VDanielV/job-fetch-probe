using System.Net;
using JobFetchProbe.Infrastructure.Files;
using JobFetchProbe.Infrastructure.Http;
using JobFetchProbe.Infrastructure.FetchJobListing;
using JobFetchProbe.Infrastructure.Ollama;
using JobFetchProbe.Services.Abstractions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace JobFetchProbe.Infrastructure;

public static class DependencyInjection
{
    /// <summary>For the API, which runs acquisitions: the HTTP client, saved pages and the input folder, with their settings. The database is added by JobFetchProbe.Data; step 2's rules by <c>AddServices</c>.</summary>
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.TryAddSingleton(TimeProvider.System);

        services.AddOptions<FetchJobListingSettings>()
            .Bind(configuration.GetSection(FetchJobListingSettings.SectionName))
            .Validate(s => !string.IsNullOrWhiteSpace(s.Folder), "FetchJobListing:Folder is required")
            .Validate(s => !string.IsNullOrWhiteSpace(s.ArchiveFolder), "FetchJobListing:ArchiveFolder is required")
            .Validate(
                s => string.IsNullOrWhiteSpace(s.Folder) || string.IsNullOrWhiteSpace(s.ArchiveFolder) ||
                     !string.Equals(Path.GetFullPath(s.Folder).TrimEnd('\\', '/'), Path.GetFullPath(s.ArchiveFolder).TrimEnd('\\', '/'), StringComparison.OrdinalIgnoreCase),
                "FetchJobListing:ArchiveFolder must be different from FetchJobListing:Folder")
            .Validate(s => s.Patterns is { Length: > 0 }, "FetchJobListing:Patterns needs at least one pattern")
            .ValidateOnStart();

        services.AddOptions<HttpClientSettings>()
            .Bind(configuration.GetSection(HttpClientSettings.SectionName))
            .Validate(s => s.TimeoutSeconds is > 0 and <= 120, "HttpClient:TimeoutSeconds must be 1-120")
            .Validate(s => !string.IsNullOrWhiteSpace(s.UserAgent), "HttpClient:UserAgent is required")
            .ValidateOnStart();

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
        services.AddSingleton<IInputFolder, InputFolder>();

        return services;
    }

    /// <summary>
    /// The local chat model (<see cref="ILocalChatModel"/>), an Ollama server. Needs <c>AddChat</c> as well, which
    /// registers the service that calls this client. Not part of <see cref="AddInfrastructure"/>: an acquisition does
    /// not talk to Ollama.
    /// </summary>
    public static IServiceCollection AddOllama(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<OllamaSettings>()
            .Bind(configuration.GetSection(OllamaSettings.SectionName))
            .Validate(s => Uri.TryCreate(s.BaseUrl, UriKind.Absolute, out _), "Ollama:BaseUrl must be an absolute URL")
            .Validate(s => !string.IsNullOrWhiteSpace(s.Model), "Ollama:Model is required")
            .Validate(s => s.TimeoutSeconds is >= 1 and <= 300, "Ollama:TimeoutSeconds must be 1-300")
            .ValidateOnStart();

        services.AddHttpClient<ILocalChatModel, OllamaChatClient>((sp, client) =>
            {
                var s = sp.GetRequiredService<IOptions<OllamaSettings>>().Value;
                client.BaseAddress = new Uri(s.BaseUrl.TrimEnd('/') + "/");
                client.Timeout = TimeSpan.FromSeconds(s.TimeoutSeconds);
                client.DefaultRequestHeaders.Accept.ParseAdd("application/json");
            })
            .SetHandlerLifetime(TimeSpan.FromMinutes(5));

        return services;
    }
}
