using JobFetchProbe.Data.Repositories;
using JobFetchProbe.Services.Abstractions;
using JobFetchProbe.Services.Catalog;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace JobFetchProbe.Data;

public static class DependencyInjection
{
    /// <summary>
    /// For the console app: the database, opened read-write (and created when missing, see
    /// <see cref="DatabaseInitializer.InitializeDatabaseAsync"/>), and the repositories the flow writes through.
    /// </summary>
    public static IServiceCollection AddJobStore(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDatabase(configuration, readOnly: false);
        services.AddSingleton<IBatchRepository, SqliteBatchRepository>();
        services.AddSingleton<IJobRepository, SqliteJobRepository>();
        return services;
    }

    /// <summary>
    /// Only what the web app and the API need: the database settings and a read-only view of the jobs. The connection
    /// is opened read-only, so they can never change the database, nor create it when it does not exist yet.
    /// </summary>
    public static IServiceCollection AddJobCatalog(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDatabase(configuration, readOnly: true);
        services.AddSingleton<IJobCatalog, SqliteJobCatalog>();
        return services;
    }

    private static IServiceCollection AddDatabase(this IServiceCollection services, IConfiguration configuration, bool readOnly)
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
                Mode = readOnly ? SqliteOpenMode.ReadOnly : SqliteOpenMode.ReadWriteCreate,
            }.ToString()));
        return services;
    }
}
