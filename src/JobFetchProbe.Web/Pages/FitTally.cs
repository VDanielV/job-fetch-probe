namespace JobFetchProbe.Web.Pages;

/// <summary>How many jobs step 2 scored in a run, how many it could not score, and whether scoring was switched off.</summary>
public sealed record FitTally(int Scored, int NotScored, bool Off);
