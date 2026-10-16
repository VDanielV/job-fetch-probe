using System.Net;
using JobFetchProbe.Infrastructure.Database;
using JobFetchProbe.Infrastructure.Database.Repositories;
using JobFetchProbe.Infrastructure.Details;
using JobFetchProbe.Infrastructure.Files;
using JobFetchProbe.Infrastructure.Http;
using JobFetchProbe.Infrastructure.Inbox;
using JobFetchProbe.Services.Abstractions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace JobFetchProbe.Infrastructure;

public static class DependencyInjection
{
    /// <summary>For the console app: settings, the HTTP client, the input folder and the database.</summary>
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.TryAddSingleton(TimeProvider.System);

        services.AddOptions<InboxSettings>()
            .Bind(configuration.GetSection(InboxSettings.SectionName))
            .Validate(s => !string.IsNullOrWhiteSpace(s.Folder), "Inbox:Folder is required")
            .Validate(s => !string.IsNullOrWhiteSpace(s.ArchiveFolder), "Inbox:ArchiveFolder is required")
            .Validate(
                s => string.IsNullOrWhiteSpace(s.Folder) || string.IsNullOrWhiteSpace(s.ArchiveFolder) ||
                     !string.Equals(Path.GetFullPath(s.Folder).TrimEnd('\\', '/'), Path.GetFullPath(s.ArchiveFolder).TrimEnd('\\', '/'), StringComparison.OrdinalIgnoreCase),
                "Inbox:ArchiveFolder must be different from Inbox:Folder")
            .Validate(s => s.Patterns is { Length: > 0 }, "Inbox:Patterns needs at least one pattern");

        services.AddOptions<DetailsSettings>()
            .Bind(configuration.GetSection(DetailsSettings.SectionName))
            .Validate(
                s => s.DelaySeconds is >= DetailsSettings.MinDelaySeconds and <= DetailsSettings.MaxDelaySeconds,
                $"Details:DelaySeconds must be {DetailsSettings.MinDelaySeconds}-{DetailsSettings.MaxDelaySeconds}")
            .Validate(s => s.CircuitBreaker.FailureThreshold is >= 1 and <= 20, "Details:CircuitBreaker:FailureThreshold must be 1-20")
            .Validate(s => s.CircuitBreaker.CooldownSeconds is >= 1 and <= 14400, "Details:CircuitBreaker:CooldownSeconds must be 1-14400")
            .Validate(s => s.CircuitBreaker.MaxCooldownSeconds is >= 1 and <= 86400, "Details:CircuitBreaker:MaxCooldownSeconds must be 1-86400")
            .Validate(s => s.CircuitBreaker.StopAfterOpenings is >= 1 and <= 10, "Details:CircuitBreaker:StopAfterOpenings must be 1-10");

        services.AddOptions<HttpClientSettings>()
            .Bind(configuration.GetSection(HttpClientSettings.SectionName))
            .Validate(s => s.TimeoutSeconds is > 0 and <= 120, "HttpClient:TimeoutSeconds must be 1-120")
            .Validate(s => !string.IsNullOrWhiteSpace(s.UserAgent), "HttpClient:UserAgent is required");

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
        services.AddSingleton<IInboxFolder, InboxFolder>();

        services.AddDatabase(configuration);
        services.AddSingleton<IBatchRepository, SqliteBatchRepository>();
        services.AddSingleton<IJobRepository, SqliteJobRepository>();
        return services;
    }

    /// <summary>Only what the web app needs: the database settings and a read-only view of the jobs.</summary>
    public static IServiceCollection AddJobCatalog(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDatabase(configuration);
        services.AddSingleton<IJobCatalog, SqliteJobCatalog>();
        return services;
    }

    private static IServiceCollection AddDatabase(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<DatabaseSettings>()
            .Bind(configuration.GetSection(DatabaseSettings.SectionName))
            .Validate(s => !string.IsNullOrWhiteSpace(s.Path), "Database:Path is required");

        // A factory, not a scoped context: the repositories are singletons and open a short-lived context per call,
        // so no context (or connection) outlives the operation that needs it.
        services.AddDbContextFactory<JobsDbContext>((sp, options) =>
            options.UseSqlite(new SqliteConnectionStringBuilder
            {
                DataSource = sp.GetRequiredService<IOptions<DatabaseSettings>>().Value.FullPath,
            }.ToString()));
        return services;
    }
}
