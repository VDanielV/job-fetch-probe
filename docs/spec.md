# Spec: JobFetchProbe

.NET 10 console app with four steps:

- **Job step:** reads one job posting from a link or a saved page, prints it, and stores it as `<JobId>.json`.
- **Search step:** reads one search results page from a link or a saved page, prints the jobs it lists, and stores their links as `<SearchId>.json`.
- **Inbox step:** reads every saved search page in an inbox folder, one by one, stores all their jobs together as `<yyyyMMdd_HHmmss>.json`, and moves each page read to an archive folder. Runs at start-up and on demand.
- **Details step:** reads the jobs of an inbox listing one by one with the job step's code, politely and behind a circuit breaker, and stores all their descriptions in one `<yyyyMMdd_HHmmss>.json`.

At start-up and with `inbox`, the inbox and details steps run as one process: inbox → listing → descriptions → a summary with the totals.

Everything the steps store goes either to JSON files or to one SQLite database, chosen by `Storage:Provider` (see [Storage](#storage)). The steps behave the same with both.

Each step was added alongside the earlier ones; their behaviour is unchanged.

This spec describes the current code. It follows [`plan.md`](plan.md). The original probe spec is kept in Git history under the tag `probe-v1` (`docs/spec.md` there).

## Solution layout

```text
job-fetch-probe/
  JobFetchProbe.sln
  Directory.Build.props            net10.0, nullable, implicit usings
  src/
    JobFetchProbe.Models/
      JobPosting.cs  JobDetail.cs  FetchedPage.cs  JobResult.cs
      JobListing.cs  JobSearch.cs  SearchResult.cs                       (search step)
      JobSearchBatch.cs  InboxRunResult.cs                               (inbox step)
      JobDetailsBatch.cs  DetailsRunResult.cs                            (details step)
    JobFetchProbe.Services/
      IJobService.cs  JobService.cs  DependencyInjection.cs
      Abstractions/  IPageFetcher.cs  ISavedPageReader.cs  IJobRepository.cs  ISearchRepository.cs
                     IInboxFolder.cs  IBatchRepository.cs  IListingStore.cs  IDetailsRepository.cs
      Parsing/       IJobPageParser.cs  JobPageParser.cs  JobIdResolver.cs
      Search/        ISearchService.cs  SearchService.cs  ISearchPageParser.cs  SearchPageParser.cs
                     ParsedSearch.cs  SearchIdResolver.cs  SearchDependencyInjection.cs
      Inbox/         IInboxService.cs  InboxService.cs  InboxDependencyInjection.cs
      Details/       IDetailsService.cs  DetailsService.cs  CircuitBreaker.cs  DetailsDependencyInjection.cs
    JobFetchProbe.Infrastructure/
      DependencyInjection.cs
      Http/     HttpPageFetcher.cs  HttpClientSettings.cs
      Files/    SavedPageReader.cs
      Storage/  JsonJobRepository.cs  OutputSettings.cs  StorageSettings.cs
      Database/ JobsDbContext.cs  Rows.cs  JobRowMapping.cs  SqliteLocations.cs  DatabaseDependencyInjection.cs
                SqliteJobRepository.cs  SqliteSearchRepository.cs  SqliteBatchRepository.cs
                SqliteListingStore.cs  SqliteDetailsRepository.cs  DesignTimeJobsDbContextFactory.cs
                Migrations/  <timestamp>_InitialCreate.cs (+ .Designer.cs)  JobsDbContextModelSnapshot.cs
      Search/   JsonSearchRepository.cs  SearchSettings.cs  SearchDependencyInjection.cs
      Inbox/    InboxFolder.cs  JsonBatchRepository.cs  InboxSettings.cs  InboxDependencyInjection.cs
      Details/  JsonListingStore.cs  JsonDetailsRepository.cs  DetailsSettings.cs  DetailsDependencyInjection.cs
    JobFetchProbe.Console/
      Program.cs  JobConsole.cs  SearchConsole.cs  InboxConsole.cs  DetailsConsole.cs
      PipelineConsole.cs  ConsoleLog.cs  DataPaths.cs  appsettings.json
  docs/       plan.md  spec.md
```

The repository holds code and documents only. Everything the app reads or writes is under `Paths:DataRoot`, a sibling folder outside the repository, so Git commands (`git clean -fdx`, a fresh clone) never touch it:

```text
job-fetch-probe-data/              Paths:DataRoot (C:\Code\job-fetch-probe-data)
  database/jobs.db                 Storage:Sqlite:Path
  input/                           Inbox:Folder: saved search pages to read
    archived/                      Inbox:ArchiveFolder: pages already read
  pages/                           job pages saved by hand, given to the job step by path (not read automatically)
  output/                          JSON storage only
    jobs/                          Output:Folder: <JobId>.json
    listings/                      Inbox:OutputFolder: <yyyyMMdd_HHmmss>.json
    descriptions/                  Details:OutputFolder: <yyyyMMdd_HHmmss>.json
    searches/                      Search:OutputFolder: <SearchId>.json
```

`DataPaths` (Console) adds these paths as an in-memory configuration source **underneath** all others (index 0), computed from the final value of `Paths:DataRoot`. A folder set in `appsettings.json` or an environment variable therefore still wins on its own. Without `Paths:DataRoot`, each setting is used as given (or its code default). At start-up the app creates `input`, `input/archived` and `pages`, and prints `Data folder: …`; the database and output folders are created on first use.

## Dependencies

```text
Console ──► Services ──► Models
   │            ▲
   └──► Infrastructure (implements the Services interfaces)
```

| Project | References | Packages |
| --- | --- | --- |
| Models | none | none |
| Services | Models | `Microsoft.Extensions.DependencyInjection.Abstractions` 10.0.0 |
| Infrastructure | Services | `Microsoft.Extensions.Http` 10.0.0, `Microsoft.Extensions.Options.ConfigurationExtensions` 10.0.0, `Microsoft.EntityFrameworkCore.Sqlite` 10.0.13 |
| Console | Services, Infrastructure | `Microsoft.Extensions.Hosting` 10.0.0, `Microsoft.EntityFrameworkCore.Design` 10.0.13 (design time only, `PrivateAssets=all`) |

Services must not use `HttpClient`, `File` or `Directory`. All outside access goes through the interfaces in `Services/Abstractions`. The search step reuses `IPageFetcher` and `ISavedPageReader` and adds `ISearchRepository`.

## Operator contract

```text
job-fetch-probe                          prompt for links or files until 'exit'
job-fetch-probe <job-link | file>        job step: read one job and exit
job-fetch-probe search [<link | file>]   search step: read one search results page and exit
                                         (no argument: Search:DefaultUrl)
job-fetch-probe inbox                    whole process once and exit: inbox → details → summary
job-fetch-probe details [<listing>]      details step for a listing (default: the newest) and exit;
                                         <listing> is a batch ID (20261014_183201), a listing file name, or its path
```

At the prompt, `search`, or `search <link | file>`, runs the search step, `inbox` runs the whole process, and `details` or `details <listing.json>` runs the details step followed by the summary; any other input runs the job step as before.

When `Inbox:ProcessOnStart` is true, every start of the app first runs the whole process (silently if the inbox is empty), then continues with the command or prompt. The `inbox` and `details` commands do not trigger it a second time. The details step runs after the inbox only when a listing was written and `Details:RunAfterInbox` is true.

Run from the repository root with `dotnet run --project src/JobFetchProbe.Console [-- <args>]`.

Input handling:

- Leading and trailing spaces and surrounding double quotes are removed.
- An absolute `http` or `https` URL is treated as a link. Anything else is treated as a file path.
- `exit` (any case) or end of input ends the prompt.

Exit codes:

| Code | Meaning |
| --- | --- |
| 0 | Interactive session ended, or the single input (job or search) was saved |
| 1 | The single input was blocked or failed, or `search` was given no input and no `Search:DefaultUrl` is set, or `inbox` left a page in the inbox or a listed job without its description, or `details` ended with failed or pending jobs |
| 2 | Configuration error (`OptionsValidationException`), including an unknown `Storage:Provider` |
| 3 | Database error: the SQLite file is locked, read-only, damaged or not a database (`SqliteException`, `DbUpdateException`) |

## Models

### `JobPosting` (record)

| Property | Type | Rule |
| --- | --- | --- |
| `JobId` | `string` | **Required.** See "Job ID" |
| `SourceUrl` | `Uri` | **Required.** Absolute `http`/`https` address where the job can be reached |
| `Title` | `string?` | |
| `Company` | `string?` | |
| `Location` | `string?` | |
| `Posted` | `string?` | Text as shown on the page, or `datePosted` from JSON-LD |
| `EmploymentType` | `string?` | From JSON-LD only |
| `Details` | `IReadOnlyList<JobDetail>` | Labelled facts; empty list by default |
| `Description` | `string?` | Plain text with paragraph and list breaks |
| `ParsedFrom` | `string` | Which layout matched |
| `RetrievedAt` | `DateTimeOffset` | UTC time of the read |

### Others

| Type | Shape |
| --- | --- |
| `JobDetail` | `record (string Label, string Value)` |
| `FetchedPage` | `record (int Status, Uri FinalUrl, string Html, bool Blocked, string? Error)` |
| `JobResult` | `record (JobResultStatus Status, JobPosting? Job, string? SavedTo, string? Message)` with factories `Saved`, `Blocked`, `Failed` |
| `JobResultStatus` | `Saved`, `Blocked`, `Failed` |

## Services

### `IJobService`

```csharp
Task<JobResult> FromUrlAsync(Uri url, CancellationToken ct = default);
Task<JobResult> FromFileAsync(string path, CancellationToken ct = default);
```

`FromUrlAsync`:

1. Non-`http`/`https` URL → `Failed`.
2. Fetch with `IPageFetcher`. `Blocked` → `Blocked` with a message suggesting a saved page. `Error` → `Failed`.
3. `SourceUrl` = the URL as given (query string kept).
4. Job ID = from the URL, else from the page's canonical link, else the fallback.
5. Parse, set `RetrievedAt`, save, return `Saved`.

`FromFileAsync`:

1. Read with `ISavedPageReader`. `FileNotFoundException`, `DirectoryNotFoundException` or `UnauthorizedAccessException` → `Failed`.
2. `SourceUrl` = the page's canonical link, else its `og:url`, if absolute `http`/`https`. If neither → `Failed`: *the saved page does not contain its own address*.
3. Job ID = from the canonical link, else from the file name, else the fallback.
4. Parse, set `RetrievedAt`, save, return `Saved`.

Lifetime: transient. Resolved inside a new DI scope for every input.

### Job ID (`JobIdResolver`)

| Source | Pattern | Example |
| --- | --- | --- |
| URL path | `/jobs/view/(?:[^/]*?-)?(\d{6,})/?$` | `/jobs/view/4478111194/`, `/jobs/view/tecnico-…-sl-4478111194` |
| Page | canonical link, else `og:url`, then the URL rule | `<link rel="canonical" href="…-4478111194">` |
| File name | `jobs_view_(?:.*?-)?(\d{6,})` | `view-source_https___www.linkedin.com_jobs_view_4478111194.html` |
| Fallback | `job-yyyyMMdd-HHmmss` (UTC) | `job-20261014-133911` |

### Parsing (`IJobPageParser`, `JobPageParser`)

```csharp
JobPosting Parse(string html, string jobId, Uri sourceUrl);
```

Singleton. Tries three layouts in order; the first that matches wins. LinkedIn comes first because most pages read with this app are LinkedIn job pages.

**1. LinkedIn public job page.** Matches when an `<h1>` has class `topcard__title`.

| Field | Element |
| --- | --- |
| Title | `<h1 class="… topcard__title">` |
| Company | `<a class="topcard__org-name-link …">` |
| Location | first `<span class="topcard__flavor topcard__flavor--bullet">` (exact class) |
| Posted | `<span class="posted-time-ago__text …">` |
| Details | each `<h3 class="description__job-criteria-subheader">` + following `<span class="description__job-criteria-text…">`, then `Applicants` from `<span class="num-applicants__caption …">` |
| Description | `<div class="show-more-less-html__markup…">` |
| ParsedFrom | `LinkedIn public job page markup` |

Class names were taken from one saved public job page in October 2026.

**2. schema.org JSON-LD.** Any `<script type="application/ld+json">` whose content is, or contains (array or `@graph`), an object with `@type` `JobPosting`.

| Field | JSON-LD property |
| --- | --- |
| Title | `title` |
| Company | `hiringOrganization.name`, else `hiringOrganization` |
| Location | first `jobLocation`: `address.addressLocality`, `addressRegion`, `addressCountry(.name)` joined with ", ", else its `name`. If there is no `jobLocation`: `jobLocationType` |
| Posted | `datePosted` |
| EmploymentType | `employmentType` (arrays joined with ", ") |
| Description | `description`, HTML converted to text |
| ParsedFrom | `structured job data (JSON-LD)` |

**3. Generic fallback.**

| Field | Source |
| --- | --- |
| Title | `og:title`, else first `<h1>`, else `<title>` |
| Company | `og:site_name` |
| Description | the page's visible text, or `og:description` / `description` meta if longer |
| ParsedFrom | `page markup (no structured job data found)` |

Meta content is HTML-decoded twice (some sites double-escape).

**HTML to text:** remove `script`, `style`, `noscript`, `svg`, `head` and comments; line break for `<br>` and closing `p`, `div`, `h1`–`h6`, `ul`, `ol`, `section`, `article`, `tr`; `- ` before each `<li>`; inline tags (`strong`, `b`, `em`, `i`, `u`, `span`, `a`, `small`, `mark`) removed without adding a space; other tags become a space; entities decoded; spaces collapsed; at most one blank line in a row.

## Infrastructure

### HTTP (`HttpPageFetcher`)

Typed client registered with `AddHttpClient<IPageFetcher, HttpPageFetcher>()`. The class receives its `HttpClient` from the factory and never creates or disposes one.

| Setting | Value |
| --- | --- |
| Timeout | `HttpClient:TimeoutSeconds` |
| `User-Agent` | `HttpClient:UserAgent` |
| `Accept` | `text/html` |
| Cookies | off (`UseCookies = false`) |
| Redirects | followed automatically, at most 5 |
| Decompression | all (gzip, deflate, brotli) |
| Handler lifetime | `HttpClient:HandlerLifetimeMinutes` |
| Retries | none |

Result rules:

| Condition | `FetchedPage` |
| --- | --- |
| Status 401, 403, 429 or 999 | `Blocked = true` |
| Final URL path contains `/login`, `/authwall`, `/checkpoint`, `/signin` or `/uas/login` | `Blocked = true` |
| Other non-success status | `Error = "HTTP <status>"` |
| `HttpRequestException` | `Status = 0`, `Error` = exception message |
| Timeout | `Status = 0`, `Error = "timed out"` |

### Saved pages (`SavedPageReader`)

Singleton. Reads the file as text. If it is a Chrome view-source save (contains `line-gutter-backdrop` and `<td class="line-content">`), the original HTML is rebuilt from the `line-content` cells: tags removed, entities decoded, one line per cell.

### Storage (`JsonJobRepository`)

Singleton.

- Folder: `Path.GetFullPath(Output:Folder)`, so a relative folder is resolved from the current directory. Created if missing.
- File: `<JobId>.json`, with characters invalid in file names replaced by `_`. If it exists it is overwritten; no previous versions or backups are kept.
- Format: `System.Text.Json`, web defaults (camelCase), indented, `UnsafeRelaxedJsonEscaping` so accents and emoji stay readable. `SourceUrl` is written as a string.

```json
{
  "jobId": "4478111194",
  "sourceUrl": "https://es.linkedin.com/jobs/view/t%C3%A9cnico-a-de-desarrollo-at-improving-logistics-consulting-sl-4478111194",
  "title": "Técnico/a de Desarrollo",
  "company": "Improving Logistics & Consulting S.L.",
  "location": "Almussafes",
  "posted": "Hace 23 horas",
  "employmentType": null,
  "details": [
    { "label": "Nivel de antigüedad", "value": "Intermedio" },
    { "label": "Tipo de empleo", "value": "Jornada completa" },
    { "label": "Sectores", "value": "Transporte, logística, cadena de suministro y almacenamiento" },
    { "label": "Applicants", "value": "25 solicitudes" }
  ],
  "description": "Somos una compañía especializada en servicios de Logística Integral, …",
  "parsedFrom": "LinkedIn public job page markup",
  "retrievedAt": "2026-10-14T13:39:11+00:00"
}
```

## Configuration

`src/JobFetchProbe.Console/appsettings.json`, copied to the build output. The host's content root is the build output folder, so the file is found however the app is started. Command-line arguments are not passed to configuration. Environment variables override settings (`Paths__DataRoot=D:\job-data`).

| Key | Default | Validation |
| --- | --- | --- |
| `Paths:DataRoot` | `C:\Code\job-fetch-probe-data` | none; when set, the folder keys marked † default to the layout above |
| `Storage:Provider` | `Sqlite` (code default `Json`) | `Json` or `Sqlite`, any case |
| `Storage:Sqlite:Path` † | `<DataRoot>\database\jobs.db` | not empty when the provider is `Sqlite` |
| `Output:Folder` † | `<DataRoot>\output\jobs` | not empty |
| `Search:OutputFolder` † | `<DataRoot>\output\searches` | not empty |
| `Search:DefaultUrl` | `https://es.linkedin.com/jobs/.net-empleos-valencia` | empty, or an absolute `http`/`https` link |
| `Inbox:Folder` † | `<DataRoot>\input` | not empty |
| `Inbox:ArchiveFolder` † | `<DataRoot>\input\archived` | not empty, different from `Inbox:Folder` |
| `Inbox:OutputFolder` † | `<DataRoot>\output\listings` | not empty |
| `Inbox:ProcessOnStart` | `true` | none |
| `Details:OutputFolder` † | `<DataRoot>\output\descriptions` | not empty |
| `Details:DelaySeconds` | `5` | 1 to 120; pause between two requests |
| `Details:RunAfterInbox` | `true` | none |
| `Details:CircuitBreaker:FailureThreshold` | `3` | 1 to 20; failures in a row that open the circuit |
| `Details:CircuitBreaker:CooldownSeconds` | `300` | 1 to 14400; first wait while open |
| `Details:CircuitBreaker:MaxCooldownSeconds` | `1800` | 1 to 86400; the wait doubles up to this |
| `Details:CircuitBreaker:StopAfterOpenings` | `3` | 1 to 10; the run stops at this opening |
| `Inbox:Patterns` | `*.html`, `*.htm` (code default, not in the file) | at least one |
| `HttpClient:TimeoutSeconds` | `20` | 1 to 120 |
| `HttpClient:UserAgent` | `JobFetchProbe/0.3 (personal study project)` | not empty |
| `HttpClient:HandlerLifetimeMinutes` | `5` | none |
| `Logging:LogLevel:Default` | `Warning` | none; keeps HttpClient request logs off the console |

With `Sqlite`, the folder settings (`Output`, `Search:OutputFolder`, `Inbox:OutputFolder`, `Details:OutputFolder`) are still validated but not written to. `Inbox:Folder` and `Inbox:ArchiveFolder` are used with both providers.

Validation runs when the settings are first used. A failure prints `Configuration error: …` and exits with 2.

## Dependency injection

| Registration | Lifetime |
| --- | --- |
| `TimeProvider.System` | singleton (if not already registered) |
| `IJobPageParser` → `JobPageParser` | singleton |
| `IJobService` → `JobService` | transient |
| `IPageFetcher` → `HttpPageFetcher` | typed client (transient, factory-managed handler) |
| `ISavedPageReader` → `SavedPageReader` | singleton |
| `IJobRepository` → `JsonJobRepository` | singleton |
| `JobConsole` | singleton; creates a scope per input via `IServiceScopeFactory`; passes `search` lines to `SearchConsole` |
| `ISearchPageParser` → `SearchPageParser` | singleton |
| `ISearchService` → `SearchService` | transient (depends on the typed client) |
| `ISearchRepository` → `JsonSearchRepository` | singleton |
| `SearchConsole` | singleton; creates a scope per input |
| `IInboxService` → `InboxService` | transient |
| `IInboxFolder` → `InboxFolder` | singleton |
| `IBatchRepository` → `JsonBatchRepository` | singleton |
| `InboxConsole` | singleton; creates a scope per run |
| `IDetailsService` → `DetailsService` | transient (depends on `IJobService`) |
| `IListingStore` → `JsonListingStore` | singleton |
| `IDetailsRepository` → `JsonDetailsRepository` | singleton |
| `DetailsConsole`, `PipelineConsole` | singletons; `DetailsConsole` creates a scope per run |
| `IDbContextFactory<JobsDbContext>` | singleton (Sqlite only); each repository call opens and disposes its own short-lived context |
| `IJobRepository`, `ISearchRepository`, `IBatchRepository`, `IListingStore`, `IDetailsRepository` → `Sqlite…` | singletons; with Sqlite they **replace** the JSON registrations |

Extension methods: `AddJobServices()` (Services), `AddInfrastructure(IConfiguration)` (Infrastructure); for the inbox step `AddInboxServices()` and `AddInboxInfrastructure(IConfiguration)` (both need the search step's registrations); for the details step `AddDetailsServices()` and `AddDetailsInfrastructure(IConfiguration)` (need the job step's registrations); `AddStorage(IConfiguration)` last, which with Sqlite swaps the five storage registrations for the database ones (`ServiceCollection.Replace`) and leaves the rest untouched; for the search step, in separate classes so the job step's registrations are untouched: `AddSearchServices()` (Services) and `AddSearchInfrastructure(IConfiguration)` (Infrastructure). `AddSearchInfrastructure` relies on `AddInfrastructure` for `IPageFetcher` and `ISavedPageReader`.

## Console output

```text
Job ID:    4478111194
Title:     Técnico/a de Desarrollo
Company:   Improving Logistics & Consulting S.L.
Location:  Almussafes
Posted:    Hace 23 horas
Nivel de antigüedad: Intermedio
...
(read from LinkedIn public job page markup)

Description:
...

Saved to C:\Code\job-fetch-probe-data\output\jobs\4478111194.json
```

Empty fields are not printed. Blocked or failed inputs print one message line and nothing is saved.

## Search step

Reads the job links from one search results page and stores them in one JSON file per search. It reuses the job step's `IPageFetcher` (same typed `HttpClient`, same blocked-request rules) and `ISavedPageReader` (same view-source unwrapping).

Only the jobs present in the page are read. The app does not request further result pages: LinkedIn loads them from its `/jobs-guest/` endpoints, which its `robots.txt` disallows. To include more jobs, scroll further in the browser before saving the page.

### Models

`JobListing` (record):

| Property | Type | Rule |
| --- | --- | --- |
| `JobId` | `string` | **Required.** From the card's `urn:li:jobPosting:<id>`, or from the link |
| `Url` | `Uri` | **Required.** The card's `/jobs/view/` link with query and fragment removed; if the card has none, `https://<host>/jobs/view/<JobId>/` |
| `Title` | `string?` | |
| `Company` | `string?` | |
| `Location` | `string?` | |
| `ListedAt` | `string?` | The card's `datetime` value, e.g. `2026-10-13` |

`JobSearch` (record):

| Property | Type | Rule |
| --- | --- | --- |
| `SearchId` | `string` | **Required.** See "Search ID" |
| `SourceUrl` | `Uri` | **Required.** Absolute `http`/`https` address of the search page |
| `Query` | `string?` | The page's own description of the search, else its `<title>` |
| `TotalResults` | `int?` | Total the site reports (digits of `results-context-header__job-count`) |
| `Count` | `int` | Number of jobs in `Jobs` (read-only, written to JSON) |
| `Jobs` | `IReadOnlyList<JobListing>` | Unique by `JobId`, in page order |
| `ParsedFrom` | `string` | Which layout matched |
| `RetrievedAt` | `DateTimeOffset` | UTC time of the read |

`SearchResult`: `record (JobResultStatus Status, JobSearch? Search, string? SavedTo, string? Message)` with factories `Saved`, `Blocked`, `Failed`, `Read`. `JobResultStatus.Read` means read successfully but not stored on its own (used by the inbox step).

### `ISearchService`

```csharp
Task<SearchResult> FromUrlAsync(Uri url, CancellationToken ct = default);
Task<SearchResult> FromFileAsync(string path, CancellationToken ct = default);
Task<SearchResult> ReadFileAsync(string path, CancellationToken ct = default);   // read without storing
```

`FromUrlAsync`:

1. Non-`http`/`https` URL → `Failed`.
2. Fetch with `IPageFetcher`. `Blocked` → `Blocked`. `Error` → `Failed`.
3. `SourceUrl` = the URL as given.
4. Parse; then "Common rules".

`FromFileAsync`:

1. Read with `ISavedPageReader`. File errors → `Failed`.
2. `SourceUrl` = the page's canonical link, else `og:url`, else the browser's `<!-- saved from url=(n)… -->` note if it is an `http`/`https` address. If none: a bot-check page → `Blocked`; anything else → `Failed` (*the saved page does not contain its own address*).
3. Parse; then "Common rules".

`ReadFileAsync` does the same as `FromFileAsync` but returns `Read` instead of saving. `FromFileAsync` is `ReadFileAsync` followed by save.

Common rules:

- The address has a `currentJobId`, the page has no LinkedIn result cards, and the only job found (if any) is that `currentJobId` → `Failed`: *the page only contains the selected job, not the result list*. This is a view-source save of LinkedIn's signed-in search, which loads the list later with scripts.
- No jobs found and the page is a bot check → `Blocked`: *the page is a bot check, not the search results… save the page from your browser*.
- No jobs found otherwise → `Failed`: *no job links found in the page*, with a hint that a view-source save of a signed-in LinkedIn search contains only the selected job.
- Otherwise build `JobSearch`, save, return `Saved`.

Lifetime: transient, resolved in a new DI scope for every input.

### Search ID (`SearchIdResolver`)

Named after the page's canonical address when it has one, otherwise after `SourceUrl`, so a link and a saved copy of the same search write to the same file.

| Address | Rule | Example |
| --- | --- | --- |
| path ends in `/jobs/search` | `keywords` and `location` query values, joined with `-` | `/jobs/search?keywords=.net&location=Valencia` → `net-valencia` |
| any other path | last path segment, URL-decoded | `/jobs/%2Enet-empleos-valencia` → `net-empleos-valencia` |
| neither gives text | fallback | `search-20261014-140524` |

Slug rule: lower case, accents removed, every run of characters other than `a`–`z` and `0`–`9` becomes one `-`, leading and trailing `-` removed.

### Parsing (`ISearchPageParser`, `SearchPageParser`)

```csharp
ParsedSearch Parse(string html, Uri pageUrl);
record ParsedSearch(IReadOnlyList<JobListing> Jobs, string? Query, int? TotalResults, string ParsedFrom, bool LooksLikeChallenge);
```

Singleton. Two layouts, in order:

**1. LinkedIn public search page.** A card starts at any element with `data-entity-urn="urn:li:jobPosting:<id>"` (a `<div>` or an `<a>`) and ends where the next card starts. Repeated IDs are skipped.

| Field | Element in the card |
| --- | --- |
| JobId | the `urn:li:jobPosting:<id>` value |
| Url | first `href` to `/jobs/view/` whose job ID matches the card |
| Title | `class="base-search-card__title"` |
| Company | `class="base-search-card__subtitle"` |
| Location | `class="job-search-card__location"` |
| ListedAt | `<time class="job-search-card__listdate…" datetime="…">`, attributes in any order |
| ParsedFrom | `LinkedIn public search page markup` |

Page level: `Query` from `class="results-context-header__query-search"`, `TotalResults` from `class="results-context-header__job-count"`. Class names were taken from one saved search page in October 2026.

**2. Generic fallback.** Every `<a href>` to a `/jobs/view/` page whose path ends in a job ID (6 or more digits), resolved against the page address, query removed, unique by ID; the link text is the title. `ParsedFrom` = `job links in page (no LinkedIn result cards found)`.

**Bot-check detection** (`LooksLikeChallenge`), only when no jobs were found: a `<meta http-equiv="refresh">` or LinkedIn's challenge-cookie script (`ccookie`) anywhere in the page, or the words `captcha` or `security verification` in the visible text (not in scripts, which ordinary pages also contain). The page's script is never run.

**Saved pages** (`SavedPageReader`, shared with the job step): when unwrapping a Chrome view-source save, the wrapper's `<!-- saved from url=… -->` note is kept as the first line, so the page's address is still known.

### Storage (`JsonSearchRepository`)

Singleton. Same format as the job files.

- Folder: `Path.GetFullPath(Search:OutputFolder)`, created if missing.
- File: `<SearchId>.json`, invalid file-name characters replaced by `_`. Overwritten if it exists; no previous versions kept.

```json
{
  "searchId": "net-empleos-valencia",
  "sourceUrl": "https://es.linkedin.com/jobs/%2Enet-empleos-valencia",
  "query": ".net Jobs in Valencia",
  "totalResults": 197,
  "count": 100,
  "jobs": [
    {
      "jobId": "4476446382",
      "url": "https://es.linkedin.com/jobs/view/senior-net-developer-at-w-hunt-espa%C3%B1a-4476446382",
      "title": "Senior .NET Developer",
      "company": "W Hunt España",
      "location": "Valencia, Valencian Community, Spain",
      "listedAt": "2026-10-13"
    }
  ],
  "parsedFrom": "LinkedIn public search page markup",
  "retrievedAt": "2026-10-14T14:05:24+00:00"
}
```

### Console output (`SearchConsole`)

```text
Search:    .net Jobs in Valencia
Search ID: net-empleos-valencia
Jobs:      100 in this page (the site reports 197 in total)
(read from LinkedIn public search page markup)

   1. 4476446382  Senior .NET Developer  (W Hunt España · Valencia, Valencian Community, Spain)
   ...
 100. 4474811701  Director/a de proyectos para PROCESOS industriales 4.0  (NUNSYS · Paterna, Valencian Community, Spain)

Only the 100 jobs in this page were read. To include more, scroll further in the browser before saving the page.

Saved to C:\Code\job-fetch-probe-data\output\searches\net-empleos-valencia.json
```

With no argument, the console prints `Using Search:DefaultUrl <url>` first. If no default is set, it prints how to give one and returns 1.

## Inbox step

Reads every saved search page in an inbox folder and stores all their jobs in one batch file. This is the main way to collect search results: save as many result pages as needed (different searches, or the same search scrolled further) into the inbox, and the app merges them.

### Run

1. **List** the files directly in `Inbox:Folder` matching `Inbox:Patterns` (default `*.html`, `*.htm`), oldest first (by last write time, then name). Subfolders, including the archive, are not read. The inbox folder is created if missing.
2. **Read** each file, one by one, with `ISearchService.ReadFileAsync`: same parsing, address and bot-check rules as the search step, no network.
3. **Merge** the jobs: a job already found in an earlier file of the same run is skipped (`JobId`), so the first file wins.
4. If at least one file was read, **write** the batch to `<Inbox:OutputFolder>/<BatchId>.json`, where `BatchId` is the run's local time as `yyyyMMdd_HHmmss`. A second run in the same second gets `_2`, `_3`…; an existing file is never overwritten.
5. Only after the batch file is written, **archive** each file that was read: move it to `Inbox:ArchiveFolder`, together with the `<name>_files` folder the browser saves next to a "complete" page. If the archive already has that name, the moved file becomes `<name>_<yyyyMMdd_HHmmss><ext>` (then `_2`, `_3`…).

Files that could not be read (no address, no jobs, bot check, unreadable) **stay in the inbox** with the reason printed, and are tried again on the next run. If the batch file cannot be written, nothing is archived.

### Models

`JobSearchBatch` (record):

| Property | Type | Rule |
| --- | --- | --- |
| `BatchId` | `string` | **Required.** `yyyyMMdd_HHmmss`, local time |
| `CreatedAt` | `DateTimeOffset` | Local time with offset |
| `Sources` | `IReadOnlyList<BatchSource>` | One per file read |
| `Count` | `int` | Number of jobs in `Jobs` (read-only, written to JSON) |
| `Jobs` | `IReadOnlyList<JobListing>` | Unique by `JobId`, in file order then page order |

`BatchSource`: `record (string File, Uri SourceUrl, string? Query, int? TotalResults, int Jobs, int NewJobs)`. `NewJobs` counts the jobs from that file that were not already in the batch.

`InboxRunResult`: `record (IReadOnlyList<InboxFileResult> Files, JobSearchBatch? Batch, string? SavedTo, string? Error)`.
`InboxFileResult`: `record (string File, bool Success, int Jobs, int NewJobs, string? ArchivedTo, string? Message)`.

### Services and infrastructure

| Type | Project | Role |
| --- | --- | --- |
| `IInboxService` → `InboxService` | Services | The run above. No file access of its own |
| `IInboxFolder` → `InboxFolder` | Interface in Services, implementation in Infrastructure | `ListPending()`, `Archive(path)` |
| `IBatchRepository` → `JsonBatchRepository` | Interface in Services, implementation in Infrastructure | Writes the batch; same JSON format as the other files (camelCase, indented, readable accents) |

### Batch file

```json
{
  "batchId": "20261014_183201",
  "createdAt": "2026-10-14T18:32:01+02:00",
  "sources": [
    { "file": "197 .net jobs in Valencia.html", "sourceUrl": "https://es.linkedin.com/jobs/%2Enet-empleos-valencia",
      "query": ".net Jobs in Valencia", "totalResults": 197, "jobs": 100, "newJobs": 100 },
    { "file": "same search again.html", "sourceUrl": "https://es.linkedin.com/jobs/%2Enet-empleos-valencia",
      "query": ".net Jobs in Valencia", "totalResults": 197, "jobs": 100, "newJobs": 0 }
  ],
  "count": 100,
  "jobs": [
    { "jobId": "4476446382", "url": "https://es.linkedin.com/jobs/view/senior-net-developer-at-w-hunt-espa%C3%B1a-4476446382",
      "title": "Senior .NET Developer", "company": "W Hunt España", "location": "Valencia, Valencian Community, Spain", "listedAt": "2026-10-13" }
  ]
}
```

### Console output (`InboxConsole`)

```text
===== Step 1: inbox =====
[18:32:01] Reading 3 saved page(s) from C:\Code\job-fetch-probe-data\input
[18:32:01] [1/3] 197 .net jobs in Valencia.html: 100 jobs (100 new)
[18:32:01] [2/3] same search again.html: 100 jobs (0 new)
[18:32:01] [3/3] signed-in-view-source.html: kept in the inbox. A view-source save of a signed-in LinkedIn search only contains …
[18:32:01] Listing written: 100 unique jobs -> C:\Code\job-fetch-probe-data\output\listings\20261014_183201.json
[18:32:01] Archived 2 page(s) to C:\Code\job-fetch-probe-data\input\archived
```

At start-up an empty inbox prints nothing. The `inbox` command prints `Inbox …: no saved pages to read.`

## Details step

Reads every job of one inbox listing (`<yyyyMMdd_HHmmss>.json` from `Inbox:OutputFolder`) and stores all their descriptions in one file. It reuses the job step: same typed `HttpClient`, same `JobPageParser`, same bot-check and refusal rules.

### Run

1. **Load** the listing. `details` without an argument takes the newest listing in `Inbox:OutputFolder` (name `yyyyMMdd_HHmmss[_n].json`).
2. **Load** the descriptions file from an earlier run, if any: `<Details:OutputFolder>/<BatchId>.json`, same `BatchId` as the listing. Jobs already in it are skipped and cost no request, so a stopped run **resumes** where it stopped. Jobs that failed earlier are tried again.
3. For each remaining job, in listing order: wait `Details:DelaySeconds` since the previous request, then call `IJobService.ReadUrlAsync(url)` (fetch + parse, no `<JobId>.json` written). The listing's `JobId` is kept on the result.
4. **Rewrite** the descriptions file after every job (temporary file, then moved over the old one), so nothing is lost if the run stops or the app is closed.
5. The run ends when the list is done, the circuit breaker stops it, or the user presses Ctrl+C (stops after the current request; progress is kept).

`IJobService.ReadUrlAsync(Uri, CancellationToken)` is new; `FromUrlAsync` is now `ReadUrlAsync` followed by saving, so the job step behaves as before. `JobResult` has a new `Read` status (read, not saved) and an `HttpStatus` property.

### Circuit breaker (`CircuitBreaker`)

| State | Meaning |
| --- | --- |
| Closed | Normal: one request every `DelaySeconds` |
| Open | No requests at all until the cooldown has passed |
| Half-open | One trial request decides: success or a neutral answer closes the circuit, anything else opens it again |

- A **refusal** (401, 403, 429, 999, or a sign-in / bot-check page: `JobResultStatus.Blocked`) opens the circuit at once.
- A **failure** (no answer, timeout, HTTP 5xx) counts; `FailureThreshold` in a row open the circuit. A success resets the count.
- A **neutral** answer (e.g. 404: that job is gone) only marks that job as failed.
- Each opening waits `CooldownSeconds`, doubling on every further opening, capped at `MaxCooldownSeconds`.
- The trial request after a cooldown is the **same job** that opened the circuit, so no job is skipped.
- When the circuit opens for the `StopAfterOpenings`-th time, the run **stops** at once; the rest stays pending for a later `details` run. Nothing is retried quickly and the identity is never changed.

### Models

`JobDetailsBatch` (record): `BatchId` (required), `ListingFile` (required), `UpdatedAt`, `Listed`, `Count` (read-only), `Pending`, `StoppedReason` (null when the last run reached the end), `Jobs` (`JobPosting`, listing order), `Failures` (`JobFailure(JobId, Url, Status, Message)`).

`DetailsRunResult`: `record (BatchId, SavedTo, Listed, AlreadyDone, Fetched, Failed, Pending, Total, Requests, CircuitOpenings, Elapsed, StoppedReason)`.

`DetailsEvent` (progress, `IProgress<DetailsEvent>`): `Started`, `Job`, `CircuitOpened`, `Waiting` (every 30 s while open), `CircuitHalfOpen`, `CircuitClosed`, `Stopped`. The inbox step reports `InboxProgress` the same way. The console uses a synchronous `IProgress<T>` so lines print in order.

### Descriptions file

```json
{
  "batchId": "20261014_183201",
  "listingFile": "C:\\Code\\job-fetch-probe-data\\output\\listings\\20261014_183201.json",
  "updatedAt": "2026-10-14T18:41:12+02:00",
  "listed": 100,
  "count": 97,
  "pending": 0,
  "stoppedReason": null,
  "jobs": [ { "jobId": "4476446382", "title": "Senior .NET Developer", "company": "W Hunt España", "description": "…", "sourceUrl": "…" } ],
  "failures": [ { "jobId": "4470000000", "url": "…", "status": "Failed", "message": "HTTP 404 Not Found" } ]
}
```

### Console output (`DetailsConsole`, `PipelineConsole`)

Every line has a `[HH:mm:ss]` time; ok is green, warnings yellow, refusals red (no colours when output is redirected).

```text
===== Step 2: job descriptions =====
[18:32:02] Listing 20261014_183201.json: 100 jobs, 0 already stored, 100 to read.
[18:32:02] One request every 5s, about 10m 00s in total if the site keeps answering.
[18:32:02] Circuit breaker: opens on a refusal or 3 failures in a row, waits 5m 00s (doubling, max 30m 00s), stops after 3 opening(s). Ctrl+C stops after the current job.
[18:32:03] [   1/100]   1%  ok       4476446382  Senior .NET Developer · W Hunt España  (ok 1, failed 0, 99 to go, ~9m 54s left)
[18:32:09] [   2/100]   2%  REFUSED  4476446383  (ok 1, failed 1, 98 to go, ~9m 48s left)
[18:32:09] Circuit OPEN (1 of 3): es.linkedin.com refused the request for job 4476446383 (status 429: refusal or sign-in page).
[18:32:09] No requests for 5m 00s, then one trial request. Ctrl+C stops now and keeps the progress.
[18:32:39]   … circuit open, trial request in 4m 30s
[18:37:09] Circuit HALF-OPEN: sending one trial request.
[18:37:10] Circuit CLOSED: the trial request worked, continuing at the normal pace.

===== Summary =====
Inbox pages        3 found, 2 read, 2 archived, 1 kept in the inbox
Jobs listed        100 unique  ->  C:\Code\job-fetch-probe-data\output\listings\20261014_183201.json
Job descriptions   97 of 100 stored  (97 read in this run, 0 from earlier runs)
                   3 failed, 0 not tried yet
Requests           101 sent in 14m 12s, circuit breaker opened 1 time(s)
Descriptions file  C:\Code\job-fetch-probe-data\output\descriptions\20261014_183201.json
```

When the run stops early, the summary adds `Stopped early <reason>`; when every job was read, `Result complete: every listed job has its description`.

## Storage

`Storage:Provider` decides where the steps store what they read. The services only see the interfaces in `Services/Abstractions`, so they work the same with both.

| Interface | `Json` | `Sqlite` |
| --- | --- | --- |
| `IJobRepository` | `<Output:Folder>/<JobId>.json` | row in `Jobs` (+ `JobDetails`), overwritten on re-read |
| `ISearchRepository` | `<Search:OutputFolder>/<SearchId>.json` | row in `Searches` + its cards in `SearchJobs`, replaced on re-read |
| `IBatchRepository` | `<Inbox:OutputFolder>/<BatchId>.json` | row in `Listings` + `ListingSources` + `ListingJobs`; never overwritten (`_2`, `_3`…) |
| `IListingStore` | reads those files | reads those rows |
| `IDetailsRepository` | `<Details:OutputFolder>/<BatchId>.json` | the description goes into the job's row in `Jobs`; the listing row keeps pending count and stop reason |

The value each `SaveAsync` returns, printed as "Saved to …" or in the summary, is the file path with `Json` and `<database path> (job|search|listing <id>)` with `Sqlite`.

`IListingStore.Resolve(text)` finds a listing from what the user typed: with `Json`, an existing file path or a batch ID / file name in `Inbox:OutputFolder`; with `Sqlite`, any text containing a batch ID (`yyyyMMdd_HHmmss[_n]`). `details` uses it; the pipeline passes it the listing location the inbox step returned.

### Database (`Sqlite`)

One SQLite file at `Storage:Sqlite:Path` (`<DataRoot>\database\jobs.db`), through EF Core 10 (`Microsoft.EntityFrameworkCore.Sqlite`). At every start, `InitializeStorageAsync` creates the folder and the file if missing and applies any pending migrations; the first console line says which storage is in use.

```text
Jobs            JobId PK · Url · Title · Company · Location · Posted · EmploymentType · Description · ParsedFrom
                RetrievedAt · Status (Pending | Read | Blocked | Failed) · LastError · LastAttemptAt · FirstSeenAt
  JobDetails    Id PK · JobId FK (cascade) · Position · Label · Value
Listings        BatchId PK · CreatedAt · DetailsUpdatedAt · DetailsPending · DetailsStoppedReason
  ListingSources  Id PK · BatchId FK (cascade) · Position · File · SourceUrl · Query · TotalResults · Jobs · NewJobs
  ListingJobs     (BatchId, JobId) PK · Position · Url · Title · Company · Location · ListedAt   JobId FK → Jobs
Searches        SearchId PK · SourceUrl · Query · TotalResults · ParsedFrom · RetrievedAt
  SearchJobs      (SearchId, JobId) PK · Position · Url · Title · Company · Location · ListedAt  JobId FK → Jobs
```

Rules:

- **One row per job.** A job listed in several listings or searches has one `Jobs` row; `ListingJobs` and `SearchJobs` keep what each card showed. A new card creates the job row as `Pending`; a card for a job not read yet updates its title, company and location; a job already `Read` keeps its page data.
- **No job is read twice.** `SqliteDetailsRepository.LoadAsync` returns every listed job whose row is `Read`, whichever listing, search or job step stored it, so the details step skips them (they count as "already stored").
- **Progress after every job.** `SaveAsync` writes only the jobs read since the last call (same `RetrievedAt` → untouched) and sets `Blocked` / `Failed` with the message on failures. A stored description is never replaced by a later failure.
- **Transactions.** Saving a listing or a search is one transaction; a failure leaves nothing half-written, and the inbox then archives nothing.
- **Contexts.** Each call opens a context from `IDbContextFactory<JobsDbContext>` and disposes it at the end; nothing holds a connection between calls.
- EF Core's own log lines are filtered out (`Microsoft.EntityFrameworkCore` → `None`); a database failure prints `Database error: …` and exits with 3.

Migrations live in `Infrastructure/Database/Migrations`. `DesignTimeJobsDbContextFactory` lets the EF tools build the model without starting the app. After changing the rows or `JobsDbContext`:

```sh
dotnet ef migrations add <Name> --project src/JobFetchProbe.Infrastructure --startup-project src/JobFetchProbe.Console --output-dir Database/Migrations
```

The next start applies it. Existing JSON files are not imported.

## Acceptance

The build is acceptable when:

1. `dotnet build` succeeds for the solution.
2. The saved page for job 4478111194 produces `output/jobs/4478111194.json` (Json storage) with title, company, location, posted, four details, a description and the canonical `sourceUrl`.
3. A page with schema.org `JobPosting` data is read from the JSON-LD layout.
4. A link that is refused or redirected to a sign-in page prints a blocked message, saves nothing, and is not retried.
5. A saved page without a canonical link or `og:url` is rejected.
6. An empty `Output:Folder` stops the app with exit code 2.
7. The code contains no cookie handling, proxy settings, retries, or browser automation.
8. `search` on the saved page "197 .net jobs in Valencia" produces `output/searches/net-empleos-valencia.json` (Json storage) with 100 unique jobs, each with a clean `/jobs/view/` URL ending in its job ID, title, company, location and `listedAt`, plus `totalResults` 197.
9. `search` on LinkedIn's bot-check page (meta refresh and challenge-cookie script) reports a bot check, saves nothing, and does not run the page's script.
10. `search` with no argument uses `Search:DefaultUrl`.
11. The job step behaves exactly as before for any input that does not start with `search` or equal `inbox`.
12. With the 197-jobs page, a copy of it and a page with only a "saved from" address in the inbox, one run writes one `yyyyMMdd_HHmmss.json` with 102 unique jobs and three sources, and moves those three pages (and the `_files` folder) to the archive.
13. A page without any address, a signed-in view-source page (with or without a link to its selected job) and a bot-check page stay in the inbox with their reasons; a run with only such pages writes no batch and returns 1.
14. A page whose name already exists in the archive is archived as `<name>_<yyyyMMdd_HHmmss><ext>`; nothing is overwritten.
15. After the inbox writes a listing, the details step reads its jobs one at a time with the configured pause and writes one `<BatchId>.json` in `Details:OutputFolder`, rewritten after each job; the summary prints the totals.
16. One refusal opens the circuit: no request is sent during the cooldown, then the same job is tried once; on success the run continues.
17. `FailureThreshold` server errors in a row open the circuit; a 404 does not.
18. At the `StopAfterOpenings`-th opening the run stops, the jobs not tried stay pending, and a later `details` run skips the stored jobs and continues.
19. With `Storage:Provider` = `Sqlite`, the first start creates `jobs.db` and applies the initial migration; a run of the whole process writes no JSON files.
20. With `Sqlite`, after a run stopped by refusals, the refused job is `Blocked` with its message and the jobs not tried are `Pending`; `details` then reads only those.
21. With `Sqlite`, a second listing of jobs already read sends no requests and reports them as already stored.
22. `Storage:Provider` = `Postgres` (or any other value) exits with 2; a file that is not a database exits with 3.
23. With `Json`, all earlier acceptance points still hold.
24. With only `Paths:DataRoot` set, the first start creates `input`, `input/archived` and `pages` under it, the database goes to `database/jobs.db`, and JSON output to `output/…`; nothing is written inside the repository.
25. A folder set on its own (e.g. `Inbox:Folder`) is used instead of the derived one, while the others still follow `Paths:DataRoot`.

## Not in this build

A server database (PostgreSQL in Docker is a possible later step: the EF Core model would stay, with another provider and a new migration), importing existing JSON files into the database, requesting further search result pages (pagination, the `/jobs-guest/` endpoints), running or solving bot-check scripts, logins, cookies, browser automation, retries with a different identity, scheduled runs, a database, applying to jobs, and storage of recruiter or contact details.
