namespace JobFetchProbe.Extract;

public sealed record JobPosting(
    string Title,
    string Company,
    string Location,
    string Description,
    string SourcePath);
