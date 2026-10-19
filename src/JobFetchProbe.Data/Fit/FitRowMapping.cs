using System.Text.Json;
using JobFetchProbe.Data.Fit.Entities;
using JobFetchProbe.Models.Fit;

namespace JobFetchProbe.Data.Fit;

/// <summary>Copies assessment rows to the models; the decision is stored by name, the missing facts as JSON.</summary>
internal static class FitRowMapping
{
    public static JobFitAssessment ToModel(this JobFitAssessmentRow row)
    {
        var missing = JsonSerializer.Deserialize<List<string>>(row.MissingInformation) ?? [];
        return new JobFitAssessment(
            row.Id, row.AssessedAt, row.Fit, Enum.Parse<JobFitDecision>(row.Decision), row.HardSkip, row.Reason, missing, row.Model,
            row.ProfileVersion);
    }

    public static JobFitAssessmentRow ToRow(this JobFitAssessment assessment, string jobId)
    {
        return new JobFitAssessmentRow
        {
            JobId = jobId,
            AssessedAt = assessment.AssessedAt,
            Fit = assessment.Fit,
            Decision = assessment.Decision.ToString(),
            HardSkip = assessment.HardSkip,
            Reason = assessment.Reason,
            MissingInformation = JsonSerializer.Serialize(assessment.MissingInformation),
            Model = assessment.Model,
            ProfileVersion = assessment.ProfileVersion,
        };
    }
}
