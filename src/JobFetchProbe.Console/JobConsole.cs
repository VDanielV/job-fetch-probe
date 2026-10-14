using JobFetchProbe.Models;
using JobFetchProbe.Services;
using Microsoft.Extensions.DependencyInjection;

namespace JobFetchProbe.Console;

/// <summary>
/// Reads input, calls the job service, prints the result. Each request gets its own DI scope, so the
/// service and its factory-made HttpClient are short-lived; the pooled handlers underneath are reused.
/// </summary>
public sealed class JobConsole(IServiceScopeFactory scopes)
{
    public async Task<int> RunOnceAsync(string input) =>
        await HandleAsync(input) ? 0 : 1;

    public async Task<int> RunInteractiveAsync()
    {
        System.Console.WriteLine("Paste a job link, or the path of a page you saved from your browser. Type 'exit' to quit.");
        while (true)
        {
            System.Console.Write("\nJob link or file: ");
            var line = System.Console.ReadLine()?.Trim().Trim('"');
            if (line is null || line.Equals("exit", StringComparison.OrdinalIgnoreCase))
            {
                return 0;
            }
            if (line.Length > 0)
            {
                await HandleAsync(line);
            }
        }
    }

    private async Task<bool> HandleAsync(string input)
    {
        using var scope = scopes.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IJobService>();

        JobResult result;
        if (Uri.TryCreate(input, UriKind.Absolute, out var url) && (url.Scheme == Uri.UriSchemeHttp || url.Scheme == Uri.UriSchemeHttps))
        {
            result = await service.FromUrlAsync(url);
        }
        else
        {
            result = await service.FromFileAsync(input);
        }

        if (result.Status != JobResultStatus.Saved || result.Job is null)
        {
            System.Console.WriteLine(result.Message);
            return false;
        }

        Print(result.Job);
        System.Console.WriteLine();
        System.Console.WriteLine($"Saved to {result.SavedTo}");
        return true;
    }

    private static void Print(JobPosting job)
    {
        Line("Job ID", job.JobId);
        Line("Title", job.Title);
        Line("Company", job.Company);
        Line("Location", job.Location);
        Line("Posted", job.Posted);
        Line("Type", job.EmploymentType);
        foreach (var d in job.Details)
        {
            Line(d.Label, d.Value);
        }
        System.Console.WriteLine($"(read from {job.ParsedFrom})");

        System.Console.WriteLine();
        System.Console.WriteLine("Description:");
        System.Console.WriteLine(string.IsNullOrWhiteSpace(job.Description) ? "(none found)" : job.Description);
    }

    private static void Line(string label, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            System.Console.WriteLine($"{label + ":",-10} {value}");
        }
    }
}
