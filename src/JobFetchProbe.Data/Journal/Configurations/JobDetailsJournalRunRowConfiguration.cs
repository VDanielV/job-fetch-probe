using JobFetchProbe.Data.Journal.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace JobFetchProbe.Data.Journal.Configurations;

/// <summary>One step 2 run in the journal; its entries are deleted with it.</summary>
internal sealed class JobDetailsJournalRunRowConfiguration : IEntityTypeConfiguration<JobDetailsJournalRunRow>
{
    public void Configure(EntityTypeBuilder<JobDetailsJournalRunRow> builder)
    {
        builder.ToTable("JobDetailsJournalRuns");
        builder.HasKey(r => r.Id);
        builder.Property(r => r.StartedBy).IsRequired().HasMaxLength(16);
        builder.Property(r => r.State).IsRequired().HasMaxLength(16);
        builder.Property(r => r.CircuitState).IsRequired().HasMaxLength(16);
        builder.HasIndex(r => r.State);
        builder.HasMany(r => r.Entries).WithOne().HasForeignKey(e => e.RunId).OnDelete(DeleteBehavior.Cascade);
    }
}
