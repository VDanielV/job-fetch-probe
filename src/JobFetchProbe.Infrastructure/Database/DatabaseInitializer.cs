using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace JobFetchProbe.Infrastructure.Database;

public static class DatabaseInitializer
{
    /// <summary>
    /// Creates the database folder and file if missing, and applies any migrations not applied yet.
    /// Call once at start-up.
    /// </summary>
    /// <returns>A line describing the database, for the console.</returns>
    public static async Task<string> InitializeDatabaseAsync(this IServiceProvider services, CancellationToken ct = default)
    {
        var path = services.GetRequiredService<IOptions<DatabaseSettings>>().Value.FullPath;
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        var contexts = services.GetRequiredService<IDbContextFactory<JobsDbContext>>();
        await using var db = await contexts.CreateDbContextAsync(ct);
        var pending = (await db.Database.GetPendingMigrationsAsync(ct)).Count();
        await db.Database.MigrateAsync(ct);
        return pending > 0
            ? $"Database: {path} (schema updated, {pending} migration(s) applied)"
            : $"Database: {path}";
    }
}
