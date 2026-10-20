namespace JobFetchProbe.Models.Applications;

/// <summary>The owner applied to this stored job.</summary>
/// <param name="JobId">Required. The app's ID of the job, e.g. 57.</param>
public sealed record ApplyRequest(
    long JobId);
