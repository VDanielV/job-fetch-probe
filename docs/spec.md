# Spec: JobFetchProbe

.NET 10 console app that runs one flow and exits:

1. **Step 1, saved search pages → job links.** Reads every search results page saved into the input folder, stores the job links they contain in a SQLite database, and moves each page read to the archive folder.
2. **Step 2, job links → job descriptions.** Reads every job in the database without a description, one at a time, politely and behind a circuit breaker, and stores each description, its details and the job's status.
3. **Summary** with the totals.

There are no commands, options or arguments. A separate, read-only web app (`JobFetchProbe.Web`) shows the stored jobs. This spec describes the current code. It follows [`plan.md`](plan.md). The original probe spec is kept in Git history under the tag `probe-v1`.

## Solution layout

```text
job-fetch-probe/
  JobFetchProbe.sln
  Directory.Build.props            net10.0, nullable, implicit usings
  nuget.config                     nuget.org only
  src/
    JobFetchProbe.Models/          one type per file, grouped by step; namespace = folder
      Jobs/      JobPosting.cs  JobDetail.cs  FetchedPage.cs  JobResult.cs  JobResultStatus.cs
      Search/    JobListing.cs  JobSearch.cs  SearchResult.cs
      Inbox/     JobSearchBatch.cs  BatchSource.cs  InboxRunResult.cs  InboxFileResult.cs  InboxProgress.cs
      Details/   DetailsRunResult.cs  DetailsEvent.cs  DetailsEventKind.cs
      Catalog/   JobSummary.cs  JobSummaryPage.cs  StoredJob.cs                          (web app)
    JobFetchProbe.Services/
      DependencyInjection.cs  IJobService.cs  JobService.cs
      Abstractions/  IPageFetcher.cs  ISavedPageReader.cs  IInboxFolder.cs  IBatchRepository.cs  IJobRepository.cs
                     IJobCatalog.cs                                                    (web app)
      Parsing/       IJobPageParser.cs  JobPageParser.cs  JobIdResolver.cs
      Search/        ISearchService.cs  SearchService.cs  ISearchPageParser.cs  SearchPageParser.cs  ParsedSearch.cs
      Inbox/         IInboxService.cs  InboxService.cs                                       (step 1)
      Details/       IDetailsService.cs  DetailsService.cs                                   (step 2)
                     CircuitBreaker.cs  CircuitBreakerOptions.cs  CircuitState.cs
    JobFetchProbe.Infrastructure/
      DependencyInjection.cs   AddInfrastructure (console) · AddJobCatalog (web)
      Configuration/  DataPaths.cs
      Http/      HttpPageFetcher.cs  HttpClientSettings.cs
      Files/     SavedPageReader.cs
      Inbox/     InboxFolder.cs  InboxSettings.cs
      Details/   DetailsSettings.cs  CircuitBreakerSettings.cs
      Database/  JobsDbContext.cs  DatabaseSettings.cs  DatabaseInitializer.cs  JobRowMapping.cs
                 DesignTimeJobsDbContextFactory.cs
        Entities/        JobRow.cs  JobDetailRow.cs  JobStatus.cs  ListingRow.cs  ListingSourceRow.cs  ListingJobRow.cs
        Configurations/  <Row>Configuration.cs, one IEntityTypeConfiguration per table
        Repositories/    SqliteBatchRepository.cs  SqliteJobRepository.cs  SqliteJobCatalog.cs
        Migrations/      InitialCreate, RemoveSearchesAndListingProgress, JobsDbContextModelSnapshot.cs
    JobFetchProbe.Console/
      Program.cs  PipelineConsole.cs  InboxConsole.cs  DetailsConsole.cs
      ConsoleLog.cs  SyncProgress.cs  appsettings.json
    JobFetchProbe.Web/
      Program.cs  appsettings.json  Properties/launchSettings.json
      Pages/     Index.cshtml(.cs)  Job.cshtml(.cs)  _ViewImports.cshtml  _ViewStart.cshtml  Shared/_Layout.cshtml
  docs/      plan.md  spec.md
```

The repository holds code and documents only. Everything the app reads or writes is under `Paths:DataRoot`, a folder outside the repository, so Git commands (`git clean -fdx`, a fresh clone) never touch it:

```text
job-fetch-probe-data/              Paths:DataRoot
  database/jobs.db                 Database:Path
  input/                           Inbox:Folder: saved search pages to read
    archived/                      Inbox:ArchiveFolder: pages already read
```

`DataPaths` (Infrastructure/Configuration, used by both apps) adds these three paths as an in-memory configuration source **underneath** all others (index 0), computed from the final value of `Paths:DataRoot`. A path set in `appsettings.json` or an environment variable therefore still wins on its own. Without `Paths:DataRoot`, each setting is used as given (or its code default). At start-up the app creates `input` and `input/archived` and prints `Data folder: …`.

Conventions:

- **One type per file**, the file named after the type; namespaces follow folders.
- **Models are not database rows.** `JobFetchProbe.Models` holds the shapes every layer shares (immutable records). The tables have their own classes in `Infrastructure/Database/Entities` (mutable, with keys, positions and status columns, suffix `Row`), used only by the database code and copied to and from the models in `JobRowMapping`.

## Dependencies

```text
Console ──► Services ──► Models
   │            ▲
   └──► Infrastructure (implements the Services interfaces) ◄── Web
```

| Project | References | Packages |
| --- | --- | --- |
| Models | none | none |
| Services | Models | `Microsoft.Extensions.DependencyInjection.Abstractions` 10.0.0 |
| Infrastructure | Services | `Microsoft.Extensions.Configuration` 10.0.0, `Microsoft.Extensions.Http` 10.0.0, `Microsoft.Extensions.Options.ConfigurationExtensions` 10.0.0, `Microsoft.EntityFrameworkCore.Sqlite` 10.0.13 |
| Console | Services, Infrastructure | `Microsoft.Extensions.Hosting` 10.0.0, `Microsoft.EntityFrameworkCore.Design` 10.0.13 (design time only, `PrivateAssets=all`) |
| Web | Infrastructure | none (`Microsoft.NET.Sdk.Web`: ASP.NET Core shared framework) |

Services never use `HttpClient`, files or the database directly; everything outside goes through `Services/Abstractions`.

## Run

`dotnet run --project src/JobFetchProbe.Console`, or the built `job-fetch-probe` executable. Arguments are ignored.

1. Build the host (content root = build output folder, so `appsettings.json` is always found), apply `DataPaths`, register services and infrastructure.
2. Create the data folders; print `Data folder: …`.
3. `InitializeDatabaseAsync`: create the database folder and file if missing, apply pending migrations; print `Database: …` (with `schema updated, N migration(s) applied` when it did).
4. `PipelineConsole.RunAsync`: step 1, then step 2 (always, whatever step 1 found), then the summary.

| Exit code | Meaning |
| --- | --- |
| 0 | Nothing left to do: no page kept in the input folder, and no job left to read (every job `Read` or `Gone`) |
| 1 | Something is left for the next run: a page kept in the input folder, or jobs still `Pending`, `Blocked` or `Failed` |
| 2 | Configuration error (`OptionsValidationException`) |
| 3 | Database error: the file is locked, read-only, damaged or not a database (`SqliteException`, `DbUpdateException`) |

## Step 1: saved search pages → job links (`InboxService`)

1. **List** the files directly in `Inbox:Folder` matching `Inbox:Patterns` (`*.html`, `*.htm`), oldest first (last write time, then name). Subfolders, including the archive, are not read.
2. **Read** each file with `ISearchService.ReadFileAsync` (no network):
   - The page's address is its canonical link or `og:url`, else the browser's `<!-- saved from url=… -->` note. A page without any is rejected (or reported as a bot check if it is one).
   - A page with no result cards of a known layout whose only job is the one selected in its address (`currentJobId`) is rejected: it holds no list. This is what a view-source save of LinkedIn's signed-in pages looks like (the list is loaded later by scripts); the message also mentions a possible layout change.
   - A page with no job links is rejected; if it is a bot-check page, that is the reason given.
3. **Merge** the jobs: a job already found in an earlier page of the same run is skipped (`JobId`), so the first page wins.
4. If at least one page was read, **store** the listing with `IBatchRepository.SaveAsync`, in one transaction: a `Listings` row (`BatchId` = local time `yyyyMMdd_HHmmss`, `_2`, `_3`… if taken), one `ListingSources` row per page, one `ListingJobs` row per job (as its card showed it), and a `Pending` row in `Jobs` for every job not seen before. A job not read yet gets its title, company, location and URL updated from the card; a job already `Read` keeps its page data.
5. Only after that, **archive** each page read: move it to `Inbox:ArchiveFolder`, together with the `<name>_files` folder the browser saves next to a "complete" page. If the archive already has that name, the moved file becomes `<name>_<yyyyMMdd_HHmmss><ext>` (then `_2`, `_3`…); nothing is overwritten.

Pages that could not be read **stay in the input folder** with the reason printed, and are tried again on the next run. If the database cannot be written, the run ends with exit code 3 and nothing is archived.

### Search page parsing (`ISearchPageParser`, `SearchPageParser`)

```csharp
ParsedSearch Parse(string html, Uri pageUrl);
record ParsedSearch(IReadOnlyList<JobListing> Jobs, string? Query, int? TotalResults, string ParsedFrom, bool LooksLikeChallenge);
```

Singleton. Three layouts, in order; the first that finds jobs wins:

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

Page level: `Query` from `class="results-context-header__query-search"` (else the address's `keywords` value, else the page title), `TotalResults` from `class="results-context-header__job-count"`. Class names were taken from one saved search page in October 2026.

**2. LinkedIn signed-in search page** (`/jobs/search-results/`, saved with "Save page as → Webpage, Complete"). Its class names are generated (`hv4k0m …`) and change, so they are not used. A card starts at `componentkey="job-card-component-ref-<id>"` (the key is on two nested elements; repeats are skipped) and ends where the next card starts (the last card: at most 20,000 characters). Repeated IDs are skipped.

| Field | Source in the card |
| --- | --- |
| JobId | the `job-card-component-ref-<id>` value |
| Url | a `/jobs/view/` link in the card whose ID matches, else `https://www.linkedin.com/jobs/view/<id>/` (the cards link back to the search, not to the job) |
| Title | first `<p>`: it holds the title twice, first for screen readers with a label such as "(Empleo verificado)", then the visible title; the visible one is used |
| Company | second `<p>` |
| Location | third `<p>` (later ones, such as "Visto" or a salary, are ignored) |
| ListedAt | not shown on these cards (null) |
| ParsedFrom | `LinkedIn signed-in search page markup` |

Page level: `Query` from the address's `keywords` value, `TotalResults` from a paragraph reading `<n> resultados` / `<n> results`. Taken from one saved page in October 2026.

**3. Generic fallback.** Every `<a href>` to a `/jobs/view/` page whose path ends in a job ID (6 or more digits), resolved against the page address, query removed, unique by ID; the link text is the title. `ParsedFrom` = `job links in page (no LinkedIn result cards found)`.

**Bot-check detection** (`LooksLikeChallenge`), only when no jobs were found: a `<meta http-equiv="refresh">` or LinkedIn's challenge-cookie script (`ccookie`) anywhere in the page, or the words `captcha` or `security verification` in the visible text (not in scripts, which ordinary pages also contain). The page's script is never run.

## Step 2: job links → job descriptions (`DetailsService`)

1. **Work list:** `IJobRepository.GetUnreadAsync`: every job whose status is `Pending`, `Failed` or `Blocked`. Never-tried (`Pending`) jobs first, then by `FirstSeenAt`, then `JobId` (sorted in memory: SQLite cannot order by `DateTimeOffset`).
2. For each job, in that order: wait `Details:DelaySeconds` since the previous request, then `IJobService.ReadUrlAsync(url)` (fetch + parse; the job keeps the listed `JobId`).
3. **Store the outcome at once**, before the next job:
   - read → `SaveDescriptionAsync`: page data, details and description stored, status `Read`, last error cleared (an earlier read is overwritten);
   - HTTP 404 or 410 → `SaveFailureAsync(gone: true)`: status `Gone`, not tried again;
   - refused → status `Blocked`; any other failure → status `Failed`; both with the message and time, tried again on the next run.
   A stored description is never replaced by a later failure. Saving is never cancelled half-way.
4. The run ends when the list is done, the circuit breaker stops it, or the user presses Ctrl+C (stops after the current request). Jobs not tried stay as they were and are first in line next time.

### Status of a job (`Jobs.Status`)

| Status | Set when | Tried by step 2 |
| --- | --- | --- |
| `Pending` | first listed by step 1 | yes, first |
| `Read` | the page was read and stored | no |
| `Blocked` | the site refused (401, 403, 429, 999, sign-in page) | yes, next run |
| `Failed` | network error, timeout, 5xx, other non-success status | yes, next run |
| `Gone` | HTTP 404 or 410 | no |

### Circuit breaker (`CircuitBreaker`)

| State | Meaning |
| --- | --- |
| Closed | Normal: one request every `DelaySeconds` |
| Open | No requests at all until the cooldown has passed |
| Half-open | One trial request decides: success or a neutral answer closes the circuit, anything else opens it again |

- A **refusal** opens the circuit at once.
- A **failure** (no answer, timeout, HTTP 5xx) counts; `FailureThreshold` in a row open the circuit. A success resets the count.
- A **neutral** answer (e.g. 404) only affects that job.
- Each opening waits `CooldownSeconds`, doubling on every further opening, capped at `MaxCooldownSeconds`. While waiting, the time left is reported every 30 s, in whole seconds rounded up (so the loop never wakes before the cooldown ends).
- The trial request after a cooldown is the **same job** that opened the circuit, so no job is skipped.
- When the circuit opens for the `StopAfterOpenings`-th time, the run **stops**. Nothing is retried quickly and the identity is never changed.

### Reading a job page (`JobService`)

`ReadUrlAsync(Uri)`: only `http`/`https`. A refusal or sign-in redirect → `Blocked`; another error → `Failed` with `could not fetch the page: …`; otherwise the page is parsed. Every result carries the HTTP status. The job ID comes from the URL, else the page's canonical link, else `job-yyyyMMdd-HHmmss`.

### Job ID (`JobIdResolver`)

| Source | Pattern | Example |
| --- | --- | --- |
| URL path | `/jobs/view/(?:[^/]*?-)?(\d{6,})/?$` | `/jobs/view/4478111194/`, `/jobs/view/tecnico-…-sl-4478111194` |
| Page | canonical link, else `og:url`, then the URL rule | `<link rel="canonical" href="…-4478111194">` |
| Fallback | `job-yyyyMMdd-HHmmss` (UTC) | `job-20261014-133911` |

### Job page parsing (`IJobPageParser`, `JobPageParser`)

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

## Web app (`JobFetchProbe.Web`)

ASP.NET Core Razor Pages, read-only. `dotnet run --project src/JobFetchProbe.Web` → <http://localhost:5080> (`Properties/launchSettings.json`).

- **Configuration:** its own `appsettings.json` with the same `Paths:DataRoot`; `DataPaths.AddDataRootDefaults` derives `Database:Path` exactly as for the console app. EF Core logs at `Warning` and above.
- **No writes, no migrations:** it only reads through `IJobCatalog`. If the database file does not exist, the table page says so and nothing is created. It can run while the console app writes (SQLite handles one writer and readers).
- **`/`** (`Pages/Index`): `IJobCatalog.GetPageAsync(p, 25)`. Jobs are ordered by job ID, longest then highest first (LinkedIn IDs grow over time, so newest first; the database cannot sort by the stored dates). Paging (`Skip`/`Take`) runs in the database; `?p=` out of range is clamped to the first or last page. Columns: Title (link), Company, Location, Posted, Status. Pager: First, Previous, Next, Last, and "<n> jobs · page x of y".
- **`/jobs/{id}`** (`Pages/Job`): `IJobCatalog.GetAsync(id)`, 404 if unknown. Shows title, company · location, status, a link to the job page (`Url`), Job ID, Posted, Employment type, every stored detail, First seen, Description read, and Last attempt / Last error when there is one; then the description (line breaks kept), or a note that it is not read yet. `?p=` is kept so "← All jobs" returns to the same table page.
- **Styles:** a few CSS rules inline in `Pages/Shared/_Layout.cshtml`, light and dark (`color-scheme`); no scripts, no external files.

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

Singleton. Reads the file as text. If it is a Chrome view-source save (contains `line-gutter-backdrop` and `<td class="line-content">`), the original HTML is rebuilt from the `line-content` cells: tags removed, entities decoded, one line per cell. The wrapper's `<!-- saved from url=… -->` note is kept as the first line, so the page's address is still known.

### Database (`JobsDbContext`)

One SQLite file at `Database:Path` (`<DataRoot>\database\jobs.db`), through EF Core 10. Each table is configured in its own `IEntityTypeConfiguration<T>` class (`Database/Configurations`); `OnModelCreating` only calls `ApplyConfigurationsFromAssembly`.

```text
Jobs            JobId PK · Url · Title · Company · Location · Posted · EmploymentType · Description · ParsedFrom
                RetrievedAt · Status · LastError · LastAttemptAt · FirstSeenAt
  JobDetails    Id PK · JobId FK (cascade) · Position · Label · Value
Listings        BatchId PK · CreatedAt
  ListingSources  Id PK · BatchId FK (cascade) · Position · File · SourceUrl · Query · TotalResults · Jobs · NewJobs
  ListingJobs     (BatchId, JobId) PK · Position · Url · Title · Company · Location · ListedAt   JobId FK → Jobs (restrict)
```

- **One row per job**, however many listings show it; `ListingJobs` keeps what each card showed.
- **Contexts:** `IDbContextFactory<JobsDbContext>` (singleton factory); every repository call opens and disposes its own context, so nothing holds a connection between calls.
- **Repositories:** `SqliteBatchRepository` (`IBatchRepository`, step 1) and `SqliteJobRepository` (`IJobRepository`, step 2).
- **Migrations** (`Database/Migrations`), applied at start-up: `InitialCreate`; `RemoveSearchesAndListingProgress` (drops the tables and columns of the removed search command and listing-based details step). `DesignTimeJobsDbContextFactory` lets the EF tools build the model without starting the app. After changing the rows or their configuration:

  ```sh
  dotnet ef migrations add <Name> --project src/JobFetchProbe.Infrastructure --startup-project src/JobFetchProbe.Console --output-dir Database/Migrations
  ```

- EF Core's own log lines are filtered out (`Microsoft.EntityFrameworkCore` → `None`); a database failure prints `Database error: …` and exits with 3.

## Configuration

`src/JobFetchProbe.Console/appsettings.json`, copied to the build output. Environment variables override settings (`Paths__DataRoot=D:\job-data`). Command-line arguments are not used.

| Key | Default | Validation |
| --- | --- | --- |
| `Paths:DataRoot` | `C:\Code\job-fetcher\job-fetch-probe-data` | none; when set, the keys marked † default to the layout above |
| `Database:Path` † | `<DataRoot>\database\jobs.db` | not empty |
| `Inbox:Folder` † | `<DataRoot>\input` | not empty |
| `Inbox:ArchiveFolder` † | `<DataRoot>\input\archived` | not empty, different from `Inbox:Folder` |
| `Inbox:Patterns` | `*.html`, `*.htm` (code default) | at least one |
| `Details:DelaySeconds` | `5` | 1 to 120 |
| `Details:CircuitBreaker:FailureThreshold` | `3` | 1 to 20 |
| `Details:CircuitBreaker:CooldownSeconds` | `300` | 1 to 14400 |
| `Details:CircuitBreaker:MaxCooldownSeconds` | `1800` | 1 to 86400 |
| `Details:CircuitBreaker:StopAfterOpenings` | `3` | 1 to 10 |
| `HttpClient:TimeoutSeconds` | `20` | 1 to 120 |
| `HttpClient:UserAgent` | `JobFetchProbe/0.3 (personal study project)` | not empty |
| `HttpClient:HandlerLifetimeMinutes` | `5` | none |
| `Logging:LogLevel:Default` | `Warning` | none; keeps HttpClient request logs off the console |

Validation runs when the settings are first used. A failure prints `Configuration error: …` and exits with 2.

## Dependency injection

Console: `AddServices()` (Services) and `AddInfrastructure(IConfiguration)` (Infrastructure). Web: `AddJobCatalog(IConfiguration)` only (database settings, context factory, `IJobCatalog` → `SqliteJobCatalog`); both share the private `AddDatabase`. The console classes are singletons; `InboxConsole` and `DetailsConsole` create one DI scope per step.

| Registration | Lifetime |
| --- | --- |
| `TimeProvider.System` | singleton |
| `IJobPageParser`, `ISearchPageParser` | singleton |
| `IJobService`, `ISearchService`, `IInboxService`, `IDetailsService` | transient (they depend, directly or not, on the typed `HttpClient`, which must not be pinned by a singleton) |
| `IPageFetcher` → `HttpPageFetcher` | typed client (factory-managed handler) |
| `ISavedPageReader`, `IInboxFolder` | singleton |
| `IDbContextFactory<JobsDbContext>` | singleton factory; short-lived contexts |
| `IBatchRepository` → `SqliteBatchRepository`, `IJobRepository` → `SqliteJobRepository` | singleton |
| `PipelineConsole`, `InboxConsole`, `DetailsConsole` | singleton |

## Console output

Every line has a `[HH:mm:ss]` time; ok is green, warnings yellow, refusals red (no colours when output is redirected).

```text
[18:32:01] Data folder: C:\Code\job-fetcher\job-fetch-probe-data
[18:32:01] Database: C:\Code\job-fetcher\job-fetch-probe-data\database\jobs.db

===== Step 1: saved search pages → job links =====
[18:32:01] [1/2] 197 .net jobs in Valencia.html: 100 jobs (100 new in this run)
[18:32:01] [2/2] view-source_….html: kept in the input folder. the page only contains the selected job …
[18:32:01] Stored 100 unique job links as listing 20261014_183201.
[18:32:01] Archived 1 page(s) to C:\Code\job-fetcher\job-fetch-probe-data\input\archived

===== Step 2: job links → job descriptions =====
[18:32:02] 100 job(s) without a description in the database.
[18:32:02] One request every 5s, about 10m 00s in total if the site keeps answering.
[18:32:02] Circuit breaker: opens on a refusal or 3 failures in a row, waits 5m 00s (doubling, max 30m 00s), stops after 3 opening(s). Ctrl+C stops after the current job.
[18:32:03] [   1/100]   1%  ok       4476446382  Senior .NET Developer · W Hunt España  (ok 1, failed 0, 99 to go, ~9m 54s left)
[18:32:09] [   2/100]   2%  REFUSED  4476446383  (ok 1, failed 1, 98 to go, ~9m 48s left)
[18:32:09] Circuit OPEN (1 of 3): es.linkedin.com refused the request for job 4476446383 (status 429: refusal or sign-in page).
[18:32:09] No requests for 5m 00s, then one trial request. Ctrl+C stops now and keeps the progress.
[18:32:39]   … circuit open, trial request in 4m 30s
[18:37:09] Circuit HALF-OPEN: sending one trial request.
[18:37:10] Circuit CLOSED: the trial request worked, continuing at the normal pace.
[18:37:16] [   4/100]   4%  gone     4476446385  the job page no longer exists; not tried again  (…)

===== Summary =====
Input pages      2 found, 1 read and archived, 1 kept in the input folder
Job links        100 stored as listing 20261014_183201
Descriptions     97 read in this run, 3 failed, 0 not tried
                 97 stored in total; 2 still to read (tried again on the next run)
Requests         101 sent in 14m 12s, circuit breaker opened 1 time(s)
Database         C:\Code\job-fetcher\job-fetch-probe-data\database\jobs.db
```

An empty input folder prints `No saved pages in …. Nothing new to add.`; a database where every job is read prints `Every job in the database already has its description. Nothing to read.` When the run stops early, the summary adds `Stopped early <reason>`; when nothing is left, `Result complete: every job has its description`.

## Acceptance

The build is acceptable when:

1. `dotnet build` succeeds for the solution, and `dotnet ef migrations has-pending-model-changes` reports no changes.
2. The first start with an empty data folder creates `input`, `input/archived` and `database/jobs.db` (all migrations applied).
3. With the saved page "197 .net jobs in Valencia" in `input`, step 1 stores 100 unique jobs as `Pending` with title, company, location, a clean `/jobs/view/` URL, and the card's `ListedAt`; the page (and its `_files` folder) moves to `input/archived`.
4. A signed-in search saved as a complete page gives its jobs (the saved example: 3 jobs with title, company, location and a `/jobs/view/<id>/` address; query "Senior .NET Software Engineer"; 3 results).
5. A page without an address, a signed-in view-source save, a page without job links and a bot-check page stay in `input` with their reasons; the run exits with 1.
6. Step 2 reads the jobs one at a time with the configured pause, stores each result at once, and sets `Read`, `Blocked`, `Failed` or `Gone`.
7. One refusal opens the circuit: no request during the cooldown, then the same job is tried once; on success the run continues. `FailureThreshold` server errors in a row open it too; a 404 does not, and marks the job `Gone`.
8. At the `StopAfterOpenings`-th opening the run stops; the next start (with an empty `input`) reads the jobs still `Pending`, then `Blocked`/`Failed`, and nothing else.
9. Saving the same search page again creates a new listing but sends no requests: its jobs are already `Read`.
10. A database made by an earlier version is upgraded at start-up and keeps its jobs.
11. A bad setting exits with 2; a file that is not a database exits with 3.
12. The web app lists every job, 25 per page, newest IDs first; clicking one shows its details and description; an unknown ID gives 404; without a database it shows a message and creates nothing.
13. The code contains no cookie handling, proxy settings, quick retries, or browser automation.

## Not in this build

Commands or arguments (the earlier prompt, single-job, search-by-link and `details` commands were removed), JSON-file storage, requesting search result pages (pagination, the `/jobs-guest/` endpoints), running or solving bot-check scripts, logins, cookies, browser automation, retries with a different identity, scheduled runs, a server database (PostgreSQL in Docker is a possible later step: the EF Core model would stay, with another provider and a new migration), applying to jobs, and storage of recruiter or contact details.
