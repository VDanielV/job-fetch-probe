using JobFetchProbe.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace JobFetchProbe.Data;

/// <summary>
/// The SQLite database. Created and upgraded by the migrations in Database/Migrations when the app starts
/// (see <see cref="DatabaseInitializer.InitializeDatabaseAsync"/>).
/// </summary>
public sealed class JobsDbContext(DbContextOptions<JobsDbContext> options) : DbContext(options)
{
    public DbSet<JobRow> Jobs => Set<JobRow>();
    public DbSet<ListingRow> Listings => Set<ListingRow>();
    public DbSet<ListingJobRow> ListingJobs => Set<ListingJobRow>();

    /// <summary>Each table is configured in its own class in Database/Configurations.</summary>
    protected override void OnModelCreating(ModelBuilder model) =>
        model.ApplyConfigurationsFromAssembly(typeof(JobsDbContext).Assembly);
}
