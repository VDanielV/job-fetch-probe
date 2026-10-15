using JobFetchProbe.Services.Abstractions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace JobFetchProbe.Infrastructure.Details;

public static class DetailsDependencyInjection
{
    /// <summary>Registers listing and details storage. Requests go through the job step's typed HttpClient.</summary>
    public static IServiceCollection AddDetailsInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<DetailsSettings>()
            .Bind(configuration.GetSection(DetailsSettings.SectionName))
            .Validate(s => !string.IsNullOrWhiteSpace(s.OutputFolder), "Details:OutputFolder is required")
            .Validate(
                s => s.DelaySeconds is >= DetailsSettings.MinDelaySeconds and <= DetailsSettings.MaxDelaySeconds,
                $"Details:DelaySeconds must be {DetailsSettings.MinDelaySeconds}-{DetailsSettings.MaxDelaySeconds}")
            .Validate(s => s.CircuitBreaker.FailureThreshold is >= 1 and <= 20, "Details:CircuitBreaker:FailureThreshold must be 1-20")
            .Validate(s => s.CircuitBreaker.CooldownSeconds is >= 1 and <= 14400, "Details:CircuitBreaker:CooldownSeconds must be 1-14400")
            .Validate(s => s.CircuitBreaker.MaxCooldownSeconds is >= 1 and <= 86400, "Details:CircuitBreaker:MaxCooldownSeconds must be 1-86400")
            .Validate(s => s.CircuitBreaker.StopAfterOpenings is >= 1 and <= 10, "Details:CircuitBreaker:StopAfterOpenings must be 1-10");

        services.AddSingleton<IListingStore, JsonListingStore>();
        services.AddSingleton<IDetailsRepository, JsonDetailsRepository>();
        return services;
    }
}
