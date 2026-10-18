using JobFetchProbe.Data.Journal.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace JobFetchProbe.Data.Journal.Configurations;

/// <summary>What happened in a step 1 run; read in Id order ("entries after N").</summary>
internal sealed class JobListingJournalEntryRowConfiguration : IEntityTypeConfiguration<JobListingJournalEntryRow>
{
    public void Configure(EntityTypeBuilder<JobListingJournalEntryRow> builder)
    {
        builder.ToTable("JobListingJournalEntries");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.File).IsRequired();
        builder.Property(e => e.Outcome).IsRequired().HasMaxLength(16);
        builder.HasIndex(e => new { e.RunId, e.Id });
    }
}
