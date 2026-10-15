using Microsoft.EntityFrameworkCore;

namespace JobFetchProbe.Infrastructure.Database;

/// <summary>
/// The SQLite database. Created and upgraded by the migrations in Database/Migrations when the app starts
/// (see <see cref="DatabaseDependencyInjection.InitializeStorageAsync"/>).
/// </summary>
public sealed class JobsDbContext(DbContextOptions<JobsDbContext> options) : DbContext(options)
{
    public DbSet<JobRow> Jobs => Set<JobRow>();
    public DbSet<ListingRow> Listings => Set<ListingRow>();
    public DbSet<ListingJobRow> ListingJobs => Set<ListingJobRow>();
    public DbSet<SearchRow> Searches => Set<SearchRow>();

    protected override void OnModelCreating(ModelBuilder model)
    {
        model.Entity<JobRow>(e =>
        {
            e.ToTable("Jobs");
            e.HasKey(j => j.JobId);
            e.Property(j => j.Url).IsRequired();
            e.Property(j => j.Status).IsRequired().HasMaxLength(16);
            e.HasIndex(j => j.Status);
            e.HasIndex(j => j.Company);
            e.HasMany(j => j.Details).WithOne().HasForeignKey(d => d.JobId).OnDelete(DeleteBehavior.Cascade);
        });

        model.Entity<JobDetailRow>(e =>
        {
            e.ToTable("JobDetails");
            e.HasIndex(d => new { d.JobId, d.Position });
        });

        model.Entity<ListingRow>(e =>
        {
            e.ToTable("Listings");
            e.HasKey(l => l.BatchId);
            e.HasMany(l => l.Sources).WithOne().HasForeignKey(s => s.BatchId).OnDelete(DeleteBehavior.Cascade);
            e.HasMany(l => l.Jobs).WithOne().HasForeignKey(j => j.BatchId).OnDelete(DeleteBehavior.Cascade);
        });

        model.Entity<ListingSourceRow>(e =>
        {
            e.ToTable("ListingSources");
            e.Property(s => s.File).IsRequired();
            e.Property(s => s.SourceUrl).IsRequired();
            e.HasIndex(s => new { s.BatchId, s.Position });
        });

        model.Entity<ListingJobRow>(e =>
        {
            e.ToTable("ListingJobs");
            e.HasKey(j => new { j.BatchId, j.JobId });
            e.Property(j => j.Url).IsRequired();
            e.HasIndex(j => j.JobId);
            e.HasOne<JobRow>().WithMany().HasForeignKey(j => j.JobId).OnDelete(DeleteBehavior.Restrict);
        });

        model.Entity<SearchRow>(e =>
        {
            e.ToTable("Searches");
            e.HasKey(s => s.SearchId);
            e.Property(s => s.SourceUrl).IsRequired();
            e.HasMany(s => s.Jobs).WithOne().HasForeignKey(j => j.SearchId).OnDelete(DeleteBehavior.Cascade);
        });

        model.Entity<SearchJobRow>(e =>
        {
            e.ToTable("SearchJobs");
            e.HasKey(j => new { j.SearchId, j.JobId });
            e.Property(j => j.Url).IsRequired();
            e.HasIndex(j => j.JobId);
            e.HasOne<JobRow>().WithMany().HasForeignKey(j => j.JobId).OnDelete(DeleteBehavior.Restrict);
        });
    }
}
