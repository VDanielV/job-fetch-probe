namespace JobFetchProbe.Models.Catalog;

/// <summary>One row of the jobs table in the web app.</summary>
public sealed record JobSummary(string JobId, string? Title, string? Company, string? Location, string? Posted, string Status);
