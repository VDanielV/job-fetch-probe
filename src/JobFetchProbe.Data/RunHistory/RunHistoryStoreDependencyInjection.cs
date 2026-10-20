using JobFetchProbe.Services.Abstractions.RunHistory;
using Microsoft.Extensions.DependencyInjection;

namespace JobFetchProbe.Data.RunHistory;

public static class RunHistoryStoreDependencyInjection
{
    /// <summary>The read-only queries behind <c>IRunHistoryService</c>. Needs the database from <c>AddJobStore</c>.</summary>
    public static IServiceCollection AddRunHistoryStore(this IServiceCollection services)
    {
        services.AddSingleton<IRunHistoryRepository, SqliteRunHistoryRepository>();
        return services;
    }
}
