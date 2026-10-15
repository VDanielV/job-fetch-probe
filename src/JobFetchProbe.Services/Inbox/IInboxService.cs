using JobFetchProbe.Models;

namespace JobFetchProbe.Services.Inbox;

/// <summary>Reads every saved search page in the inbox, stores all their jobs as one batch, and archives the pages read.</summary>
public interface IInboxService
{
    /// <param name="progress">Told before and after each page is read.</param>
    Task<InboxRunResult> RunAsync(IProgress<InboxProgress>? progress = null, CancellationToken ct = default);
}
