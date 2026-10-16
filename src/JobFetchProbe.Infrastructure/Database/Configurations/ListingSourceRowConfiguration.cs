using JobFetchProbe.Infrastructure.Database.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace JobFetchProbe.Infrastructure.Database.Configurations;

/// <summary>The saved pages a listing was read from.</summary>
internal sealed class ListingSourceRowConfiguration : IEntityTypeConfiguration<ListingSourceRow>
{
    public void Configure(EntityTypeBuilder<ListingSourceRow> builder)
    {
        builder.ToTable("ListingSources");
        builder.Property(s => s.File).IsRequired();
        builder.Property(s => s.SourceUrl).IsRequired();
        builder.HasIndex(s => new { s.BatchId, s.Position });
    }
}
