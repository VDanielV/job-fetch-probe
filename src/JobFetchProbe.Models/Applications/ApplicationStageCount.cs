namespace JobFetchProbe.Models.Applications;

/// <summary>How many applications are at one stage, for the chips.</summary>
public sealed record ApplicationStageCount(ApplicationStage Stage, int Count);
