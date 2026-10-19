using JobFetchProbe.Data.Applications.Repositories;
using JobFetchProbe.Services.Abstractions.Applications;
using Microsoft.Extensions.DependencyInjection;

namespace JobFetchProbe.Data.Applications;

public static class ApplicationStoreDependencyInjection
{
    /// <summary>The Applications and ApplicationNotes tables behind <c>IApplicationTracker</c>. Needs the database from <c>AddJobStore</c> (read-write).</summary>
    public static IServiceCollection AddApplicationStore(this IServiceCollection services)
    {
        services.AddSingleton<IApplicationRepository, SqliteApplicationRepository>();
        return services;
    }
}
