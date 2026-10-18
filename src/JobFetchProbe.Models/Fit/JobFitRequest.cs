using System.ComponentModel.DataAnnotations;

namespace JobFetchProbe.Models.Fit;

/// <summary>Score this stored job. The id is the one already in the database.</summary>
/// <param name="JobId">Required. The job id, e.g. 4476446382.</param>
public sealed record JobFitRequest(
    [Required(ErrorMessage = "Choose a job to score.")]
    string JobId);
