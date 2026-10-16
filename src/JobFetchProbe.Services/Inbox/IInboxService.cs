using JobFetchProbe.Models.Inbox;

namespace JobFetchProbe.Services.Inbox;

/// <summary>Step 1: reads every saved search page in the input folder, stores their job links in the database, and archives the pages read.</summary>
public interface IInboxService
{
    /// <param name="progress">Told before and after each page is read.</param>
    Task<InboxRunResult> RunAsync(IProgress<InboxProgress>? progress = null, CancellationToken ct = default);
}
