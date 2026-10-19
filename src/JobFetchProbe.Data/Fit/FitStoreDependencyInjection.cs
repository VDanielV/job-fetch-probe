using JobFetchProbe.Data.Fit.Repositories;
using JobFetchProbe.Services.Abstractions.Fit;
using Microsoft.Extensions.DependencyInjection;

namespace JobFetchProbe.Data.Fit;

public static class FitStoreDependencyInjection
{
    /// <summary>The JobFitAssessments table behind <c>IJobFitService</c>. Needs the database from <c>AddJobStore</c> (read-write).</summary>
    public static IServiceCollection AddJobFitStore(this IServiceCollection services)
    {
        services.AddSingleton<IJobFitAssessmentStore, SqliteJobFitAssessmentStore>();
        return services;
    }
}
