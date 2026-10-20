using JobFetchProbe.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace JobFetchProbe.Data.Configurations;

/// <summary>One row per job, keyed by the app's own ID; a site's job is stored once (unique Source + SourceJobId).</summary>
internal sealed class JobRowConfiguration : IEntityTypeConfiguration<JobRow>
{
    public void Configure(EntityTypeBuilder<JobRow> builder)
    {
        builder.ToTable("Jobs");
        builder.HasKey(j => j.Id);
        builder.Property(j => j.Id).ValueGeneratedOnAdd();
        builder.Property(j => j.Source).IsRequired().HasMaxLength(32);
        builder.Property(j => j.SourceJobId).IsRequired().HasMaxLength(128);
        builder.HasIndex(j => new { j.Source, j.SourceJobId }).IsUnique();
        builder.Property(j => j.Url).IsRequired();
        builder.Property(j => j.Status).IsRequired().HasMaxLength(16);
        builder.HasIndex(j => j.Status);
        builder.HasIndex(j => j.Company);
        builder.HasMany(j => j.Details).WithOne().HasForeignKey(d => d.JobId).OnDelete(DeleteBehavior.Cascade);
    }
}
