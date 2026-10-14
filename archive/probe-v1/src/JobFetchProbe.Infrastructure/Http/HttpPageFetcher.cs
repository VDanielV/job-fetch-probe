using System.Text;
using JobFetchProbe.Application.Abstractions;
using JobFetchProbe.Application.Dtos;
using JobFetchProbe.Domain.Rules;

namespace JobFetchProbe.Infrastructure.Http;

/// <summary>
/// The only network code in the app. Identifies itself, sends no cookies or authorization,
/// makes one attempt, and follows at most 3 redirects that stay on linkedin.com.
/// A redirect to a login or checkpoint URL is recorded and not followed (plan, stop rules).
/// </summary>
/// <remarks>The handler (no cookies, no auto-redirect, decompression) is configured in <see cref="DependencyInjection"/>.</remarks>
public sealed class HttpPageFetcher(HttpClient http) : IPageFetcher
{
    public const string UserAgent = "JobFetchProbe/0.1 (academic exercise; unattended-contact: none)";
    public const int MaxRedirects = 3;

    public async Task<FetchResult> GetAsync(Uri url, string accept, CancellationToken ct = default)
    {
        var hops = new List<RedirectHop>();
        var current = url;
        var sent = 0;

        try
        {
            while (true)
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, current);
                request.Headers.TryAddWithoutValidation("User-Agent", UserAgent);
                request.Headers.Accept.ParseAdd(accept);
                sent++;

                using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
                var status = (int)response.StatusCode;
                var contentType = response.Content.Headers.ContentType?.MediaType;

                if (status is >= 300 and < 400 && response.Headers.Location is { } location)
                {
                    var next = location.IsAbsoluteUri ? location : new Uri(current, location);

                    string? stop = null;
                    if (ResponseClassifier.IsAuthPath(next))
                    {
                        stop = "redirect to a login or checkpoint URL; not followed";
                    }
                    else if (next.Scheme != Uri.UriSchemeHttps || !IsLinkedInHost(next.Host))
                    {
                        stop = "redirect leaves https linkedin.com; not followed";
                    }
                    else if (hops.Count >= MaxRedirects)
                    {
                        stop = $"redirect limit ({MaxRedirects}) reached";
                    }

                    hops.Add(new RedirectHop(status, next.Host, Followed: stop is null));

                    if (stop is not null)
                    {
                        return new FetchResult
                        {
                            Status = status,
                            RequestedUrl = url,
                            FinalUrl = next,
                            ContentType = contentType,
                            RequestsSent = sent,
                            Hops = hops,
                            StopReason = stop,
                        };
                    }

                    current = next;
                    continue;
                }

                var bytes = await response.Content.ReadAsByteArrayAsync(ct);
                return new FetchResult
                {
                    Status = status,
                    RequestedUrl = url,
                    FinalUrl = current,
                    ContentType = contentType,
                    Body = Encoding.UTF8.GetString(bytes),
                    BodyLength = bytes.Length,
                    RequestsSent = sent,
                    Hops = hops,
                };
            }
        }
        catch (HttpRequestException ex)
        {
            return Failed(url, current, sent, hops, $"transport: {ex.HttpRequestError}");
        }
        catch (TaskCanceledException) when (!ct.IsCancellationRequested)
        {
            return Failed(url, current, sent, hops, "timeout");
        }
    }

    private static bool IsLinkedInHost(string host) =>
        host.Equals("linkedin.com", StringComparison.OrdinalIgnoreCase) ||
        host.EndsWith(".linkedin.com", StringComparison.OrdinalIgnoreCase);

    private static FetchResult Failed(Uri url, Uri current, int sent, List<RedirectHop> hops, string error) => new()
    {
        Status = 0,
        RequestedUrl = url,
        FinalUrl = current,
        RequestsSent = sent,
        Hops = hops,
        TransportError = error,
    };
}
