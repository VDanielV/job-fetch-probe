using JobFetchProbe.Data.Applications.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace JobFetchProbe.Data.Applications.Configurations;

/// <summary>The timeline lines, read per application in date order.</summary>
internal sealed class ApplicationNoteRowConfiguration : IEntityTypeConfiguration<ApplicationNoteRow>
{
    public void Configure(EntityTypeBuilder<ApplicationNoteRow> builder)
    {
        builder.ToTable("ApplicationNotes");
        builder.HasKey(n => n.Id);
        builder.Property(n => n.Kind).IsRequired().HasMaxLength(16);
        builder.Property(n => n.Text).IsRequired().HasMaxLength(2000);
        builder.HasIndex(n => new { n.JobId, n.At });
    }
}
