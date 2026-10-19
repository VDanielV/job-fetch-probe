using JobFetchProbe.Models.Fit;
using Microsoft.EntityFrameworkCore;

namespace JobFetchProbe.Data.Fit;

/// <summary>
/// The latest assessment of each job, for the lists. Picked here, not in SQL: SQLite cannot order by DateTimeOffset.
/// </summary>
internal static class LatestFits
{
    /// <param name="jobIds">Only these jobs; null for every job.</param>
    public static async Task<Dictionary<string, FitScore>> ReadAsync(JobsDbContext db, IReadOnlyCollection<string>? jobIds, CancellationToken ct)
    {
        var query = db.JobFitAssessments.AsNoTracking();
        if (jobIds is not null)
        {
            query = query.Where(a => jobIds.Contains(a.JobId));
        }
        var rows = await query
            .Select(a => new { a.Id, a.JobId, a.AssessedAt, a.Fit, a.Decision, a.HardSkip })
            .ToListAsync(ct);

        var latest = new Dictionary<string, FitScore>(StringComparer.Ordinal);
        foreach (var group in rows.GroupBy(r => r.JobId, StringComparer.Ordinal))
        {
            var last = group.OrderByDescending(r => r.AssessedAt).ThenByDescending(r => r.Id).First();
            latest[group.Key] = new FitScore(last.Fit, Enum.Parse<JobFitDecision>(last.Decision), last.HardSkip, last.AssessedAt);
        }
        return latest;
    }
}
