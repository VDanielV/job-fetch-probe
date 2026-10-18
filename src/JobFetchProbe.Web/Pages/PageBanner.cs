namespace JobFetchProbe.Web.Pages;

/// <summary>A coloured message at the top of a pane.</summary>
/// <param name="Kind">CSS class: ok (green), busy (amber) or error (red).</param>
public sealed record PageBanner(string Kind, string Title, string Text)
{
    /// <summary>The sign shown before the title.</summary>
    public string Icon
    {
        get
        {
            if (Kind == "ok")
            {
                return "✓";
            }
            if (Kind == "busy")
            {
                return "!";
            }
            return "✕";
        }
    }
}
