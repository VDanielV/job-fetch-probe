using JobFetchProbe.Infrastructure.Database.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace JobFetchProbe.Infrastructure.Database.Configurations;

/// <summary>The labelled facts of a job page, deleted with their job.</summary>
internal sealed class JobDetailRowConfiguration : IEntityTypeConfiguration<JobDetailRow>
{
    public void Configure(EntityTypeBuilder<JobDetailRow> builder)
    {
        builder.ToTable("JobDetails");
        builder.HasIndex(d => new { d.JobId, d.Position });
    }
}
