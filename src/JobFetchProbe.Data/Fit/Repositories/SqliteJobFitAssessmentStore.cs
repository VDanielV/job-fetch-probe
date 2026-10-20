using JobFetchProbe.Models.Fit;
using JobFetchProbe.Services.Abstractions.Fit;
using Microsoft.EntityFrameworkCore;

namespace JobFetchProbe.Data.Fit.Repositories;

/// <summary>Keeps every fit assessment in the JobFitAssessments table. Never changes Jobs.</summary>
public sealed class SqliteJobFitAssessmentStore(IDbContextFactory<JobsDbContext> contexts) : IJobFitAssessmentStore
{
    public async Task AddAsync(long jobId, JobFitAssessment assessment, CancellationToken ct = default)
    {
        await using var db = await contexts.CreateDbContextAsync(ct);
        db.JobFitAssessments.Add(assessment.ToRow(jobId));
        await db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<JobFitAssessment>> GetAsync(long jobId, CancellationToken ct = default)
    {
        await using var db = await contexts.CreateDbContextAsync(ct);
        var rows = await db.JobFitAssessments.AsNoTracking().Where(a => a.JobId == jobId).ToListAsync(ct);
        return rows
            .OrderByDescending(a => a.AssessedAt)
            .ThenByDescending(a => a.Id)
            .Select(a => a.ToModel())
            .ToList();
    }
}
