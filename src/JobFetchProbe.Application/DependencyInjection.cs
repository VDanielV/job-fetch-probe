using JobFetchProbe.Application.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace JobFetchProbe.Application;

public static class DependencyInjection
{
    /// <summary>Registers the use-case services. Infrastructure supplies the interfaces they depend on.</summary>
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.AddTransient<IPolicyService, PolicyService>();
        services.AddTransient<IExtractService, ExtractService>();
        services.AddTransient<IFetchService, FetchService>();
        return services;
    }
}
