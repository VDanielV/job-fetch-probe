using JobFetchProbe.Data.Archive.Repositories;
using JobFetchProbe.Services.Abstractions.Archive;
using Microsoft.Extensions.DependencyInjection;

namespace JobFetchProbe.Data.Archive;

public static class ArchiveStoreDependencyInjection
{
    /// <summary>The ArchivedJobs table behind <c>IJobArchiver</c>. Needs the database from <c>AddJobStore</c> (read-write).</summary>
    public static IServiceCollection AddJobArchiveStore(this IServiceCollection services)
    {
        services.AddSingleton<IJobArchiveRepository, SqliteJobArchiveRepository>();
        return services;
    }
}
