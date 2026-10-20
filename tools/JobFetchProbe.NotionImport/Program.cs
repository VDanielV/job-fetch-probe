using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using JobFetchProbe.Data;
using JobFetchProbe.Data.Applications.Entities;
using JobFetchProbe.Data.Entities;
using JobFetchProbe.Data.Fit.Entities;
using JobFetchProbe.Models.Applications;
using JobFetchProbe.Models.Fit;
using JobFetchProbe.Models.Jobs;
using Microsoft.EntityFrameworkCore;

const string BatchId = "notion-migration";
const string NotionPage = "https://app.notion.com/p/pro4all/Applications-market-signals-3d71dd648b538100921ac101afca1033";

var options = ImportOptions.Parse(args);
var document = await JsonSerializer.DeserializeAsync<NotionExport>(
    File.OpenRead(options.InputPath), NotionJson.Options) ?? throw new InvalidOperationException("The input did not contain a Notion export.");
var rows = document.Results.Where(r => !string.IsNullOrWhiteSpace(r.Id) && !string.IsNullOrWhiteSpace(r.Role)).ToList();

var dbOptions = new DbContextOptionsBuilder<JobsDbContext>().UseSqlite($"Data Source={options.DatabasePath}").Options;
await using var db = new JobsDbContext(dbOptions);

if (!options.Apply)
{
    // The imported IDs are the jobs' SourceJobId (decision 7b of docs/spec-job-identity.md).
    var existing = await db.Jobs.AsNoTracking().Select(j => j.SourceJobId).ToHashSetAsync(StringComparer.Ordinal);
    var archived = await db.ArchivedJobs.AsNoTracking()
        .Join(db.Jobs.AsNoTracking(), a => a.JobId, j => j.Id, (a, j) => j.SourceJobId)
        .ToHashSetAsync(StringComparer.Ordinal);
    var plan = rows.Select(r => ImportRow.Create(r)).ToList();
    Console.WriteLine($"Dry run: {rows.Count} Notion applications. No database changes were made.");
    PrintCounts("Status", rows.GroupBy(r => r.Status ?? "Missing").Select(g => (g.Key, g.Count())));
    PrintCounts("Source", rows.GroupBy(r => r.Source ?? "Missing").Select(g => (g.Key, g.Count())));
    Console.WriteLine($"Existing jobs: {plan.Count(r => existing.Contains(r.JobId))}");
    Console.WriteLine($"New job shells: {plan.Count(r => !existing.Contains(r.JobId) && !archived.Contains(r.JobId))}");
    Console.WriteLine($"Archived conflicts: {plan.Count(r => archived.Contains(r.JobId))}");
    Console.WriteLine($"LinkedIn IDs: {plan.Count(r => r.UsesLinkedInId)}; Notion fallbacks: {plan.Count(r => r.UsesNotionFallback)}");
    Console.WriteLine($"Duplicate job ids: {plan.GroupBy(r => r.JobId, StringComparer.Ordinal).Count(g => g.Count() > 1)}");
    Console.WriteLine($"Listing cards to add: {plan.Count(r => !archived.Contains(r.JobId))}");
    return;
}

await db.Database.MigrateAsync();
await using var transaction = await db.Database.BeginTransactionAsync();
var now = DateTimeOffset.UtcNow;
var listing = await db.Listings.SingleOrDefaultAsync(l => l.BatchId == BatchId);
if (listing is null)
{
    listing = new ListingRow { BatchId = BatchId, CreatedAt = now };
    db.Listings.Add(listing);
}
var sources = db.Set<ListingSourceRow>();
var source = await sources.SingleOrDefaultAsync(s => s.BatchId == BatchId && s.Position == 0);
if (source is null)
{
    source = new ListingSourceRow { BatchId = BatchId, Position = 0, File = "notion-applications", SourceUrl = NotionPage, Query = "Notion migration" };
    sources.Add(source);
}

    var imported = 0;
    var skippedArchived = 0;
    var skippedDuplicate = 0;
    var seen = new HashSet<string>(StringComparer.Ordinal);
var addedJobs = 0;
var addedCards = 0;
var addedFits = 0;
foreach (var raw in rows)
{
    var row = ImportRow.Create(raw);
    var jobSource = SourceOf(row.JobId);
    var job = await db.Jobs.SingleOrDefaultAsync(j => j.Source == jobSource && j.SourceJobId == row.JobId);
    if (job is not null && await db.ArchivedJobs.AnyAsync(a => a.JobId == job.Id))
    {
        skippedArchived++;
        continue;
    }
    if (!seen.Add(row.JobId))
    {
        skippedDuplicate++;
        continue;
    }

    if (job is null)
    {
        job = new JobRow
        {
            Source = jobSource,
            SourceJobId = row.JobId,
            Url = row.Url,
            Title = raw.Role,
            Company = raw.Company,
            Location = raw.Location ?? raw.City,
            FirstSeenAt = row.AppliedAt,
            Status = JobStatus.External,
        };
        db.Jobs.Add(job);
        await db.SaveChangesAsync(); // gives the job its Id, inside the transaction
        addedJobs++;
    }

    if (!await db.ListingJobs.AnyAsync(j => j.BatchId == BatchId && j.JobId == job.Id))
    {
        db.ListingJobs.Add(new ListingJobRow
        {
            BatchId = BatchId,
            JobId = job.Id,
            Position = imported,
            Url = job.Url,
            Title = job.Title,
            Company = job.Company,
            Location = job.Location,
            ListedAt = row.AppliedAt.ToString("yyyy-MM-dd"),
        });
        addedCards++;
    }

    if (!await db.Applications.AnyAsync(a => a.JobId == job.Id))
    {
        var application = new ApplicationRow
        {
            JobId = job.Id,
            AppliedAt = row.AppliedAt,
            Stage = row.Stage.ToString(),
            StageChangedAt = row.Stage == ApplicationStage.Applied ? row.AppliedAt : row.StageChangedAt,
        };
        application.Notes.Add(new ApplicationNoteRow
        {
            JobId = job.Id,
            At = row.AppliedAt,
            Kind = ApplicationNoteKind.Applied.ToString(),
            Text = $"Imported from Notion {raw.Id}.",
        });
        if (row.Stage != ApplicationStage.Applied)
        {
            application.Notes.Add(new ApplicationNoteRow
            {
                JobId = job.Id,
                At = row.StageChangedAt,
                Kind = ApplicationNoteKind.Stage.ToString(),
                Text = $"Imported status: {raw.Status}.",
            });
        }
        foreach (var note in Notes(raw))
        {
            application.Notes.Add(new ApplicationNoteRow
            {
                JobId = job.Id,
                At = row.StageChangedAt,
                Kind = ApplicationNoteKind.Note.ToString(),
                Text = note,
            });
        }
        db.Applications.Add(application);
    }

    if (raw.Fit is not null && !await db.JobFitAssessments.AnyAsync(a => a.JobId == job.Id && a.Model == "score-card"))
    {
        db.JobFitAssessments.Add(new JobFitAssessmentRow
        {
            JobId = job.Id,
            AssessedAt = now,
            Fit = (int)Math.Round(raw.Fit.Value, MidpointRounding.AwayFromZero),
            Decision = Decision(raw.Fit.Value).ToString(),
            HardSkip = false,
            Reason = $"Score-card fit imported from Notion {raw.Id}.",
            MissingInformation = "[]",
            Model = "score-card",
            ProfileVersion = "notion",
        });
        addedFits++;
    }
    imported++;
}

source.Jobs = imported;
source.NewJobs = addedJobs;
await db.SaveChangesAsync();
await transaction.CommitAsync();
Console.WriteLine($"Imported {imported} applications; {addedJobs} jobs, {addedCards} listing cards, and {addedFits} score-card fits added. {skippedArchived} archived conflicts and {skippedDuplicate} duplicate jobs skipped.");

// A plain number is a LinkedIn posting; anything else is the import's own ID (Notion-A027, InfoJobs-A071…).
static string SourceOf(string id)
{
    if (id.Length > 0 && id.All(char.IsAsciiDigit))
    {
        return JobSources.LinkedIn;
    }
    return JobSources.Notion;
}

static IEnumerable<string> Notes(NotionApplication row)
{
    foreach (var (label, value) in new[]
             {
                 ("Notes", row.Notes), ("Interview topics asked", row.InterviewTopics), ("Gap to study", row.GapToStudy),
                 ("Contact", Contact(row)), ("Outcome reason", row.OutcomeReason), ("CV used", row.CvUsed), ("Source", row.Source),
             })
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            yield return Truncate($"{label}: {value.Trim()}", 2000);
        }
    }
}

static string? Contact(NotionApplication row) =>
    string.Join(" ", new[] { row.Contact, row.ContactEmail }.Where(value => !string.IsNullOrWhiteSpace(value))).Trim() is { Length: > 0 } value ? value : null;

static string Truncate(string value, int maximum) => value.Length <= maximum ? value : value[..(maximum - 1)] + "…";

static JobFitDecision Decision(double fit) => fit >= 60 ? JobFitDecision.Apply : fit >= 40 ? JobFitDecision.Maybe : JobFitDecision.Skip;

static void PrintCounts(string title, IEnumerable<(string Key, int Count)> counts)
{
    Console.WriteLine(title + ":");
    foreach (var (key, count) in counts.OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase))
    {
        Console.WriteLine($"  {key}: {count}");
    }
}

file sealed record ImportOptions(string InputPath, string DatabasePath, bool Apply)
{
    public static ImportOptions Parse(string[] args)
    {
        var input = Value("--input");
        var database = Value("--database");
        if (input is null || database is null)
        {
            throw new ArgumentException("Usage: --input <notion-export.json> --database <jobs.db> [--apply]");
        }
        return new ImportOptions(Path.GetFullPath(input), Path.GetFullPath(database), args.Contains("--apply", StringComparer.OrdinalIgnoreCase));

        string? Value(string name)
        {
            var index = Array.FindIndex(args, arg => string.Equals(arg, name, StringComparison.OrdinalIgnoreCase));
            return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
        }
    }
}

file sealed record NotionExport([property: JsonPropertyName("results")] List<NotionApplication> Results);

file sealed record NotionApplication
{
    [JsonPropertyName("url")] public string Url { get; init; } = string.Empty;
    [JsonPropertyName("createdTime")] public string? CreatedAt { get; init; }
    [JsonPropertyName("userDefined:ID")] public string Id { get; init; } = string.Empty;
    [JsonPropertyName("Role")] public string Role { get; init; } = string.Empty;
    [JsonPropertyName("Company")] public string? Company { get; init; }
    [JsonPropertyName("Status")] public string? Status { get; init; }
    [JsonPropertyName("Fit")] public double? Fit { get; init; }
    [JsonPropertyName("Source")] public string? Source { get; init; }
    [JsonPropertyName("Source ref")] public string? SourceRef { get; init; }
    [JsonPropertyName("Location")] public string? Location { get; init; }
    [JsonPropertyName("City")] public string? City { get; init; }
    [JsonPropertyName("Notes")] public string? Notes { get; init; }
    [JsonPropertyName("Interview topics asked")] public string? InterviewTopics { get; init; }
    [JsonPropertyName("Gap to study")] public string? GapToStudy { get; init; }
    [JsonPropertyName("Contact")] public string? Contact { get; init; }
    [JsonPropertyName("Contact email")] public string? ContactEmail { get; init; }
    [JsonPropertyName("Outcome reason")] public string? OutcomeReason { get; init; }
    [JsonPropertyName("CV used")] public string? CvUsed { get; init; }
    [JsonPropertyName("date:Applied:start")] public DateOnly? Applied { get; init; }
    [JsonPropertyName("date:Outcome date:start")] public DateOnly? OutcomeDate { get; init; }
}

file sealed record ImportRow(string JobId, string Url, DateTimeOffset AppliedAt, DateTimeOffset StageChangedAt, ApplicationStage Stage, bool UsesLinkedInId, bool UsesNotionFallback)
{
    public static ImportRow Create(NotionApplication row)
    {
        var linkedIn = string.Equals(row.Source, "LinkedIn", StringComparison.OrdinalIgnoreCase);
        var linkedInId = linkedIn ? LinkedInId(row.SourceRef) : null;
        var numericLinkedInId = linkedInId is not null;
        var fallback = linkedIn && !numericLinkedInId;
        var jobId = numericLinkedInId
            ? linkedInId!
            : fallback
                ? $"Notion-{row.Id}"
                : $"{SourcePrefix(row.Source)}-{row.Id}";
        var applied = row.Applied is { } date
            ? new DateTimeOffset(date.Year, date.Month, date.Day, 0, 0, 0, TimeSpan.Zero)
            : ParseCreated(row.CreatedAt);
        var stageChanged = row.OutcomeDate is { } outcome
            ? new DateTimeOffset(outcome.Year, outcome.Month, outcome.Day, 0, 0, 0, TimeSpan.Zero)
            : applied;
        var url = numericLinkedInId ? $"https://www.linkedin.com/jobs/view/{linkedInId}/" : row.Url;
        return new ImportRow(jobId, url, applied, stageChanged, StageFor(row.Status), numericLinkedInId, fallback);
    }

    private static DateTimeOffset ParseCreated(string? value)
    {
        if (DateTimeOffset.TryParse(value, out var parsed))
        {
            return parsed;
        }
        return DateTimeOffset.UtcNow;
    }

    private static string? LinkedInId(string? sourceRef)
    {
        var match = Regex.Match(sourceRef?.Trim() ?? string.Empty, @"^(\d{6,})(?:\s*/.*)?$");
        return match.Success ? match.Groups[1].Value : null;
    }

    private static string SourcePrefix(string? source) => source?.ToLowerInvariant() switch
    {
        "infojobs" => "InfoJobs",
        "tecnoempleo" => "Tecnoempleo",
        "manfred" => "Manfred",
        "corporate site" => "Corporate",
        "recruiter" => "Recruiter",
        "referral" => "Referral",
        _ => "Notion",
    };

    private static ApplicationStage StageFor(string? status) => status switch
    {
        "Applied" => ApplicationStage.Applied,
        "Screening" or "Tech interview" or "Final round" => ApplicationStage.Interview,
        "Offer" => ApplicationStage.Offer,
        "Rejected" => ApplicationStage.Rejected,
        "Withdrawn" => ApplicationStage.Withdrawn,
        "No answer" => ApplicationStage.NoAnswer,
        _ => throw new InvalidOperationException($"Unknown Notion status '{status}'."),
    };
}

file static class NotionJson
{
    public static readonly JsonSerializerOptions Options = new() { PropertyNameCaseInsensitive = false };
}
