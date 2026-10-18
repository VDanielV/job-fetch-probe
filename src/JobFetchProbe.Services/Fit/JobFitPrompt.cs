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

            Senior backend and API individual-contributor roles are target roles. Never penalise them for not asking for leadership. Do not say the candidate lacks leadership unless the profile says so.

            Do this in order:
            1. Read the job title and the mandatory requirements. One responsibility that mentions an API does not make the job a backend role. The primary stack is the title plus what the posting requires, not a side task.
            2. A named product or language counts as production experience only when the profile lists it under production skills. Microsoft Business Central and the AL language are not C#, .NET, or ASP.NET. Go, Node.js, and C++ are exposure only.
            3. Fill mandatoryRequirements, matchedRequirements, and unmetMandatoryRequirements before you score.
            4. Score with the rubric. fit is an integer from 0 to 100. Sum the six parts.
            5. Apply the decision policy after the sum. A hard skip forces Skip. Still report the fit number. Salary does not change the decision.

            reason is one short sentence. When something mandatory is unmet, name the largest gap. Do not mention leadership when the posting does not ask for it.
            hardSkip is true or false.
            missingInformation is an array of strings. Use an empty array when nothing important is missing. An unmet mandatory skill is a gap, not missing information.
            decision is exactly one of Apply, Maybe, or Skip.

            Return only a JSON object with these keys, in this order: mandatoryRequirements, matchedRequirements, unmetMandatoryRequirements, decision, fit, reason, hardSkip, missingInformation.
            The three requirement keys are arrays of short strings.

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
