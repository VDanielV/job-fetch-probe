using JobFetchProbe.Data.Journal.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace JobFetchProbe.Data.Journal.Configurations;

/// <summary>One step 1 run in the journal; its entries are deleted with it.</summary>
internal sealed class JobListingJournalRunRowConfiguration : IEntityTypeConfiguration<JobListingJournalRunRow>
{
    public void Configure(EntityTypeBuilder<JobListingJournalRunRow> builder)
    {
        builder.ToTable("JobListingJournalRuns");
        builder.HasKey(r => r.Id);
        builder.Property(r => r.StartedBy).IsRequired().HasMaxLength(16);
        builder.Property(r => r.State).IsRequired().HasMaxLength(16);
        builder.Property(r => r.ErrorType).HasMaxLength(256);
        builder.HasIndex(r => r.State);
        builder.HasMany(r => r.Entries).WithOne().HasForeignKey(e => e.RunId).OnDelete(DeleteBehavior.Cascade);
    }
}
