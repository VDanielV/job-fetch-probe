using JobFetchProbe.Models;
using Microsoft.EntityFrameworkCore;

namespace JobFetchProbe.Infrastructure.Database;

/// <summary>Copies between the Models records and the job rows.</summary>
internal static class JobRowMapping
{
    /// <summary>Stores what a job page showed. Overwrites the previous read, as the JSON files do.</summary>
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
        row.Details.AddRange(job.Details.Select((d, i) => new JobDetailRow { JobId = row.JobId, Position = i, Label = d.Label, Value = d.Value }));
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

    public static JobPosting ToPosting(this JobRow row) => new()
    {
        JobId = row.JobId,
        SourceUrl = new Uri(row.Url),
        Title = row.Title,
        Company = row.Company,
        Location = row.Location,
        Posted = row.Posted,
        EmploymentType = row.EmploymentType,
        Details = row.Details.OrderBy(d => d.Position).Select(d => new JobDetail(d.Label, d.Value)).ToList(),
        Description = row.Description,
        ParsedFrom = row.ParsedFrom ?? string.Empty,
        RetrievedAt = row.RetrievedAt ?? default,
    };

    public static JobListing ToListing(string jobId, string url, string? title, string? company, string? location, string? listedAt) => new()
    {
        JobId = jobId,
        Url = new Uri(url),
        Title = title,
        Company = company,
        Location = location,
        ListedAt = listedAt,
    };

    /// <summary>
    /// Makes sure every card's job has a row (new rows start as Pending) and fills unread ones from the card.
    /// Returns the rows by JobId, tracked by <paramref name="db"/>.
    /// </summary>
    public static async Task<Dictionary<string, JobRow>> UpsertCardsAsync(
        this JobsDbContext db, IReadOnlyList<JobListing> cards, DateTimeOffset now, CancellationToken ct)
    {
        var ids = cards.Select(c => c.JobId).Distinct(StringComparer.Ordinal).ToList();
        var rows = await db.Jobs.Where(j => ids.Contains(j.JobId)).ToDictionaryAsync(j => j.JobId, StringComparer.Ordinal, ct);
        foreach (var card in cards)
        {
            if (!rows.TryGetValue(card.JobId, out var row))
            {
                row = new JobRow { JobId = card.JobId, FirstSeenAt = now, Status = JobStatus.Pending };
                rows[card.JobId] = row;
                db.Jobs.Add(row);
            }
            row.ApplyListing(card);
        }
        return rows;
    }
}
