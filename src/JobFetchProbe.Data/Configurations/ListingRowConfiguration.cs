using JobFetchProbe.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace JobFetchProbe.Data.Configurations;

/// <summary>One inbox run; its pages and cards are deleted with it.</summary>
internal sealed class ListingRowConfiguration : IEntityTypeConfiguration<ListingRow>
{
    public void Configure(EntityTypeBuilder<ListingRow> builder)
    {
        builder.ToTable("Listings");
        builder.HasKey(l => l.BatchId);
        builder.HasMany(l => l.Sources).WithOne().HasForeignKey(s => s.BatchId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(l => l.Jobs).WithOne().HasForeignKey(j => j.BatchId).OnDelete(DeleteBehavior.Cascade);
    }
}
