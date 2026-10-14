namespace JobFetchProbe.Domain.Models;

/// <summary>Everything the classifier sees: status, final URL and visible text. Never the raw body.</summary>
public sealed record ClassifierInput(int Status, Uri? FinalUrl, string VisibleText, bool HasH1, bool TransportFailed);
