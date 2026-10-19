using JobFetchProbe.Data.Entities;
using JobFetchProbe.Data.Fit.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace JobFetchProbe.Data.Fit.Configurations;

/// <summary>Every assessment of a job, read per job in date order; they go if the job is ever deleted.</summary>
internal sealed class JobFitAssessmentRowConfiguration : IEntityTypeConfiguration<JobFitAssessmentRow>
{
    public void Configure(EntityTypeBuilder<JobFitAssessmentRow> builder)
    {
        builder.ToTable("JobFitAssessments");
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Decision).IsRequired().HasMaxLength(16);
        builder.Property(a => a.Reason).IsRequired();
        builder.Property(a => a.MissingInformation).IsRequired();
        builder.Property(a => a.Model).HasMaxLength(64);
        builder.Property(a => a.ProfileVersion).IsRequired().HasMaxLength(16);
        builder.HasIndex(a => new { a.JobId, a.AssessedAt });
        builder.HasOne<JobRow>().WithMany().HasForeignKey(a => a.JobId).OnDelete(DeleteBehavior.Cascade);
    }
}
