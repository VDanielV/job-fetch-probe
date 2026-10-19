namespace JobFetchProbe.Models.Applications;

/// <summary>Moves an application to another stage; any stage may follow any other (a closed one can be reopened).</summary>
public sealed record StageRequest(ApplicationStage Stage);
