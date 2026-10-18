using JobFetchProbe.Data.Archive.Entities;
using JobFetchProbe.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace JobFetchProbe.Data.Archive.Configurations;

/// <summary>One row per archived job, keyed by the job; it goes if the job is ever deleted.</summary>
internal sealed class ArchivedJobRowConfiguration : IEntityTypeConfiguration<ArchivedJobRow>
{
    public void Configure(EntityTypeBuilder<ArchivedJobRow> builder)
    {
        builder.ToTable("ArchivedJobs");
        builder.HasKey(a => a.JobId);
        builder.Property(a => a.Reason).IsRequired().HasMaxLength(16);
        builder.Property(a => a.Note).HasMaxLength(500);
        builder.HasOne<JobRow>().WithOne().HasForeignKey<ArchivedJobRow>(a => a.JobId).OnDelete(DeleteBehavior.Cascade);
    }
}
