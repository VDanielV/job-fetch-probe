namespace JobFetchProbe.Models.Applications;

/// <summary>Where an application stands. Applied, Interview and Offer are open; the others close it. Stored by name.</summary>
public enum ApplicationStage
{
    Applied,
    Interview,
    Offer,
    Rejected,
    Withdrawn,
    NoAnswer,
}
