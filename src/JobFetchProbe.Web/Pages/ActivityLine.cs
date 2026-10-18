namespace JobFetchProbe.Web.Pages;

/// <summary>One line of an acquisition's recent activity, from either step's journal entries.</summary>
/// <param name="Tag">Short label shown as a coloured tag (Read, Refused, Page read…).</param>
/// <param name="TagClass">The tag's CSS class (the job status colours).</param>
/// <param name="Step">1 for step 1 (pages), 2 for step 2 (jobs).</param>
/// <param name="Subject">The job id or the page's file name.</param>
public sealed record ActivityLine(int Step, long Order, DateTimeOffset At, string Tag, string TagClass, string? Subject, string? Text);
