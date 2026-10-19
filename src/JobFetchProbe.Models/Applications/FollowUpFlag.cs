namespace JobFetchProbe.Models.Applications;

/// <summary>
/// The reminder on an application still at stage Applied: from day 7 Follow up (ask them for the status), from day 21
/// No answer (worth closing). Worked out from the date of applying; never stored, and it never closes anything.
/// </summary>
public enum FollowUpFlag
{
    None,
    FollowUp,
    NoAnswer,
}
