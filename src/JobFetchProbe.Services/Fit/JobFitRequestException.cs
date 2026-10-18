namespace JobFetchProbe.Services.Fit;

/// <summary>A fit request that cannot be scored (no job id, or the job has no description). The model was not called.</summary>
public sealed class JobFitRequestException(string field, string message) : Exception(message)
{
    /// <summary>The request field at fault: jobId or description.</summary>
    public string Field { get; } = field;
}
