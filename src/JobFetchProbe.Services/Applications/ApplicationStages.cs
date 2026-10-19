using JobFetchProbe.Models.Applications;

namespace JobFetchProbe.Services.Applications;

/// <summary>Which stages close an application, and the words used for them in the timeline.</summary>
internal static class ApplicationStages
{
    public static bool IsClosed(ApplicationStage stage)
    {
        return stage == ApplicationStage.Rejected || stage == ApplicationStage.Withdrawn || stage == ApplicationStage.NoAnswer;
    }

    public static string Label(ApplicationStage stage)
    {
        if (stage == ApplicationStage.NoAnswer)
        {
            return "No answer";
        }
        return stage.ToString();
    }
}
