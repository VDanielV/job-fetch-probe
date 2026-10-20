using System.Text.Json;
using JobFetchProbe.Models.Catalog;

namespace JobFetchProbe.Services.Fit;

/// <summary>One message for the local model: the Fit files, then the stored job. No history.</summary>
public static class JobFitPrompt
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// Ollama structured-output schema. The first five properties are the API result. The two requirement lists force
    /// the model to name the gap before deciding. The matched-skill list is not asked for: the service never stores it.
    /// </summary>
    public const string ResponseSchema = """
        {
          "type": "object",
          "properties": {
            "decision": {
              "type": "string",
              "enum": ["Apply", "Maybe", "Skip"]
            },
            "fit": {
              "type": "integer",
              "minimum": 0,
              "maximum": 100
            },
            "reason": {
              "type": "string",
              "minLength": 1
            },
            "hardSkip": {
              "type": "boolean"
            },
            "missingInformation": {
              "type": "array",
              "maxItems": 8,
              "items": { "type": "string", "maxLength": 120 }
            },
            "mandatoryRequirements": {
              "type": "array",
              "maxItems": 8,
              "items": { "type": "string", "maxLength": 120 }
            },
            "unmetMandatoryRequirements": {
              "type": "array",
              "maxItems": 8,
              "items": { "type": "string", "maxLength": 120 }
            }
          },
          "required": [
            "decision",
            "fit",
            "reason",
            "hardSkip",
            "missingInformation",
            "mandatoryRequirements",
            "unmetMandatoryRequirements"
          ],
          "additionalProperties": false
        }
        """;

    public static string Build(FitPack pack, StoredJob job)
    {
        var posting = JsonSerializer.Serialize(new
        {
            JobId = job.SourceJobId,
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
            2. A named product or language counts as production experience only when the profile lists it under production skills. Microsoft Business Central and the AL language are not C#, .NET, or ASP.NET. Go, Node.js, and C++ are exposure only. Never invent years of experience in an exposure-only skill.
            3. Fill mandatoryRequirements and unmetMandatoryRequirements before you score. Keep each list to at most 8 short lines. A skill listed in the profile is met. Never put C#, .NET, ASP.NET, Azure, SQL, Docker, RabbitMQ, tests, or CI/CD in unmetMandatoryRequirements.
            4. Score with the rubric. fit is an integer from 0 to 100. Sum the six parts. More experience than the posting asks for is not a gap and does not lower the score.
            5. Apply the decision policy after the sum. A hard skip forces Skip. Still report the fit number. Salary does not change the decision.
            6. If one mandatory line requires Java, Python, PHP, Node.js, React, or Go, and that same line does not accept C# or .NET as an alternative, hardSkip is true and decision is Skip. "C# and/or Go" is not a hard skip. "Python, .NET and/or TypeScript" is not a hard skip.

            reason is one short sentence. When something mandatory is unmet, name that gap. Do not write that the candidate lacks backend, API, Azure, or .NET experience. Do not mention leadership when the posting does not ask for it.
            hardSkip is true or false.
            missingInformation is an array of strings. Use an empty array when nothing important is missing. An unmet mandatory skill is a gap, not missing information.
            decision is exactly one of Apply, Maybe, or Skip.

            Your response is constrained by a JSON Schema. Return every schema field and no other fields.
            Use this order: decision, fit, reason, hardSkip, missingInformation, mandatoryRequirements, unmetMandatoryRequirements.
            The requirement keys are arrays of short strings. Do not repeat the candidate's skill list.

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
