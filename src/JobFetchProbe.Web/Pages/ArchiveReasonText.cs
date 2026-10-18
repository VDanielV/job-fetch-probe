using JobFetchProbe.Models.Archive;

namespace JobFetchProbe.Web.Pages;

/// <summary>How the archive reasons read on the page.</summary>
public static class ArchiveReasonText
{
    /// <summary>The reasons with their own button in the details pane, in key order (1, 2, 3).</summary>
    public static IReadOnlyList<ArchiveReason> Quick { get; } = [ArchiveReason.Relocation, ArchiveReason.TechStack, ArchiveReason.Salary];

    /// <summary>Every reason, in the order the bar's list shows them.</summary>
    public static IReadOnlyList<ArchiveReason> All { get; } = [ArchiveReason.Relocation, ArchiveReason.TechStack, ArchiveReason.Salary, ArchiveReason.Other];

    public static string Label(ArchiveReason reason)
    {
        switch (reason)
        {
            case ArchiveReason.Relocation:
                return "Requires relocation";
            case ArchiveReason.TechStack:
                return "Not applicable tech stack";
            case ArchiveReason.Salary:
                return "Salary range too low";
            default:
                return "Other";
        }
    }
}
