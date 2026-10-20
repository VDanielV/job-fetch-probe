using JobFetchProbe.Models.Jobs;

namespace JobFetchProbe.Web.Pages;

/// <summary>How a job's source reads on the pages: "LinkedIn 4476446382", "Notion Notion-A027".</summary>
public static class JobSourceText
{
    public static string Label(string source)
    {
        if (source == JobSources.LinkedIn)
        {
            return "LinkedIn";
        }
        if (source == JobSources.Notion)
        {
            return "Notion";
        }
        return source;
    }

    /// <summary>The site and the site's own ID, for the top line of the pane.</summary>
    public static string Of(string source, string sourceJobId)
    {
        return Label(source) + " " + sourceJobId;
    }
}
