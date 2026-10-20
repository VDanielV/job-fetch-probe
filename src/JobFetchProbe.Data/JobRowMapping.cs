using JobFetchProbe.Data.Entities;
using JobFetchProbe.Models.Jobs;
using JobFetchProbe.Models.Search;
using Microsoft.EntityFrameworkCore;

namespace JobFetchProbe.Data;

/// <summary>Copies between the Models records and the job rows.</summary>
internal static class JobRowMapping
{
    /// <summary>Stores what a job page showed and marks the job as read. Overwrites any earlier read.</summary>
    public static void ApplyPosting(this JobRow row, JobPosting job)
    {
        row.Url = job.SourceUrl.AbsoluteUri;
        row.Title = job.Title;
        row.Company = job.Company;
        row.Location = job.Location;
        row.Posted = job.Posted;
        row.EmploymentType = job.EmploymentType;
        row.Description = job.Description;
        row.ParsedFrom = job.ParsedFrom;
        row.RetrievedAt = job.RetrievedAt;
        row.Status = JobStatus.Read;
        row.LastError = null;
        row.LastAttemptAt = job.RetrievedAt;

        row.Details.Clear();
        row.Details.AddRange(job.Details.Select((d, i) => new JobDetailRow { JobId = row.Id, Position = i, Label = d.Label, Value = d.Value }));
    }

    /// <summary>Fills a job not read yet from a search card. A job already read keeps its page data.</summary>
    public static void ApplyListing(this JobRow row, JobListing card)
    {
        if (row.Status == JobStatus.Read)
        {
            return;
        }
        row.Url = card.Url.AbsoluteUri;
        row.Title = card.Title ?? row.Title;
        row.Company = card.Company ?? row.Company;
        row.Location = card.Location ?? row.Location;
    }

    /// <summary>The job as step 2 needs it: its site and the site's ID, its page, and what its card showed.</summary>
    public static JobListing ToCard(this JobRow row) => new()
    {
        Source = row.Source,
        SourceJobId = row.SourceJobId,
        Url = new Uri(row.Url),
        Title = row.Title,
        Company = row.Company,
        Location = row.Location,
    };

    /// <summary>
    /// Makes sure every card's job has a row (new rows start as Pending) and fills unread ones from the card.
    /// Returns the rows by <see cref="KeyOf(JobListing)"/>, tracked by <paramref name="db"/>; new rows get their Id when
    /// the context is saved.
    /// </summary>
    public static async Task<Dictionary<string, JobRow>> UpsertCardsAsync(
        this JobsDbContext db, IReadOnlyList<JobListing> cards, DateTimeOffset now, CancellationToken ct)
    {
        var rows = new Dictionary<string, JobRow>(StringComparer.Ordinal);
        foreach (var group in cards.GroupBy(c => c.Source, StringComparer.Ordinal))
        {
            var source = group.Key;
            var ids = group.Select(c => c.SourceJobId).Distinct(StringComparer.Ordinal).ToList();
            var known = await db.Jobs.Where(j => j.Source == source && ids.Contains(j.SourceJobId)).ToListAsync(ct);
            foreach (var row in known)
            {
                rows[KeyOf(row.Source, row.SourceJobId)] = row;
            }
        }
        foreach (var card in cards)
        {
            var key = KeyOf(card);
            if (!rows.TryGetValue(key, out var row))
            {
                row = new JobRow { Source = card.Source, SourceJobId = card.SourceJobId, FirstSeenAt = now, Status = JobStatus.Pending };
                rows[key] = row;
                db.Jobs.Add(row);
            }
            row.ApplyListing(card);
        }
        return rows;
    }

    /// <summary>A dictionary key for a site's job: source and site ID.</summary>
    public static string KeyOf(JobListing card)
    {
        return KeyOf(card.Source, card.SourceJobId);
    }

    public static string KeyOf(string source, string sourceJobId)
    {
        return source + "\n" + sourceJobId;
    }
}
