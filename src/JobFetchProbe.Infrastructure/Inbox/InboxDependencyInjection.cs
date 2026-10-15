using JobFetchProbe.Services.Abstractions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace JobFetchProbe.Infrastructure.Inbox;

public static class InboxDependencyInjection
{
    /// <summary>Registers the inbox folder and batch storage. The pages are read with the search step's reader.</summary>
    public static IServiceCollection AddInboxInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<InboxSettings>()
            .Bind(configuration.GetSection(InboxSettings.SectionName))
            .Validate(s => !string.IsNullOrWhiteSpace(s.Folder), "Inbox:Folder is required")
            .Validate(s => !string.IsNullOrWhiteSpace(s.ArchiveFolder), "Inbox:ArchiveFolder is required")
            .Validate(s => !string.IsNullOrWhiteSpace(s.OutputFolder), "Inbox:OutputFolder is required")
            .Validate(
                s => string.IsNullOrWhiteSpace(s.Folder) || string.IsNullOrWhiteSpace(s.ArchiveFolder) ||
                     !string.Equals(Path.GetFullPath(s.Folder).TrimEnd('\\', '/'), Path.GetFullPath(s.ArchiveFolder).TrimEnd('\\', '/'), StringComparison.OrdinalIgnoreCase),
                "Inbox:ArchiveFolder must be different from Inbox:Folder")
            .Validate(s => s.Patterns is { Length: > 0 }, "Inbox:Patterns needs at least one pattern");

        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton<IInboxFolder, InboxFolder>();
        services.AddSingleton<IBatchRepository, JsonBatchRepository>();
        return services;
    }
}
