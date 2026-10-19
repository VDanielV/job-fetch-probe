namespace JobFetchProbe.Services.Applications;

/// <summary>A request that breaks the rules (no job ID, an empty or too long note). Nothing was changed.</summary>
public sealed class ApplicationRequestException(string field, string message) : Exception(message)
{
    /// <summary>The request field at fault: jobId or text.</summary>
    public string Field { get; } = field;
}
