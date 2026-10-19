using JobFetchProbe.Models.Applications;

namespace JobFetchProbe.Web.ViewComponents;

/// <summary>What the Applications menu entry shows.</summary>
public sealed record ApplicationsMenuView(bool Active, ApplicationsSummary? Summary);
