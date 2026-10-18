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

    /// <summary>
    /// Asks the acquisition started here to stop: during step 1 between two pages (nothing is stored, the pages stay
    /// in the input folder; step 2 does not start), during step 2 after the current job, and at once during a pause or
    /// a cooldown. The run then ends Stopped. An acquisition started by the console app cannot be stopped from here.
    /// </summary>
    Task<AcquisitionStop> TryStopAsync(CancellationToken ct = default);
}
