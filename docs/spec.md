# Spec: JobFetchProbe

.NET 10 console app that runs one flow and exits:

1. **Step 1, saved search pages → job links.** Reads every search results page saved into the input folder, stores the job links they contain in a SQLite database, and moves each page read to the archive folder.
2. **Step 2, job links → job descriptions.** Reads every job in the database without a description, one at a time, politely and behind a circuit breaker, and stores each description, its details and the job's status.
3. **Summary** with the totals.

There are no commands, options or arguments. A separate, read-only web app (`JobFetchProbe.Web`) shows the stored jobs, and a REST API (`JobFetchProbe.Api`) returns them as JSON and can start the same acquisition in the background (`POST api/JobFetcher`). This spec describes the current code. It follows [`plan.md`](plan.md). The original probe spec is kept in Git history under the tag `probe-v1`.

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
      FetchJobListing/  JobSearchBatch.cs  BatchSource.cs  JobListingFetchResult.cs  JobListingFileResult.cs  JobListingFetchProgress.cs   (step 1)
      FetchJobDetails/  JobDetailsFetchResult.cs  JobDetailsFetchEvent.cs  JobDetailsFetchEventKind.cs                         (step 2)
      Acquisition/      AcquisitionResult.cs  AcquisitionStart.cs  AcquisitionStartState.cs                                 (both steps)
      Catalog/   JobSummary.cs  JobSummaryPage.cs  StoredJob.cs                          (web app, api)
      Journal/   JournalStep.cs  JournalRunState.cs  JournalStarter.cs  JournalCircuitState.cs  JournalRunSummary.cs
                 JobListingJournalRun.cs  JobListingJournalEntry.cs  JobListingJournalOutcome.cs
                 JobDetailsJournalRun.cs  JobDetailsJournalEntry.cs  JobDetailsJournalEntryKind.cs
    JobFetchProbe.Services/
      DependencyInjection.cs
      Abstractions/  IPageFetcher.cs  ISavedPageReader.cs  IInputFolder.cs  IBatchRepository.cs  IJobRepository.cs
                     IJobCatalog.cs                                                    (web app, api)
      Abstractions/Journal/  IJournalRepository.cs                                      (journal)
      Catalog/       IJobRetriever.cs  JobRetriever.cs  JobStoreUnavailableException.cs   (api)
      Journal/       IJournalService.cs  JournalService.cs  JournalOptions.cs  JournalUnavailableException.cs
                     IJobListingRunRecorder.cs  JobListingRunRecorder.cs  IJobDetailsRunRecorder.cs  JobDetailsRunRecorder.cs
                     RunRecorderCore.cs  JournalQueue.cs  SignOfLife.cs
      Parsing/          JobIdResolver.cs  HtmlText.cs                                       (shared by both steps)
                        HtmlQuery.cs  UrlHelper.cs  JsonLd.cs                               (helpers)
      FetchJobListing/  IJobListingFetcher.cs  JobListingFetcher.cs                            (step 1)
                        ISearchService.cs  SearchService.cs  ISearchPageParser.cs  SearchPageParser.cs  ParsedSearch.cs
      FetchJobDetails/  IJobDetailsFetcher.cs  JobDetailsFetcher.cs                            (step 2)
                        FetchJobDetailsSettings.cs  CircuitBreakerSettings.cs  (step 2's rules)
                        JobDetailsFetchState.cs  FetchOutcome.cs  TimeLeftEstimate.cs  (helpers)
                        CircuitBreaker.cs  CircuitBreakerOptions.cs  CircuitState.cs
                        IJobService.cs  JobService.cs  IJobPageParser.cs  JobPageParser.cs
      Acquisition/      IAcquisitionService.cs  AcquisitionService.cs  IAcquisitionObserver.cs  CallbackProgress.cs   (step 1 → step 2)
                        IAcquisitionRunner.cs  AcquisitionRunner.cs  LoggingAcquisitionObserver.cs              (api: in the background)
    JobFetchProbe.Infrastructure/
      DependencyInjection.cs   AddInfrastructure (console)
      Http/      HttpPageFetcher.cs  HttpClientSettings.cs
      Files/     SavedPageReader.cs
      FetchJobListing/  InputFolder.cs  FetchJobListingSettings.cs
    JobFetchProbe.Data/
      DependencyInjection.cs   AddJobStore (console and api, read-write) · AddJobCatalog (read-only, for an app that only reads)
      JobsDbContext.cs  DatabaseSettings.cs  DatabaseInitializer.cs  JobRowMapping.cs  DesignTimeJobsDbContextFactory.cs
      Configuration/   DataPaths.cs
      Entities/        JobRow.cs  JobDetailRow.cs  JobStatus.cs  ListingRow.cs  ListingSourceRow.cs  ListingJobRow.cs
      Configurations/  <Row>Configuration.cs, one IEntityTypeConfiguration per table
      Repositories/    SqliteBatchRepository.cs  SqliteJobRepository.cs  SqliteJobCatalog.cs
      Migrations/      InitialCreate, RemoveSearchesAndListingProgress, AddJournal, JobsDbContextModelSnapshot.cs
      Journal/         JournalRowMapping.cs
        Entities/        JobListingJournalRunRow.cs  JobListingJournalEntryRow.cs  JobDetailsJournalRunRow.cs  JobDetailsJournalEntryRow.cs
        Configurations/  one per journal table
        Repositories/    SqliteJournalRepository.cs
    JobFetchProbe.Console/
      Program.cs  PipelineConsole.cs  FetchJobListingConsole.cs  FetchJobDetailsConsole.cs
      ConsolePaths.cs  ConsoleLog.cs  appsettings.json
    JobFetchProbe.Web/
      Program.cs  appsettings.json  Properties/launchSettings.json
      Api/       IJobCatalogClient.cs  JobCatalogClient.cs  JobCatalogApiSettings.cs  JobCatalogUnavailableException.cs
      Pages/     Index.cshtml(.cs)  Acquire.cshtml(.cs)  Job.cshtml(.cs)  _ViewImports.cshtml  _ViewStart.cshtml  Shared/_Layout.cshtml
      wwwroot/   css/site.css  js/split.js  img/logo.png  img/favicon.png
    JobFetchProbe.Api/
      Program.cs  appsettings.json  Properties/launchSettings.json  JobFetchProbe.Api.http
      Controllers/  JobFetcherController.cs  JobCatalogController.cs  JournalController.cs
      Handlers/     JobStoreUnavailableHandler.cs  JournalUnavailableHandler.cs
  docs/      plan.md  spec.md  database.md  database.svg
```

The repository holds code and documents only. Everything the app reads or writes is under `Paths:DataRoot`, a folder outside the repository, so Git commands (`git clean -fdx`, a fresh clone) never touch it:

```text
job-fetch-probe-data/              Paths:DataRoot
  database/jobs.db                 Database:Path
  input/                           FetchJobListing:Folder: saved search pages to read
    archived/                      FetchJobListing:ArchiveFolder: pages already read
```

`DataPaths` (Data/Configuration, used by the console app and the API) adds these three paths as an in-memory configuration source **underneath** all others (index 0), computed from the final value of `Paths:DataRoot`. A path set in `appsettings.json` or an environment variable therefore still wins on its own. Without `Paths:DataRoot`, each setting is used as given (or its code default). At start-up the app creates `input` and `input/archived` and prints `Data folder: …`.

Conventions:

- **One type per file**, the file named after the type; namespaces follow folders.
- **Helpers** are `internal static` classes of pure functions (no state, no I/O, not registered in dependency injection), or small internal classes that hold one run's state: `Parsing/HtmlQuery` (element text by class, meta tags, attributes, title), `Parsing/UrlHelper` (http(s) check, query values, links without tracking parameters), `Parsing/JsonLd` (schema.org objects in a page), `FetchJobDetails/FetchOutcome` (read, gone, what the circuit breaker is told), `FetchJobDetails/TimeLeftEstimate`, `FetchJobDetails/JobDetailsFetchState`, `Journal/RunRecorderCore` (queue, sign of life, lock and finishing, shared by both recorders). The classes that do the work stay behind their interfaces; helpers shared by both steps live in `Parsing/`, others in their step's folder, journal ones in `Journal/`.
- **Models are not database rows.** `JobFetchProbe.Models` holds the shapes every layer shares (immutable records). The tables have their own classes in `Data/Entities` (mutable, with keys, positions and status columns, suffix `Row`), used only inside the Data project and copied to and from the models in `JobRowMapping`. The repositories take and return models only.

## Dependencies

```text
Console ──► Services (IAcquisitionService) ──► Models
   │            ▲  ▲
   ├──► Infrastructure    (implements IPageFetcher, ISavedPageReader, IInputFolder)        Program.cs only
   └──► Data ─────────┘   (implements IBatchRepository, IJobRepository, IJobCatalog)       Program.cs only

Web ──HTTP──► Api ──► Services (IJobRetriever → IJobCatalog) ; Api/Program.cs ──► Data (registration only)
               Api ──► Services (IJournalService → IJournalRepository ◄── Data)          journal; the console writes it
```

| Project | References | Packages |
| --- | --- | --- |
| Models | none | none |
| Services | Models | `Microsoft.Extensions.Hosting.Abstractions` 10.0.0 (the runner is a hosted service), `Microsoft.Extensions.DependencyInjection.Abstractions` 10.0.0, `Microsoft.Extensions.Logging.Abstractions` 10.0.0 (journal warnings), `Microsoft.Extensions.Options.ConfigurationExtensions` 10.0.0 (step 2's settings) |
| Infrastructure | Services | `Microsoft.Extensions.Http` 10.0.0, `Microsoft.Extensions.Options.ConfigurationExtensions` 10.0.0 |
| Data | Services | `Microsoft.EntityFrameworkCore.Sqlite` 10.0.13, `Microsoft.Extensions.Configuration` 10.0.0, `Microsoft.Extensions.Options.ConfigurationExtensions` 10.0.0 |
| Console | Services, Infrastructure, Data | `Microsoft.Extensions.Hosting` 10.0.0, `Microsoft.EntityFrameworkCore.Design` 10.0.13 (design time only, `PrivateAssets=all`) |
| Web | Models | none (`Microsoft.NET.Sdk.Web`: ASP.NET Core shared framework, which includes `IHttpClientFactory`) |
| Api | Services, Data, Infrastructure (the last two only for `Program.cs`) | `Microsoft.AspNetCore.OpenApi` 10.0.13 (`Microsoft.NET.Sdk.Web`) |

Only the console's `Program.cs` refers to Infrastructure and Data, to register them; its other classes use Services and Models only (Console → Services → (Data | Infrastructure)). Services never use `HttpClient`, files or the database directly; everything outside goes through `Services/Abstractions`, which Infrastructure and Data implement. The web app references only Models and reads through the API; the API's controllers use only Services. Neither can reach the console's HTTP client or the input folder.

## Run

`dotnet run --project src/JobFetchProbe.Console`, or the built `job-fetch-probe` executable. Arguments are ignored.

1. Build the host (content root = build output folder, so `appsettings.json` is always found), apply `DataPaths`, register services and infrastructure.
2. Create the data folders; print `Data folder: …`.
3. `InitializeDatabaseAsync`: create the database folder and file if missing, apply pending migrations; print `Database: …` (with `schema updated, N migration(s) applied` when it did).
4. `PipelineConsole.RunAsync` calls `IAcquisitionService.RunAsync` (Services): step 1, then step 2 (always, whatever step 1 found), each in its own DI scope and recorded in the journal; `PipelineConsole` is the `IAcquisitionObserver` and prints what it is told through `FetchJobListingConsole` and `FetchJobDetailsConsole`, then the summary. Ctrl+C is caught only during step 2 and cancels the token passed to `RunAsync`. Before anything else, `IAcquisitionService.IsRunningAsync` is asked: if an acquisition is running (a run of either step `Running` in the journal with a sign of life in the last 2 minutes, e.g. one started from the API), the console prints so and exits with 4.

| Exit code | Meaning |
| --- | --- |
| 0 | Nothing left to do: no page kept in the input folder, and no job left to read (every job `Read` or `Gone`) |
| 1 | Something is left for the next run: a page kept in the input folder, or jobs still `Pending`, `Blocked` or `Failed` |
| 2 | Configuration error (`OptionsValidationException`) |
| 3 | Database error: the file is locked, read-only, damaged or not a database (`SqliteException`, `DbUpdateException`) |
| 4 | Another acquisition is running (started from the API); nothing was done |

## Step 1, FetchJobListing: saved search pages → job listing (`JobListingFetcher`)

1. **List** the files directly in `FetchJobListing:Folder` matching `FetchJobListing:Patterns` (`*.html`, `*.htm`), oldest first (last write time, then name). Subfolders, including the archive, are not read.
2. **Read** each file with `ISearchService.ReadFileAsync` (no network):
   - The page's address is its canonical link or `og:url`, else the browser's `<!-- saved from url=… -->` note. A page without any is rejected (or reported as a bot check if it is one).
   - A page with no result cards of a known layout whose only job is the one selected in its address (`currentJobId`) is rejected: it holds no list. This is what a view-source save of LinkedIn's signed-in pages looks like (the list is loaded later by scripts); the message also mentions a possible layout change.
   - A page with no job links is rejected; if it is a bot-check page, that is the reason given.
3. **Merge** the jobs: a job already found in an earlier page of the same run is skipped (`JobId`), so the first page wins.
4. If at least one page was read, **store** the listing with `IBatchRepository.SaveAsync`, in one transaction: a `Listings` row (`BatchId` = local time `yyyyMMdd_HHmmss`, `_2`, `_3`… if taken), one `ListingSources` row per page, one `ListingJobs` row per job (as its card showed it), and a `Pending` row in `Jobs` for every job not seen before. A job not read yet gets its title, company, location and URL updated from the card; a job already `Read` keeps its page data.
5. Only after that, **archive** each page read: move it to `FetchJobListing:ArchiveFolder`, together with the `<name>_files` folder the browser saves next to a "complete" page. If the archive already has that name, the moved file becomes `<name>_<yyyyMMdd_HHmmss><ext>` (then `_2`, `_3`…); nothing is overwritten.

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

## Step 2, FetchJobDetails: job listing → job details (`JobDetailsFetcher`)

1. **Work list:** `IJobRepository.GetUnreadAsync`: every job whose status is `Pending`, `Failed` or `Blocked`. Never-tried (`Pending`) jobs first, then by `FirstSeenAt`, then `JobId` (sorted in memory: SQLite cannot order by `DateTimeOffset`).
2. For each job, in that order: wait `FetchJobDetails:DelaySeconds` since the previous request, then `IJobService.ReadUrlAsync(url)` (fetch + parse; the job keeps the listed `JobId`).
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

ASP.NET Core Razor Pages, read-only. `dotnet run --project src/JobFetchProbe.Web` → <http://localhost:5080> (`Properties/launchSettings.json`). It needs the API running; `JobFetchProbe.slnLaunch` (Visual Studio profile "Web + Api") starts both.

- **Data only from the API:** the pages call `IJobCatalogClient`, a typed `HttpClient` (`AddHttpClient<IJobCatalogClient, JobCatalogClient>`) with `BaseAddress` = `JobCatalogApi:BaseUrl` (default `http://localhost:5081`) and `Timeout` = `JobCatalogApi:TimeoutSeconds` (default 10, 1–120; both validated at start-up). The JSON is read into the Models DTOs (`JobSummaryPage`, `StoredJob`). The web app has no database settings and never opens the database.
- **When the jobs cannot be shown:** a 503 from the API (no database yet) or a connection failure or timeout becomes `JobCatalogUnavailableException`; the page shows its message (the API's own text for 503, "The API is not reachable at … Start JobFetchProbe.Api" otherwise) instead of the table or the job.
- **Layout** (`Pages/Shared/_Layout.cshtml`): a CSS grid with a 216 px menu column (logo `img/logo.png` 64×64 shown at 32 px, "Job Fetch Probe", items Overview and Acquire; the page sets `ViewData["Nav"]` to mark the active item) and the content column. Below 900 px the menu shrinks to icons and the details pane covers the table.
- **`/`** (`Pages/Index`, Overview): `GET api/JobCatalog?page=p&pageSize=25`. `p` below 1 is sent as 1 (the API would answer 400); a page past the end is corrected by the API, and the pager uses the page it returns. Jobs are ordered by the API (newest job IDs first). Columns: Title (link), Company, Location, Posted, Status (coloured tag per status). Footer: "Showing a–b of n jobs", ‹, the first and last page, the current page with its neighbours, gaps as "…", ›. Page links keep the selected job.
- **Details pane** (`/?p=&job=<id>`): `GET api/JobCatalog/{id}` as well; the selected row is highlighted. Shows ✕ (back to `/?p=`), `#<id>`, status, title, company · location, "Open the job page ↗" (`Url`), then label–value rows: Job ID, Posted, Employment type, every stored detail, First seen, Description read, and Last attempt / Last error when there is one; then the description (line breaks kept), or a note that it is not read yet. An unknown ID shows "There is no job … in the database." with status 404.
- **Divider and rows** (`wwwroot/js/split.js`, the only script): the divider between table and pane (`role="separator"`, focusable) is dragged with the pointer or moved with the arrow keys (Shift for larger steps); the pane is 280 px or wider and the table keeps at least 480 px. The width is stored in `localStorage` (`jobfetchprobe.paneWidth`); double-click resets it to 440 px. A click anywhere on a row follows the row's link. Without the script everything works except dragging. On a narrow table, container queries hide Posted (below 760 px) and Location (below 620 px).
- **`/acquire`** (`Pages/Acquire`): three steps explaining acquisition (save search pages into `input`, run the console app, see the result in Overview). No data.
- **`/jobs/{id}`** (`Pages/Job`): redirects (302) to `/?p=&job={id}`, so old links keep working.
- **Styles:** `wwwroot/css/site.css`, light theme only: grey background `#f6f7f9`, white cards with `#e5e7eb` borders and 12 px corners, green accent `#16a34a`; status tags Read green, Pending indigo, Blocked orange, Failed red, Gone grey. Static files are served with `MapStaticAssets`.

## API (`JobFetchProbe.Api`)

ASP.NET Core controllers. `dotnet run --project src/JobFetchProbe.Api` → <http://localhost:5081>. Configuration: `Paths:DataRoot`, `FetchJobDetails` and `HttpClient` in its own `appsettings.json` (`DataPaths` derives `Database:Path` and the input folders as for the console app). At start-up it creates the data folders and runs `InitializeDatabaseAsync` (database created or migrated, `Database: …` logged); a bad setting (`ValidateOnStart`) stops it with `Configuration error: …` and exit code 2. It is the web app's only source of data. `JobFetchProbe.Api.http` holds sample requests.

`JobFetcherController` (route `api/JobFetcher`) depends only on `IAcquisitionRunner`:

| Action | Request | Responses |
| --- | --- | --- |
| `Start` | `POST api/JobFetcher` | `202 AcquisitionStart` (`state` `Started`, `at`, `message`), `Location: /api/Journal/runs`: the acquisition runs in the background. `409 ProblemDetails` ("Acquisition already running") if one is running in this process, or anywhere according to the journal. |

`AcquisitionRunner` (Services/Acquisition, singleton and hosted service, registered by `AddAcquisitionRunner()`): `TryStartAsync` checks and starts under a semaphore (one check-and-start at a time), refuses while its own run is going or while `IAcquisitionService.IsRunningAsync` is true, then starts `IAcquisitionService.RunAsync(JournalStarter.Api, LoggingAcquisitionObserver, token)` on a background task. `StopAsync` (application stopping) cancels the token, so step 2 stops after the current job (journal `Stopped`, "cancelled by the user"), and waits for the run within the host's shutdown timeout. `LoggingAcquisitionObserver` writes one log line per page, job, circuit change and result.

`JobCatalogController` (`[ApiController]`, route `api/JobCatalog`, JSON with camelCase names) depends only on `IJobRetriever`:

| Action | Request | Responses |
| --- | --- | --- |
| `GetPage` | `GET api/JobCatalog?page=&pageSize=` | `200 JobSummaryPage` (`IJobRetriever.GetPageAsync`): `jobs[]` (`jobId`, `title`, `company`, `location`, `posted`, `status`), `page`, `pageSize`, `total`, `totalPages`. Defaults `page=1`, `pageSize=25`. `[Range]` validation: `page` ≥ 1, `pageSize` 1–100, else `400 ValidationProblemDetails`. A page past the end is clamped to the last page (the `page` field says which). |
| `GetJob` | `GET api/JobCatalog/{jobId}` | `200 StoredJob` (`IJobRetriever.GetJobAsync`): `jobId`, `url`, `title`, `company`, `location`, `posted`, `employmentType`, `details[]` (`label`, `value`), `description`, `status`, `lastError`, `firstSeenAt`, `retrievedAt`, `lastAttemptAt`. `404 ProblemDetails` if unknown. |

Both return `503 ProblemDetails` ("No database yet", `application/problem+json`) while the database file does not exist; since the API creates it at start-up, only if it is removed while the API runs.

`JobRetriever` (Services/Catalog, singleton, registered by `AddJobRetriever()`) holds the reading rules: `DefaultPageSize` 25, `MaxPageSize` 100 (the controller's `[Range]` uses these), `ArgumentOutOfRangeException` for a page below 1 or a page size out of range, the job ID trimmed and an empty ID treated as unknown (null → 404). Before reading it checks `IJobCatalog.IsAvailable`, which the data layer answers (`SqliteJobCatalog`: the database file exists); if not, it throws `JobStoreUnavailableException`. `JobStoreUnavailableHandler` (`IExceptionHandler`, with `AddProblemDetails` and `UseExceptionHandler`) turns that exception into the 503 for every endpoint, so the controller has no database check. `JournalController` (route `api/Journal`) depends only on `IJournalService`:

| Action | Request | Responses |
| --- | --- | --- |
| `GetRuns` | `GET api/Journal/runs?take=` | `200 JournalRunSummary[]`: `step`, `runId`, `startedBy`, `startedAt`, `lastSeenAt`, `finishedAt`, `state`, `message`. `take` 1–50 (default 10), else 400. |
| `GetJobListingRun` | `GET api/Journal/job-listing/runs/{runId}?after=` | `200 JobListingJournalRun` with `entries` after `after` (default 0); `404` if unknown. |
| `GetJobDetailsRun` | `GET api/Journal/job-details/runs/{runId}?after=` | `200 JobDetailsJournalRun` with `entries` after `after`; `404` if unknown. |

All three return `503` ("No journal yet", via `JournalUnavailableHandler`) while the database file does not exist. Enums are serialised as their names (`JsonStringEnumConverter`).

In Development, `GET openapi/v1.json` serves the OpenAPI 3.1 description (`AddOpenApi` / `MapOpenApi`), with the response types declared by `[ProducesResponseType]`.

## Journal

The progress of every run of each step, written while it runs and readable by another process at the same time. Isolated in its own folders: `Models/Journal`, `Services/Journal` + `Services/Abstractions/Journal`, `Data/Journal`, `Api/Controllers/JournalController`. The step folders do not refer to it.

**Writing (the process that fetches).** `IJournalService.StartJobListingRunAsync(starter)` / `StartJobDetailsRunAsync(starter)` returns a recorder (`IJobListingRunRecorder`, `IJobDetailsRunRecorder`), which is an `IProgress<>` of the step's own progress type. The console passes every progress report to its printer and to the recorder, then calls `CompleteAsync(result)` (or `FailAsync(error)` when the step throws; disposing without either records Failed). Before a run starts, runs of the same step still `Running` with no sign of life for `AbandonedAfter` are marked `Abandoned`, and the step's older runs are pruned to `KeepRunsPerStep` (a TODO marks this for review).

- **Recorder → queue → repository.** Each report updates the recorder's copy of the run (counters, circuit breaker, message) and queues one write: the run's state, plus one entry when something happened. `JournalQueue` writes in order on one background task, so the fetch never waits; a failed write is logged once (then counted) and skipped. State and entry are saved separately, so a failed entry never loses the final state.
- **Sign of life.** `SignOfLife` (a `PeriodicTimer`) refreshes `LastSeenAt` every `SignOfLifeEvery` (30 s) while the run is open, whatever the step is doing (a long pause or a 30-minute cooldown included).
- **What is recorded.** Step 1: one entry per page read (`Read`, or `Kept` with the reason) and, at the end, per page archived (`Archived` with the destination, or `NotArchived` with the reason); the run holds pages, read, kept, job links stored, new jobs, listing id. Step 2: `Started`, one `Job` entry per request (job id, title, status, gone, message), `CircuitOpened` (with the cooldown), `CircuitHalfOpen`, `CircuitClosed`, `Stopped`, `Finished` (totals); the run holds total, to read, fetched, failed, requests, circuit state, `CircuitOpenUntil`, openings, ETA. The step's "still waiting" reports only refresh the run (no entry). A run stopped by the circuit breaker ends with the circuit `Open`.
- **Never in the way.** If the journal cannot be started (database error), the recorder records nothing, a warning is logged and the console prints `Journal: not recorded this time …`; the step runs as usual.

**Reading (anyone).** `GetRecentRunsAsync(take)` (1–50, both steps, newest first), `GetJobListingRunAsync(id, after)`, `GetJobDetailsRunAsync(id, after)` (the run plus its entries with id greater than `after`; null if unknown). A run still `Running` whose `LastSeenAt` is older than `AbandonedAfter` (2 minutes) is returned as `Abandoned` with "No sign of life since …". `JournalUnavailableException` when the database file does not exist; a database without the journal tables (not yet upgraded by the console) reads as empty.

`JournalOptions` (code defaults, `JournalOptions.Default`): `AbandonedAfter` 2 min, `SignOfLifeEvery` 30 s, `KeepRunsPerStep` 50. `AddJournal()` (Services) registers `JournalOptions` and `IJournalService` (singleton). The data layer registers `IJournalRepository` → `SqliteJournalRepository` in `AddJobStore` (read-write: console and API) and in `AddJobCatalog` (read-only). `IJournalService.IsAnyRunActiveAsync` tells whether a run of either step is `Running` and not abandoned; the acquisition uses it to refuse a second one.

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

## Data (`JobFetchProbe.Data`)

### Database (`JobsDbContext`)

One SQLite file at `Database:Path` (`<DataRoot>\database\jobs.db`), through EF Core 10. Each table is configured in its own `IEntityTypeConfiguration<T>` class (`Configurations/`, and `Journal/Configurations/` for the journal); `OnModelCreating` only calls `ApplyConfigurationsFromAssembly`.

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
- **Repositories** (`Repositories/`): `SqliteBatchRepository` (`IBatchRepository`, step 1), `SqliteJobRepository` (`IJobRepository`, step 2) and `SqliteJobCatalog` (`IJobCatalog`, read-only, for the API). They return models; no entity leaves the project.
- **Migration namespace:** `JobFetchProbe.Data.Migrations`. EF Core identifies an applied migration by its ID in `__EFMigrationsHistory`, not by namespace, so databases created before the move to this project are not migrated again.
- **Migrations** (`Migrations/`), applied at start-up: `InitialCreate`; `RemoveSearchesAndListingProgress` (drops the tables and columns of the removed search command and listing-based details step). `DesignTimeJobsDbContextFactory` lets the EF tools build the model without starting the app. After changing the rows or their configuration:

  ```sh
  dotnet ef migrations add <Name> --project src/JobFetchProbe.Data --startup-project src/JobFetchProbe.Console --output-dir Migrations
  ```

- EF Core's own log lines are filtered out (`Microsoft.EntityFrameworkCore` → `None`); a database failure prints `Database error: …` and exits with 3.

## Configuration

`src/JobFetchProbe.Console/appsettings.json`, copied to the build output. Environment variables override settings (`Paths__DataRoot=D:\job-data`). Command-line arguments are not used.

| Key | Default | Validation |
| --- | --- | --- |
| `Paths:DataRoot` | `C:\Code\job-fetcher\job-fetch-probe-data` | none; when set, the keys marked † default to the layout above |
| `Database:Path` † | `<DataRoot>\database\jobs.db` | not empty |
| `FetchJobListing:Folder` † | `<DataRoot>\input` | not empty |
| `FetchJobListing:ArchiveFolder` † | `<DataRoot>\input\archived` | not empty, different from `FetchJobListing:Folder` |
| `FetchJobListing:Patterns` | `*.html`, `*.htm` (code default) | at least one |
| `FetchJobDetails:DelaySeconds` | `5` | 1 to 120 |
| `FetchJobDetails:CircuitBreaker:FailureThreshold` | `3` | 1 to 20 |
| `FetchJobDetails:CircuitBreaker:CooldownSeconds` | `300` | 1 to 14400 |
| `FetchJobDetails:CircuitBreaker:MaxCooldownSeconds` | `1800` | 1 to 86400 |
| `FetchJobDetails:CircuitBreaker:StopAfterOpenings` | `3` | 1 to 10 |
| `HttpClient:TimeoutSeconds` | `20` | 1 to 120 |
| `HttpClient:UserAgent` | `JobFetchProbe/0.3 (personal study project)` | not empty |
| `HttpClient:HandlerLifetimeMinutes` | `5` | none |
| `Logging:LogLevel:Default` | `Warning` | none; keeps HttpClient request logs off the console |

Validation runs when the settings are first used. A failure prints `Configuration error: …` and exits with 2.

## Dependency injection

Console: `AddServices(IConfiguration)` (Services: both steps, step 2's settings `FetchJobDetails` with their validation, `IAcquisitionService`), `AddInfrastructure(IConfiguration)` (Infrastructure: `FetchJobListing` and `HttpClient` settings, HTTP client, saved pages, input folder), `AddJournal()` (Services: `IJournalService`) and `AddJobStore(IConfiguration)` (Data: database settings, context factory, `IBatchRepository`, `IJobRepository`, `IJournalRepository`). Api: `AddServices(IConfiguration)`, `AddAcquisitionRunner()` (Services: `IAcquisitionRunner`, hosted), `AddJobRetriever()` (Services: `IJobRetriever` → `JobRetriever`), `AddJournal()`, `AddInfrastructure(IConfiguration)` and `AddJobStore(IConfiguration)` (Data, read-write; it also registers `IJobCatalog` → `SqliteJobCatalog`). `AddJobCatalog(IConfiguration)` (read-only) is no longer used by an app. Web: `JobCatalogApiSettings` and the typed client `IJobCatalogClient` only. Both Data methods share the private `AddDatabase`; `AddJobCatalog` opens the connection with `Mode=ReadOnly`. The console classes are singletons and only print; `Program.cs` builds `ConsolePaths` (database file, input and archive folders as full paths) for them. `AcquisitionService` creates one DI scope per step.

| Registration | Lifetime |
| --- | --- |
| `TimeProvider.System` | singleton |
| `IJobPageParser`, `ISearchPageParser` | singleton |
| `IJobService`, `ISearchService`, `IJobListingFetcher`, `IJobDetailsFetcher` | transient (they depend, directly or not, on the typed `HttpClient`, which must not be pinned by a singleton) |
| `IPageFetcher` → `HttpPageFetcher` | typed client (factory-managed handler) |
| `ISavedPageReader`, `IInputFolder` | singleton |
| `IDbContextFactory<JobsDbContext>` | singleton factory; short-lived contexts |
| `IBatchRepository` → `SqliteBatchRepository`, `IJobRepository` → `SqliteJobRepository` | singleton |
| `JournalOptions`, `IJournalService` → `JournalService`, `IJournalRepository` → `SqliteJournalRepository` | singleton (a recorder, with its queue and sign of life, lives for one run) |
| `IAcquisitionService` → `AcquisitionService` | singleton (no state; a scope per step) |
| `IAcquisitionRunner` → `AcquisitionRunner` (API) | singleton, also the hosted service that cancels the run when the API stops |
| `PipelineConsole`, `FetchJobListingConsole`, `FetchJobDetailsConsole`, `ConsolePaths` | singleton |

## Console output

Every line has a `[HH:mm:ss]` time; ok is green, warnings yellow, refusals red (no colours when output is redirected).

```text
[18:32:01] Data folder: C:\Code\job-fetcher\job-fetch-probe-data
[18:32:01] Database: C:\Code\job-fetcher\job-fetch-probe-data\database\jobs.db

===== Step 1: saved search pages → job links =====
[18:32:01] Journal: run 12
[18:32:01] [1/2] 197 .net jobs in Valencia.html: 100 jobs (100 new in this run)
[18:32:01] [2/2] view-source_….html: kept in the input folder. the page only contains the selected job …
[18:32:01] Stored 100 unique job links as listing 20261014_183201.
[18:32:01] Archived 1 page(s) to C:\Code\job-fetcher\job-fetch-probe-data\input\archived

===== Step 2: job links → job descriptions =====
[18:32:02] Journal: run 9
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
12. The web app lists every job, 25 per page, newest IDs first; clicking a row shows its details and description in a pane whose divider can be dragged; an unknown ID gives 404; it reads only through the API; without a database it shows the API's message, and without the API it says the API is not reachable.
13. The API returns a `JobSummaryPage` for `GET api/JobCatalog?page=&pageSize=` and a `StoredJob` for `GET api/JobCatalog/{jobId}`; out-of-range values give 400, an unknown job 404. A missing database is created at the API's start-up.
14. Every run of each step is recorded in the journal while it runs (console line `Journal: run N`): its state and entries can be followed from another process through `api/Journal` during the run; the state ends `Completed`, `Stopped` or `Failed`, or reads `Abandoned` 2 minutes after the last sign of life of a killed process.
15. `POST api/JobFetcher` answers 202 and runs the acquisition in the background (jobs stored, pages archived, journal runs `startedBy: Api`); a second POST while it runs answers 409, and the console exits with 4; stopping the API during a cooldown ends the run `Stopped`; a bad setting stops the API at start-up with exit code 2.
16. A journal that cannot be written (start or entries) never stops or slows a step: the step completes, a warning is logged, and a run's final state is still stored when only its entries fail.
17. The code contains no cookie handling, proxy settings, quick retries, or browser automation.

## Not in this build

Commands or arguments (the earlier prompt, single-job, search-by-link and `details` commands were removed), JSON-file storage, requesting search result pages (pagination, the `/jobs-guest/` endpoints), running or solving bot-check scripts, logins, cookies, browser automation, retries with a different identity, scheduled runs, a server database (PostgreSQL in Docker is a possible later step: the EF Core model would stay, with another provider and a new migration), applying to jobs, and storage of recruiter or contact details.
