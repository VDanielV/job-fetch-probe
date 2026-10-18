using JobFetchProbe.Data.Journal.Repositories;
using JobFetchProbe.Data.Repositories;
using JobFetchProbe.Services.Abstractions;
using JobFetchProbe.Services.Abstractions.Journal;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace JobFetchProbe.Data;

public static class DependencyInjection
{
    /// <summary>
    /// For the API since it starts acquisitions: the database, opened
    /// read-write (and created when missing, see <see cref="DatabaseInitializer.InitializeDatabaseAsync"/>), the
    /// repositories the flow writes through, the read-only view of the jobs, and the journal.
    /// </summary>
    public static IServiceCollection AddJobStore(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDatabase(configuration, readOnly: false);
        services.AddSingleton<IBatchRepository, SqliteBatchRepository>();
        services.AddSingleton<IJobRepository, SqliteJobRepository>();
        services.AddSingleton<IJobCatalog, SqliteJobCatalog>();
        services.AddSingleton<IJournalRepository, SqliteJournalRepository>();
        return services;
    }

    /// <summary>
    /// Read-only access: the database settings, a view of the jobs, and the journal to read. The connection is opened
    /// read-only, so it can never change the database, nor create it when it does not exist yet. For an app that only
    /// reads (the API used it until it started acquisitions).
    /// </summary>
    public static IServiceCollection AddJobCatalog(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDatabase(configuration, readOnly: true);
        services.AddSingleton<IJobCatalog, SqliteJobCatalog>();
        services.AddSingleton<IJournalRepository, SqliteJournalRepository>();
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
