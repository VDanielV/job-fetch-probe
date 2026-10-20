namespace JobFetchProbe.Services.Abstractions.RunHistory;

/// <summary>A saved page of a listing.</summary>
/// <param name="Unique">Job links first found on this page in its run (not on an earlier page); they follow each other in the listing.</param>
public sealed record ListingPageRecord(string File, int Unique);
