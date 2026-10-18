using JobFetchProbe.Data.Archive.Entities;
using JobFetchProbe.Models.Archive;

namespace JobFetchProbe.Data.Archive;

/// <summary>Copies an archive row to the model; the reason is stored by name.</summary>
internal static class ArchiveRowMapping
{
    public static JobArchiveInfo ToModel(this ArchivedJobRow row)
    {
        return new JobArchiveInfo(row.ArchivedAt, Enum.Parse<ArchiveReason>(row.Reason), row.Note);
    }
}
