namespace JobFetchProbe.Models.Applications;

/// <summary>The two numbers next to Applications in the menu: open applications, and how many of them are flagged.</summary>
public sealed record ApplicationsSummary(int Open, int Flagged);
