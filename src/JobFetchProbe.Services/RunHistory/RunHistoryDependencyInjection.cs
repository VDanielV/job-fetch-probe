using Microsoft.Extensions.DependencyInjection;

namespace JobFetchProbe.Services.RunHistory;

public static class RunHistoryDependencyInjection
{
    /// <summary>
    /// The history of past acquisitions (<see cref="IRunHistoryService"/>). Needs an <c>IRunHistoryRepository</c>, the
    /// journal's <c>IJournalRepository</c> and an <c>IJobCatalog</c> from the data layer (<c>AddRunHistoryStore</c>, <c>AddJobStore</c>).
    /// </summary>
    public static IServiceCollection AddRunHistory(this IServiceCollection services)
    {
        services.AddSingleton<IRunHistoryService, RunHistoryService>();
        return services;
    }
}
