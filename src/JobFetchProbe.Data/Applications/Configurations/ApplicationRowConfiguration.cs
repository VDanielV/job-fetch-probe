using JobFetchProbe.Data.Applications.Entities;
using JobFetchProbe.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace JobFetchProbe.Data.Applications.Configurations;

/// <summary>One row per job applied to, keyed by the job; its timeline goes with it when it is deleted (undo).</summary>
internal sealed class ApplicationRowConfiguration : IEntityTypeConfiguration<ApplicationRow>
{
    public void Configure(EntityTypeBuilder<ApplicationRow> builder)
    {
        builder.ToTable("Applications");
        builder.HasKey(a => a.JobId);
        builder.Property(a => a.Stage).IsRequired().HasMaxLength(16);
        builder.HasIndex(a => a.Stage);
        builder.HasOne<JobRow>().WithOne().HasForeignKey<ApplicationRow>(a => a.JobId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(a => a.Notes).WithOne().HasForeignKey(n => n.JobId).OnDelete(DeleteBehavior.Cascade);
    }
}
