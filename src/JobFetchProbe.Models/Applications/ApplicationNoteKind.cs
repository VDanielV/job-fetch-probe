namespace JobFetchProbe.Models.Applications;

/// <summary>What a timeline line records. Only <see cref="Note"/> is written by the owner; the others are added by the app.</summary>
public enum ApplicationNoteKind
{
    Applied,
    Stage,
    Note,
}
