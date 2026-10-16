using JobFetchProbe.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace JobFetchProbe.Data.Configurations;

/// <summary>One row per job.</summary>
internal sealed class JobRowConfiguration : IEntityTypeConfiguration<JobRow>
{
    public void Configure(EntityTypeBuilder<JobRow> builder)
    {
        builder.ToTable("Jobs");
        builder.HasKey(j => j.JobId);
        builder.Property(j => j.Url).IsRequired();
        builder.Property(j => j.Status).IsRequired().HasMaxLength(16);
        builder.HasIndex(j => j.Status);
        builder.HasIndex(j => j.Company);
        builder.HasMany(j => j.Details).WithOne().HasForeignKey(d => d.JobId).OnDelete(DeleteBehavior.Cascade);
    }
}
