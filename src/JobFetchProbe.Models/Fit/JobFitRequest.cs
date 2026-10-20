namespace JobFetchProbe.Models.Fit;

/// <summary>Score this stored job. The id is the one already in the database.</summary>
/// <param name="JobId">Required. The app's ID of the job, e.g. 57.</param>
public sealed record JobFitRequest(long JobId);
