using System.ComponentModel.DataAnnotations;

namespace JobFetchProbe.Models.Applications;

/// <summary>The owner applied to this stored job.</summary>
/// <param name="JobId">Required. The job ID, e.g. 4476446382.</param>
public sealed record ApplyRequest(
    [Required(ErrorMessage = "Choose the job you applied to.")]
    string JobId);
