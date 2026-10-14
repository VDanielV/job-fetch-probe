using JobFetchProbe.Application;
using JobFetchProbe.Application.Results;
using JobFetchProbe.Application.Services;
using JobFetchProbe.Console.Cli;
using JobFetchProbe.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace JobFetchProbe.Console;

/// <summary>
/// Composition root. Arguments are validated before the host is built,
/// so an invalid command never creates an HTTP client.
/// </summary>
public static class ProbeApp
{
    /// <param name="configureServices">Test hook: runs after the real registrations, so later registrations replace them.</param>
    public static async Task<int> RunAsync(
        string[] args,
        TextWriter stdout,
        TextWriter stderr,
        Action<IServiceCollection>? configureServices = null,
        CancellationToken ct = default)
    {
        if (args is ["--help"] or ["-h"] or ["help"])
        {
            stdout.WriteLine(ProbeOptions.Usage);
            return ExitCodes.Ok;
        }

        ProbeOptions options;
        try
        {
            options = ProbeOptions.Parse(args);
        }
        catch (ArgumentException ex)
        {
            stderr.WriteLine($"error: {ex.Message}");
            stderr.WriteLine(ProbeOptions.Usage);
            return ExitCodes.InvalidArguments;
        }

        var builder = Host.CreateEmptyApplicationBuilder(new HostApplicationBuilderSettings());
        builder.Services
            .AddApplication()
            .AddInfrastructure(new InfrastructureOptions
            {
                OutDir = options.OutDir,
                TimeoutSeconds = options.TimeoutSeconds,
            });
        configureServices?.Invoke(builder.Services);

        using var host = builder.Build();
        var services = host.Services;

        try
        {
            ProbeResult result = options.Command switch
            {
                ProbeCommand.Policy => await services.GetRequiredService<IPolicyService>().RunAsync(ct),
                ProbeCommand.Extract => services.GetRequiredService<IExtractService>().Run(options.HtmlPath!),
                ProbeCommand.Fetch => await services.GetRequiredService<IFetchService>().RunAsync(options.Url!, ct),
                _ => throw new ArgumentException($"unknown command {options.Command}"),
            };

            foreach (var warning in result.Warnings)
            {
                stderr.WriteLine($"note: {warning}");
            }
            stdout.WriteLine(result.Summary);
            return ExitCodes.From(result.Status);
        }
        catch (Exception ex) when (ex is FormatException or ArgumentException)
        {
            stderr.WriteLine($"error: {ex.Message}");
            return ExitCodes.InvalidArguments;
        }
    }
}
