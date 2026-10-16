using JobFetchProbe.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace JobFetchProbe.Data.Configurations;

/// <summary>A job card in a listing. A job cannot be deleted while a listing still refers to it.</summary>
internal sealed class ListingJobRowConfiguration : IEntityTypeConfiguration<ListingJobRow>
{
    public void Configure(EntityTypeBuilder<ListingJobRow> builder)
    {
        builder.ToTable("ListingJobs");
        builder.HasKey(j => new { j.BatchId, j.JobId });
        builder.Property(j => j.Url).IsRequired();
        builder.HasIndex(j => j.JobId);
        builder.HasOne<JobRow>().WithMany().HasForeignKey(j => j.JobId).OnDelete(DeleteBehavior.Restrict);
    }
}
