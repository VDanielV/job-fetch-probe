using JobFetchProbe.Infrastructure.Search;
using JobFetchProbe.Models;
using JobFetchProbe.Services.Search;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace JobFetchProbe.Console;

/// <summary>
/// The "search" command: reads one search results page (link or saved file, or Search:DefaultUrl when none is given),
/// prints the jobs found and stores their links. Each request gets its own DI scope, like JobConsole.
/// </summary>
public sealed class SearchConsole(IServiceScopeFactory scopes, IOptions<SearchSettings> settings)
{
    public const string Command = "search";

    /// <summary>True when the input is the search command, with or without an argument.</summary>
    public static bool IsSearch(string input) =>
        input.Equals(Command, StringComparison.OrdinalIgnoreCase) ||
        input.StartsWith(Command + " ", StringComparison.OrdinalIgnoreCase);

    /// <summary>Runs "search" or "search &lt;link | file&gt;" as typed at the prompt.</summary>
    public Task<bool> HandleLineAsync(string line) =>
        HandleAsync(line.Length > Command.Length ? line[Command.Length..].Trim().Trim('"') : null);

    public async Task<bool> HandleAsync(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            input = settings.Value.DefaultUrl;
            if (string.IsNullOrWhiteSpace(input))
            {
                System.Console.WriteLine("Give a search link or saved page: search <link | file>  (or set Search:DefaultUrl in appsettings.json)");
                return false;
            }
            System.Console.WriteLine($"Using Search:DefaultUrl {input}");
        }

        using var scope = scopes.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<ISearchService>();

        var result = Uri.TryCreate(input, UriKind.Absolute, out var url) && (url.Scheme == Uri.UriSchemeHttp || url.Scheme == Uri.UriSchemeHttps)
            ? await service.FromUrlAsync(url)
            : await service.FromFileAsync(input);

        if (result.Status != JobResultStatus.Saved || result.Search is null)
        {
            System.Console.WriteLine(result.Message);
            return false;
        }

        Print(result.Search);
        System.Console.WriteLine();
        System.Console.WriteLine($"Saved to {result.SavedTo}");
        return true;
    }

    private static void Print(JobSearch search)
    {
        System.Console.WriteLine($"Search:    {search.Query ?? search.SearchId}");
        System.Console.WriteLine($"Search ID: {search.SearchId}");
        System.Console.WriteLine($"Jobs:      {search.Count} in this page" +
                                 (search.TotalResults is { } total ? $" (the site reports {total} in total)" : string.Empty));
        System.Console.WriteLine($"(read from {search.ParsedFrom})");
        System.Console.WriteLine();

        var n = 0;
        foreach (var job in search.Jobs)
        {
            var who = string.Join(" · ", new[] { job.Company, job.Location }.Where(s => !string.IsNullOrWhiteSpace(s)));
            System.Console.WriteLine($"{++n,4}. {job.JobId}  {job.Title}{(who.Length > 0 ? $"  ({who})" : string.Empty)}");
        }

        if (search.TotalResults is { } t && t > search.Count)
        {
            System.Console.WriteLine();
            System.Console.WriteLine($"Only the {search.Count} jobs in this page were read. To include more, scroll further in the browser before saving the page.");
        }
    }
}
