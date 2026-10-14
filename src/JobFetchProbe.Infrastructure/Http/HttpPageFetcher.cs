using JobFetchProbe.Models;
using JobFetchProbe.Services.Abstractions;

namespace JobFetchProbe.Infrastructure.Http;

/// <summary>
/// Typed client: the HttpClient is created and configured by IHttpClientFactory (see DependencyInjection).
/// Do not dispose it and do not create HttpClient instances here; the factory pools and rotates the
/// underlying handlers, which avoids socket exhaustion and stale DNS.
/// One attempt, no cookies; a refusal or sign-in redirect is reported as Blocked, never retried.
/// </summary>
public sealed class HttpPageFetcher(HttpClient http) : IPageFetcher
{
    private static readonly int[] RefusedStatuses = [401, 403, 429, 999];
    private static readonly string[] SignInPaths = ["/login", "/authwall", "/checkpoint", "/signin", "/uas/login"];

    public async Task<FetchedPage> GetAsync(Uri url, CancellationToken ct = default)
    {
        try
        {
            using var response = await http.GetAsync(url, ct);
            var finalUrl = response.RequestMessage?.RequestUri ?? url;
            var html = await response.Content.ReadAsStringAsync(ct);
            var status = (int)response.StatusCode;

            var blocked = RefusedStatuses.Contains(status) ||
                          SignInPaths.Any(p => finalUrl.AbsolutePath.Contains(p, StringComparison.OrdinalIgnoreCase));
            var error = !blocked && !response.IsSuccessStatusCode ? $"HTTP {status}" : null;

            return new FetchedPage(status, finalUrl, html, blocked, error);
        }
        catch (HttpRequestException ex)
        {
            return new FetchedPage(0, url, string.Empty, false, ex.Message);
        }
        catch (TaskCanceledException) when (!ct.IsCancellationRequested)
        {
            return new FetchedPage(0, url, string.Empty, false, "timed out");
        }
    }
}
