using JobFetchProbe.Services.Acquisition;
using JobFetchProbe.Services.Catalog;
using JobFetchProbe.Services.Chat;
using JobFetchProbe.Services.Fit;
using JobFetchProbe.Services.FetchJobDetails;
using JobFetchProbe.Services.FetchJobListing;
using JobFetchProbe.Services.Journal;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace JobFetchProbe.Services;

public static class DependencyInjection
{
    /// <summary>
    /// Registers both steps: step 1, FetchJobListing (saved search pages → job listing), and step 2, FetchJobDetails
    /// (job details behind the circuit breaker), with step 2's rules from the "FetchJobDetails" section; and
    /// <see cref="IAcquisitionService"/>, which runs them in order. The acquisition records the journal, so
    /// <see cref="AddJournal"/> is needed too.
    /// </summary>
    public static IServiceCollection AddServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.TryAddSingleton(TimeProvider.System);

        services.AddOptions<FetchJobDetailsSettings>()
            .Bind(configuration.GetSection(FetchJobDetailsSettings.SectionName))
            .Validate(
                s => s.DelaySeconds is >= FetchJobDetailsSettings.MinDelaySeconds and <= FetchJobDetailsSettings.MaxDelaySeconds,
                $"FetchJobDetails:DelaySeconds must be {FetchJobDetailsSettings.MinDelaySeconds}-{FetchJobDetailsSettings.MaxDelaySeconds}")
            .Validate(s => s.CircuitBreaker.FailureThreshold is >= 1 and <= 20, "FetchJobDetails:CircuitBreaker:FailureThreshold must be 1-20")
            .Validate(s => s.CircuitBreaker.CooldownSeconds is >= 1 and <= 14400, "FetchJobDetails:CircuitBreaker:CooldownSeconds must be 1-14400")
            .Validate(s => s.CircuitBreaker.MaxCooldownSeconds is >= 1 and <= 86400, "FetchJobDetails:CircuitBreaker:MaxCooldownSeconds must be 1-86400")
            .Validate(s => s.CircuitBreaker.StopAfterOpenings is >= 1 and <= 10, "FetchJobDetails:CircuitBreaker:StopAfterOpenings must be 1-10")
            .ValidateOnStart(); // the API refuses to start with a bad value


        services.AddSingleton<IJobPageParser, JobPageParser>();
        services.AddSingleton<ISearchPageParser, SearchPageParser>();

        // Transient, because these depend (directly or not) on the typed HttpClient, which must not be held by a
        // singleton: that would pin one handler forever and defeat the factory's handler rotation.
        services.AddTransient<IJobService, JobService>();
        services.AddTransient<ISearchService, SearchService>();
        services.AddTransient<IJobListingFetcher, JobListingFetcher>();
        services.AddTransient<IJobDetailsFetcher, JobDetailsFetcher>();

        // Singleton: it holds no state and opens a scope per step for the transient fetchers above.
        services.AddSingleton<IAcquisitionService, AcquisitionService>();
        return services;
    }

    /// <summary>
    /// For the API: <see cref="IAcquisitionRunner"/>, which starts an acquisition in the background and is told when the
    /// application stops. Needs <see cref="AddServices"/> and <see cref="AddJournal"/>.
    /// </summary>
    public static IServiceCollection AddAcquisitionRunner(this IServiceCollection services)
    {
        services.AddSingleton<AcquisitionRunner>();
        services.AddSingleton<IAcquisitionRunner>(sp => sp.GetRequiredService<AcquisitionRunner>());
        services.AddHostedService(sp => sp.GetRequiredService<AcquisitionRunner>());
        return services;
    }

    /// <summary>
    /// For the API: reading the stored jobs (<see cref="IJobRetriever"/>). Needs an <c>IJobCatalog</c> from the data
    /// layer (<c>AddJobCatalog</c>). Separate from <see cref="AddServices"/>, so the API gets nothing that sends requests.
    /// </summary>
    public static IServiceCollection AddJobRetriever(this IServiceCollection services)
    {
        services.AddSingleton<IJobRetriever, JobRetriever>();
        return services;
    }

    /// <summary>
    /// One-turn chat (<see cref="IChatService"/>). Needs an <see cref="Abstractions.ILocalChatModel"/> from the
    /// infrastructure layer. Transient, because that model is a typed HTTP client and must not be held by a singleton.
    /// </summary>
    public static IServiceCollection AddChat(this IServiceCollection services)
    {
        services.AddTransient<IChatService, ChatService>();
        return services;
    }

    /// <summary>
    /// Scores one stored job (<see cref="IJobFitService"/>). Needs <see cref="AddJobRetriever"/>, an
    /// <see cref="IFitDocuments"/>, and an <see cref="Abstractions.ILocalChatModel"/>. Transient, for the same reason as
    /// <see cref="AddChat"/>.
    /// </summary>
    public static IServiceCollection AddJobFit(this IServiceCollection services)
    {
        services.AddTransient<IJobFitService, JobFitService>();
        return services;
    }

    /// <summary>
    /// The journal of the two steps (<see cref="IJournalService"/>): written by the process that fetches, read by anyone.
    /// Needs an <c>IJournalRepository</c> from the data layer.
    /// </summary>
    public static IServiceCollection AddJournal(this IServiceCollection services, JournalOptions? options = null)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton(options ?? JournalOptions.Default);
        services.AddSingleton<IJournalService, JournalService>();
        return services;
    }
}
