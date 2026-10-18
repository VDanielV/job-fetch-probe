namespace JobFetchProbe.Services.Fit;

/// <summary>The candidate profile, the decision policy, and the scoring rubric. Implemented where those files live.</summary>
public interface IFitDocuments
{
    FitPack Load();
}
