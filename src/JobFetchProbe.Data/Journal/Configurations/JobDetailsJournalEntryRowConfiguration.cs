using JobFetchProbe.Data.Journal.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace JobFetchProbe.Data.Journal.Configurations;

/// <summary>What happened in a step 2 run; read in Id order ("entries after N").</summary>
internal sealed class JobDetailsJournalEntryRowConfiguration : IEntityTypeConfiguration<JobDetailsJournalEntryRow>
{
    public void Configure(EntityTypeBuilder<JobDetailsJournalEntryRow> builder)
    {
        builder.ToTable("JobDetailsJournalEntries");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Kind).IsRequired().HasMaxLength(16);
        builder.Property(e => e.Status).HasMaxLength(16);
        builder.HasIndex(e => new { e.RunId, e.Id });
    }
}
