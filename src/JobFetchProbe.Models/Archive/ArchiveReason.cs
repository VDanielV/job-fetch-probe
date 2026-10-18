namespace JobFetchProbe.Models.Archive;

/// <summary>Why a job was archived. Stored by name, so new reasons can be added without a migration.</summary>
public enum ArchiveReason
{
    /// <summary>Requires relocation.</summary>
    Relocation,

    /// <summary>Not applicable tech stack.</summary>
    TechStack,

    /// <summary>Salary range too low.</summary>
    Salary,

    /// <summary>None of the above; the note says why (required).</summary>
    Other,
}
