# JobFetchProbe

A .NET console app: give it a link to a job posting, or a page you saved from your browser. It prints the job's title, company, location, date, details and description, and saves them as `output/<JobId>.json`.

## Run

```sh
dotnet run --project src/JobFetchProbe.Console
```

```text
Paste a job link, or the path of a page you saved from your browser. Type 'exit' to quit.

Job link or file: C:\Code\job-fetch-probe\out\job-4478111194.saved.html
Job ID:    4478111194
Title:     Técnico/a de Desarrollo
Company:   Improving Logistics & Consulting S.L.
Location:  Almussafes
...
Saved to C:\Code\job-fetch-probe\output\4478111194.json
```

You can also pass one link or file directly: `dotnet run --project src/JobFetchProbe.Console -- <job-link-or-file>`.

### Saved pages

Sites such as LinkedIn often refuse plain requests. When that happens, open the job in your browser (a private window shows what an anonymous visitor gets), save it, and give the app the file path instead of the link. Both a normal "Save page as" file and a Chrome **view-source** save (Ctrl+U, then Ctrl+S) work. Save them in `out/` or name them `*.saved.html` so Git ignores them.

## Configuration

`src/JobFetchProbe.Console/appsettings.json`:

| Setting | Default | Meaning |
| --- | --- | --- |
| `Output:Folder` | `output` | Where `<JobId>.json` files go. Relative paths start from the folder you run the app in |
| `HttpClient:TimeoutSeconds` | `20` | Request timeout |
| `HttpClient:UserAgent` | `JobFetchProbe/0.3 (personal study project)` | How the app identifies itself |
| `HttpClient:HandlerLifetimeMinutes` | `5` | How long the client factory reuses a connection handler before rotating it |

Any setting can be overridden with an environment variable, using `__` for `:`, e.g. `Output__Folder=D:\jobs`.

## Output

One file per job, named after the job ID (the number at the end of the `/jobs/view/` link, or of the page's canonical link for saved files). Reading the same job again overwrites its file. If no ID can be found, the name is `job-<date>-<time>`.

```json
{
  "jobId": "4478111194",
  "sourceUrl": "https://es.linkedin.com/jobs/view/...-4478111194",
  "title": "Técnico/a de Desarrollo",
  "company": "Improving Logistics & Consulting S.L.",
  "location": "Almussafes",
  "posted": "Hace 23 horas",
  "employmentType": null,
  "details": [ { "label": "Tipo de empleo", "value": "Jornada completa" } ],
  "description": "...",
  "parsedFrom": "LinkedIn public job page markup",
  "retrievedAt": "2026-10-14T13:12:20+00:00"
}
```

## Structure

```text
Console ──► Services ──► Models
   │            ▲
   └──► Infrastructure (implements the Services interfaces)
```

| Project | Role | Main files |
| --- | --- | --- |
| `JobFetchProbe.Models` | Data shapes shared by all layers. No dependencies | `JobPosting`, `JobDetail`, `FetchedPage`, `JobResult` |
| `JobFetchProbe.Services` | Business logic and the interfaces it needs | `JobService`, `Parsing/JobPageParser`, `Parsing/JobIdResolver`, `Abstractions/IPageFetcher`, `ISavedPageReader`, `IJobRepository` |
| `JobFetchProbe.Infrastructure` | HTTP, files and JSON output | `Http/HttpPageFetcher` (typed client from `IHttpClientFactory`), `Files/SavedPageReader`, `Storage/JsonJobRepository`, settings classes, `DependencyInjection` |
| `JobFetchProbe.Console` | Entry point: configuration, dependency injection, prompt | `Program`, `JobConsole`, `appsettings.json` |

### HTTP client

The client is a **typed client** registered with `AddHttpClient<IPageFetcher, HttpPageFetcher>()`. `IHttpClientFactory` creates the `HttpClient` instances and pools the underlying connection handlers, rotating them every `HandlerLifetimeMinutes`. This avoids the two classic problems of `new HttpClient()`: running out of sockets when clients are created and disposed often, and stale DNS when one client lives forever. `HttpPageFetcher` never creates or disposes an `HttpClient`, and `IJobService` is transient and resolved in a new scope for each request, so no long-lived object pins a handler.

The client sends no cookies and makes one attempt. If the site refuses the request (401, 403, 429, 999) or redirects to a sign-in page, the app says so and stops; it does not retry or try to get around the block.

### Page layouts

`JobPageParser` tries three layouts in order: schema.org `JobPosting` data embedded for search engines; LinkedIn's public job page (`topcard__title`, `topcard__org-name-link`, `description__job-criteria-*`, `show-more-less-html__markup`); then the page title and visible text. The LinkedIn class names come from one saved public job page (October 2026). If LinkedIn changes its markup, the app falls back to the generic layout; check a fresh saved page and update the class names.

## Archive

The earlier probe version (crawler-policy check, tests, plan and spec) is in [`archive/probe-v1`](archive/probe-v1). It is not part of the build.
