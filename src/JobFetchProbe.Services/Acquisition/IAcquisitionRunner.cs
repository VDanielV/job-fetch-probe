using JobFetchProbe.Models.Acquisition;
using JobFetchProbe.Models.Journal;

namespace JobFetchProbe.Services.Acquisition;

/// <summary>
/// Starts an acquisition (<see cref="IAcquisitionService"/>) in the background and answers at once, for a caller that
/// cannot wait for it, such as an HTTP request. One acquisition at a time; its progress goes to the log and the journal.
/// </summary>
public interface IAcquisitionRunner
{
    /// <summary>
    /// Starts an acquisition, unless one is already running in this process or in another one (the console app),
    /// according to the journal.
    /// </summary>
    /// <param name="startedBy">Who asked, as shown in the journal.</param>
    Task<AcquisitionStart> TryStartAsync(JournalStarter startedBy, CancellationToken ct = default);
}
