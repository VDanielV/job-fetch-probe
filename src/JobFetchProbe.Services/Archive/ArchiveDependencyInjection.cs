using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace JobFetchProbe.Services.Archive;

public static class ArchiveDependencyInjection
{
    /// <summary>
    /// Archiving jobs (<see cref="IJobArchiver"/>). Needs an <c>IJobArchiveRepository</c> and an <c>IJobCatalog</c> from the
    /// data layer (<c>AddJobArchiveStore</c>, <c>AddJobStore</c>).
    /// </summary>
    public static IServiceCollection AddJobArchive(this IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton<IJobArchiver, JobArchiver>();
        return services;
    }
}
