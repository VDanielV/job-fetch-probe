using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace JobFetchProbe.Services.Inbox;

public static class InboxDependencyInjection
{
    /// <summary>Registers the inbox step. Needs AddSearchServices for ISearchService.</summary>
    public static IServiceCollection AddInboxServices(this IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.AddTransient<IInboxService, InboxService>();
        return services;
    }
}
