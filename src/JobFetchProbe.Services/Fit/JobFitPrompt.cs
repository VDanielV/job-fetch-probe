using System.Text.Json;
using JobFetchProbe.Models.Catalog;

namespace JobFetchProbe.Services.Fit;

/// <summary>One message for the local model: the Fit files, then the stored job. No history.</summary>
public static class JobFitPrompt
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public static string Build(FitPack pack, StoredJob job)
    {
        var posting = JsonSerializer.Serialize(new
        {
            job.JobId,
            job.Title,
            job.Company,
            job.Location,
            job.Posted,
            job.EmploymentType,
            job.Details,
            job.Description,
        }, Json);

        return $"""
            You assess one job for one candidate.

            Use only the candidate profile, the decision policy, and the scoring rubric below. Do not invent experience.

            Score with the rubric. fit is an integer from 0 to 100.
            decision is exactly one of Apply, Maybe, or Skip.
            A hard skip forces Skip. Still report the fit number.
            Salary does not change the decision. Low pay or missing pay is a note, not a skip and not a downgrade.
            reason is one short sentence.
            hardSkip is true or false.
            missingInformation is an array of strings. Use an empty array when nothing important is missing.

            Return only a JSON object with the keys decision, fit, reason, hardSkip, and missingInformation.

            Candidate profile:
            {pack.Profile}

            Decision policy:
            {pack.Policy}

            Scoring rubric:
            {pack.Rubric}

            Job:
            {posting}
            """;
    }
}
