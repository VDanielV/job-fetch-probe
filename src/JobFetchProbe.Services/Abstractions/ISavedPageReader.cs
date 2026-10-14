namespace JobFetchProbe.Services.Abstractions;

/// <summary>Reads a page saved from the browser. Implemented in Infrastructure.</summary>
public interface ISavedPageReader
{
    Task<string> ReadAsync(string path, CancellationToken ct = default);
}
