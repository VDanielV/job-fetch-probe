using JobFetchProbe.Infrastructure.Storage;
using JobFetchProbe.Services.Abstractions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace JobFetchProbe.Infrastructure.Database;

public static class DatabaseDependencyInjection
{
    /// <summary>
    /// Chooses where everything is stored (Storage:Provider). With Json nothing changes. With Sqlite the JSON
    /// repositories registered by the other Add…Infrastructure methods are replaced by the database ones, so call
    /// this after them.
    /// </summary>
    public static IServiceCollection AddStorage(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<StorageSettings>()
            .Bind(configuration.GetSection(StorageSettings.SectionName))
            .Validate(s => s.IsValidProvider, "Storage:Provider must be Json or Sqlite")
            .Validate(s => !s.UsesSqlite || !string.IsNullOrWhiteSpace(s.Sqlite.Path), "Storage:Sqlite:Path is required");

        var settings = new StorageSettings { Provider = configuration[$"{StorageSettings.SectionName}:Provider"] ?? StorageSettings.JsonProvider };
        if (!settings.UsesSqlite)
        {
            return services;
        }

        // A factory, not a scoped context: the repositories are singletons and open a short-lived context per call,
        // so no context (or connection) outlives the operation that needs it.
        services.AddDbContextFactory<JobsDbContext>((sp, options) =>
            options.UseSqlite(new SqliteConnectionStringBuilder
            {
                DataSource = sp.GetRequiredService<IOptions<StorageSettings>>().Value.Sqlite.FullPath,
            }.ToString()));

        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton<SqliteLocations>();
        services.Replace(ServiceDescriptor.Singleton<IJobRepository, SqliteJobRepository>());
        services.Replace(ServiceDescriptor.Singleton<ISearchRepository, SqliteSearchRepository>());
        services.Replace(ServiceDescriptor.Singleton<IBatchRepository, SqliteBatchRepository>());
        services.Replace(ServiceDescriptor.Singleton<IListingStore, SqliteListingStore>());
        services.Replace(ServiceDescriptor.Singleton<IDetailsRepository, SqliteDetailsRepository>());
        return services;
    }

    /// <summary>
    /// With Sqlite: creates the database folder and file if missing, and applies any migrations not applied yet.
    /// Call once at start-up. With Json: does nothing.
    /// </summary>
    /// <returns>A line describing the storage, for the console.</returns>
    public static async Task<string> InitializeStorageAsync(this IServiceProvider services, CancellationToken ct = default)
    {
        var settings = services.GetRequiredService<IOptions<StorageSettings>>().Value;
        if (!settings.UsesSqlite)
        {
            return "Storage: JSON files";
        }

        var path = settings.Sqlite.FullPath;
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        var contexts = services.GetRequiredService<IDbContextFactory<JobsDbContext>>();
        await using var db = await contexts.CreateDbContextAsync(ct);
        var pending = (await db.Database.GetPendingMigrationsAsync(ct)).Count();
        await db.Database.MigrateAsync(ct);
        return pending > 0
            ? $"Storage: SQLite {path} (schema updated, {pending} migration(s) applied)"
            : $"Storage: SQLite {path}";
    }
}
