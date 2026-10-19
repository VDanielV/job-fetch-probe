using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace JobFetchProbe.Services.Applications;

public static class ApplicationsDependencyInjection
{
    /// <summary>
    /// The applications (<see cref="IApplicationTracker"/>). Needs an <c>IApplicationRepository</c> and an <c>IJobCatalog</c>
    /// from the data layer (<c>AddApplicationStore</c>, <c>AddJobStore</c>).
    /// </summary>
    public static IServiceCollection AddApplications(this IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton<IApplicationTracker, ApplicationTracker>();
        return services;
    }
}
