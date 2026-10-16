# JobFetchProbe

A .NET console app that collects job postings in two steps and keeps them in a SQLite database:

1. **Job links.** It reads the search result pages you saved from your browser into the `input` folder, stores every job link they contain (URL, title, company), and moves each page it read to `input\archived`.
2. **Job descriptions.** It goes through every job in the database that has no description yet, one at a time, reads its page, and stores the description and details with the job's status.

Then it prints a summary with the totals and exits. There are no commands or options: every start runs the same flow.

A small **web app** shows what is stored: a table of all jobs with pagination, and a page per job with its details and description (see [Web app](#web-app)), and a small **REST API** returns the same data as JSON (see [API](#api)).

## Run

```sh
dotnet run --project src/JobFetchProbe.Console
```

```text
[18:32:01] Data folder: C:\Code\job-fetcher\job-fetch-probe-data
[18:32:01] Database: C:\Code\job-fetcher\job-fetch-probe-data\database\jobs.db

===== Step 1: saved search pages → job links =====
[18:32:01] [1/2] 197 .net jobs in Valencia.html: 100 jobs (100 new in this run)
[18:32:01] [2/2] view-source_….html: kept in the input folder. …
[18:32:01] Stored 100 unique job links as listing 20261014_183201.
[18:32:01] Archived 1 page(s) to C:\Code\job-fetcher\job-fetch-probe-data\input\archived

===== Step 2: job links → job descriptions =====
[18:32:02] 100 job(s) without a description in the database.
[18:32:02] One request every 5s, about 10m 00s in total if the site keeps answering.
[18:32:03] [   1/100]   1%  ok       4476446382  Senior .NET Developer · W Hunt España  (ok 1, failed 0, 99 to go, ~9m 54s left)
[18:32:09] [   2/100]   2%  REFUSED  4476446383  (ok 1, failed 1, 98 to go, ~9m 48s left)
[18:32:09] Circuit OPEN (1 of 3): es.linkedin.com refused the request for job 4476446383 …
[18:32:39]   … circuit open, trial request in 4m 30s

===== Summary =====
Input pages      2 found, 1 read and archived, 1 kept in the input folder
Job links        100 stored as listing 20261014_183201
Descriptions     97 read in this run, 3 failed, 0 not tried
                 97 stored in total; 2 still to read (tried again on the next run)
Requests         101 sent in 14m 12s, circuit breaker opened 1 time(s)
Database         C:\Code\job-fetcher\job-fetch-probe-data\database\jobs.db
```

Exit code: `0` nothing left to do, `1` something is left for the next run (a page kept in `input`, or jobs still without a description), `2` configuration error, `3` database error.

### Step 1: saved search pages

- Save search result pages from your browser into `input` with **"Save page as → Webpage, Complete"** (Ctrl+S). Both work:
  - the **signed-in** search (`linkedin.com/jobs/search-results/…`): scroll through the whole result list first, because LinkedIn only builds the cards that have been on screen, and save each results page;
  - the **signed-out** search (a private window, `linkedin.com/jobs/…`).
- Do **not** use view-source (Ctrl+U): it shows the page before LinkedIn's scripts load the list, so it contains only the selected job. Such pages are kept in `input` with that explanation.
- To get more results than one page shows, save several pages (scroll further, or use other filters); their jobs are merged without duplicates.
- Pages that cannot be read stay in `input` with the reason shown, and are tried again on the next start.
- The pages are only read from disk: step 1 sends no requests.

### Step 2: job descriptions

Each job in the database has a status:

| Status | Meaning | Tried by step 2? |
| --- | --- | --- |
| `Pending` | Listed, page not read yet | yes, first |
| `Read` | Description and details stored | no |
| `Blocked` | The site refused the last attempt | yes, again on the next run |
| `Failed` | Network or server error, or nothing readable | yes, again on the next run |
| `Gone` | The page no longer exists (HTTP 404 or 410) | no |

Each result is stored as soon as it is known, so a stopped run loses nothing: the next start continues with what is still unread. A job already read is never requested again, whichever pages listed it. Ctrl+C stops after the current job.

**Politeness and the circuit breaker.** One request at a time, a pause between requests (`Details:DelaySeconds`), an honest User-Agent, no cookies, no quick retries. A refusal (401, 403, 429, 999 or a sign-in page) opens the circuit at once; 3 network or server errors in a row do too; a 404 only marks that job as gone. While open, nothing is sent for 5 minutes (doubling each time, at most 30); then the same job is tried once. If it works, the run continues; at the third opening the run stops. LinkedIn is likely to refuse these requests, so expect runs to stop early and to continue on later starts.

## Web app

The web app shows the jobs it gets from the [API](#api), so both must run. In two terminals:

```sh
dotnet run --project src/JobFetchProbe.Api
dotnet run --project src/JobFetchProbe.Web
```

Then open <http://localhost:5080>. Stop each with Ctrl+C. In Visual Studio, choose the startup profile **Web + Api** (from `JobFetchProbe.slnLaunch`, Visual Studio 2022 17.10 or later) and press F5: it starts both.

The screen has a narrow menu on the left (the app's logo and name, **Overview** and **Acquire**) and the content on the right:

- **Overview** (`/`) lists all jobs, newest job IDs first, 25 per page: title, company, location, posted date and a coloured status tag (Read green, Pending blue, Blocked orange, Failed red, Gone grey). Below the table: "Showing 1–25 of 107 jobs" and the page buttons.
- **Clicking a row** opens that job in a **details pane** on the right (`/?p=2&job=<id>`): status, title, company and location, a link to the job's own page, the labelled details (seniority, employment type, sectors…), when it was first seen and read, the last error if any, and the full description. ✕ closes it. The address can be bookmarked; old `/jobs/<id>` links open the same view.
- **The divider** between the table and the pane can be dragged left or right; the table's text wraps, and on a narrow table the Posted and Location columns step aside. The width is remembered in your browser; double-click the divider to reset it (or focus it and use the arrow keys).
- **Acquire** (`/acquire`) explains, for now, how jobs get in: save search pages into `input`, run the console app.

It never touches the database: it only calls the API's two endpoints, at `JobCatalogApi:BaseUrl` in `src/JobFetchProbe.Web/appsettings.json` (`http://localhost:5081`). If the database does not exist yet, it shows the API's message (run the console app first); if the API is not running, it says so. You can keep it open while the console app runs; reload to see new jobs.

## API

```sh
dotnet run --project src/JobFetchProbe.Api
```

Listens on <http://localhost:5081> (Visual Studio: set `JobFetchProbe.Api` as the startup project). `src/JobFetchProbe.Api/JobFetchProbe.Api.http` has ready-made requests for Visual Studio or VS Code.

| Request | Returns |
| --- | --- |
| `GET /api/JobCatalog?page=1&pageSize=25` | `200` with a `JobSummaryPage`: `jobs` (jobId, title, company, location, posted, status), `page`, `pageSize`, `total`, `totalPages`. Newest job IDs first. `page` defaults to 1, `pageSize` to 25 (1–100); a page past the end returns the last page. `400` with the errors if a value is out of range |
| `GET /api/JobCatalog/{jobId}` | `200` with a `StoredJob`: url, title, company, location, posted, employment type, `details` (label/value), description, status, last error, first seen, read and last attempt times. `404` if the job is not in the database |
| `GET /openapi/v1.json` | The OpenAPI description of the API (Development only) |

Errors are returned as standard problem details. If the database does not exist yet, both endpoints return `503`: the data layer reports it, the service turns it into a `JobStoreUnavailableException`, and one handler in the API turns that into the response, so the controller holds no database logic. The API reads the same database as the console app (same `Paths:DataRoot`, in its own `appsettings.json`) and opens it **read-only**: it can never change it. The web app gets its data only from these two endpoints.

## Folders

Code and data are kept apart. The repository holds only what belongs in Git; everything the app reads or writes lives in one data folder next to it:

```text
C:\Code\job-fetcher\
  job-fetch-probe\                 ← the repository: code and documents only (this is what goes to GitHub)
    docs\                          plan, spec and database diagram
    src\                           the seven projects (Console, Web, Api, Services, Models, Infrastructure, Data)
    .gitignore  CLAUDE.md  Directory.Build.props  JobFetchProbe.sln  JobFetchProbe.slnLaunch  nuget.config  README.md

  job-fetch-probe-data\            ← your data: never committed, never touched by Git
    database\
      jobs.db                      every job, listing and description
    input\                         ← save search result pages here
      archived\                    pages already read (moved here automatically)
```

- **Your data is safe from Git.** `git clean -fdx`, or deleting and re-cloning the repository, cannot touch the database or your saved pages.
- **One place to back up.** Copy `job-fetch-probe-data` while the app is closed.
- **One setting.** All of these paths come from `Paths:DataRoot` in `appsettings.json` (the console app's and the API's; the web app has no data paths, it asks the API). Change that line to move all your data. The app creates `input`, `input\archived` and the database on its first start.
- **Individual overrides still work.** `Inbox:Folder`, `Inbox:ArchiveFolder` and `Database:Path` can each be set on their own; they then win over `DataRoot`.

## The database

`jobs.db` is created on the first start and upgraded automatically when a new version changes its tables (EF Core migrations). Open it with [DB Browser for SQLite](https://sqlitebrowser.org/) or a SQLite extension for VS Code. For example:

```sql
-- jobs still without a description, with the reason
SELECT JobId, Status, Title, Company, LastError FROM Jobs WHERE Status <> 'Read';

-- what one listing contained
SELECT lj.Position, lj.JobId, lj.Title, lj.Company, j.Status
FROM ListingJobs lj JOIN Jobs j ON j.JobId = lj.JobId
WHERE lj.BatchId = '20261014_183201' ORDER BY lj.Position;
```

| Table | Content |
| --- | --- |
| `Jobs` | One row per job: URL, title, company, location, posted, description, status, last error, when it was first seen and last tried |
| `JobDetails` | The labelled facts of a job page (e.g. "Tipo de empleo: Jornada completa") |
| `Listings` | One row per run of step 1 that stored something |
| `ListingSources` | The pages each listing was read from |
| `ListingJobs` | The jobs each listing contained, as their cards showed them |

The diagram of these tables and their relationships is in [`docs/database.md`](docs/database.md).

The tables are defined in `src/JobFetchProbe.Data` (one entity and one configuration class per table). After changing them, add a migration from the repository folder; the app applies it on its next start:

```sh
dotnet ef migrations add <Name> --project src/JobFetchProbe.Data --startup-project src/JobFetchProbe.Console --output-dir Migrations
```

## Configuration

`src/JobFetchProbe.Console/appsettings.json`:

| Setting | Value | Meaning |
| --- | --- | --- |
| `Paths:DataRoot` | `C:\Code\job-fetcher\job-fetch-probe-data` | Where all data lives (see [Folders](#folders)) |
| `Details:DelaySeconds` | `5` | Pause between two requests (1–120) |
| `Details:CircuitBreaker:FailureThreshold` | `3` | Network or server errors in a row that open the circuit |
| `Details:CircuitBreaker:CooldownSeconds` | `300` | First wait while the circuit is open |
| `Details:CircuitBreaker:MaxCooldownSeconds` | `1800` | The wait doubles up to this |
| `Details:CircuitBreaker:StopAfterOpenings` | `3` | The run stops when the circuit opens this many times |
| `HttpClient:TimeoutSeconds` | `20` | Request timeout |
| `HttpClient:UserAgent` | `JobFetchProbe/0.3 (personal study project)` | How the app identifies itself |
| `HttpClient:HandlerLifetimeMinutes` | `5` | How long the client factory reuses a connection handler before rotating it |

Any setting can be overridden with an environment variable, using `__` for `:`, e.g. `Paths__DataRoot=D:\job-data`.

## Structure

```text
Console ──► Services ──► Models
   │            ▲  ▲
   ├──► Infrastructure    (HTTP, saved pages, input folder: implements the Services interfaces)
   └──► Data ─────────┘   (SQLite: implements the repository interfaces)

Web ──HTTP──► Api ──► Services (IJobRetriever) ──► IJobCatalog ◄── Data
```

Services defines the interfaces it needs (`Services/Abstractions`); Infrastructure and Data implement them, so Services never touches HTTP, files or the database itself. The reading side follows Web → Api → Services → Data: the web app calls the API over HTTP and references only Models (the DTOs in the JSON); the API's controllers use only the service layer (`IJobRetriever`); only the API's `Program.cs` refers to Data, to register it.

| Project | Role | Main files |
| --- | --- | --- |
| `JobFetchProbe.Models` | Data shapes shared by all layers. No dependencies | `Jobs/` (`JobPosting`, `JobResult` …), `Search/` (`JobListing`, `JobSearch`, `SearchResult`), `Inbox/` (`JobSearchBatch`, `InboxRunResult` …), `Details/` (`DetailsRunResult`, `DetailsEvent` …), `Catalog/` (`JobSummary`, `JobSummaryPage`, `StoredJob`: what the web app shows) |
| `JobFetchProbe.Services` | The two steps, reading stored jobs for the API, and the interfaces they need | `Catalog/JobRetriever` (`IJobRetriever`, used by the API), `Inbox/InboxService` (step 1), `Search/SearchService` + `SearchPageParser` (reading saved search pages), `Details/DetailsService` + `CircuitBreaker` (step 2), `JobService` + `Parsing/JobPageParser` (reading a job page), `Abstractions/` |
| `JobFetchProbe.Infrastructure` | HTTP and files | `Http/HttpPageFetcher`, `Files/SavedPageReader`, `Inbox/InboxFolder`, settings classes, `DependencyInjection` (`AddInfrastructure`) |
| `JobFetchProbe.Data` | The data layer: the SQLite database (EF Core) | `JobsDbContext`, `Entities/` (table rows), `Configurations/` (one per table), `Repositories/` (`SqliteBatchRepository`, `SqliteJobRepository`, `SqliteJobCatalog`), `Migrations/`, `JobRowMapping`, `DatabaseInitializer`, `Configuration/DataPaths`, `DependencyInjection` (`AddJobStore` read-write for the console, `AddJobCatalog` read-only for Web and Api) |
| `JobFetchProbe.Console` | Entry point: configuration, dependency injection, console output | `Program`, `PipelineConsole` (step 1 → step 2 → summary), `InboxConsole`, `DetailsConsole`, `ConsoleLog`, `SyncProgress`, `appsettings.json` |
| `JobFetchProbe.Web` | Read-only web page over the API (Razor Pages) | `Program`, `Api/JobCatalogClient` (`IJobCatalogClient`: the two API calls), `Pages/Index` (table, pagination and details pane), `Pages/Acquire`, `Pages/Job` (redirect of old links), `Pages/Shared/_Layout` (menu), `wwwroot/css/site.css`, `wwwroot/js/split.js` (divider and clickable rows), `wwwroot/img/` (logo), `appsettings.json` |
| `JobFetchProbe.Api` | Read-only REST API over the database (ASP.NET Core controllers, OpenAPI) | `Program`, `Controllers/JobCatalogController` (uses `IJobRetriever`), `Handlers/JobStoreUnavailableHandler` (503), `JobFetchProbe.Api.http`, `appsettings.json` |

Each file holds one type and is named after it. The database rows (`Data/Entities`, suffix `Row`) are kept apart from the shared models on purpose: the models (DTOs) describe what the app works with, the rows how SQLite stores it. Rows never leave the Data project; its repositories return models, so the tables can change without touching the other projects.

### HTTP client

The client is a **typed client** registered with `AddHttpClient<IPageFetcher, HttpPageFetcher>()`. `IHttpClientFactory` creates the `HttpClient` instances and pools the underlying connection handlers, rotating them every `HandlerLifetimeMinutes`. This avoids the two classic problems of `new HttpClient()`: running out of sockets when clients are created and disposed often, and stale DNS when one client lives forever. `HttpPageFetcher` never creates or disposes an `HttpClient`, and the services that use it are transient and resolved in a scope per step, so no long-lived object pins a handler.

### Page layouts

`JobPageParser` tries three layouts in order: LinkedIn's public job page (`topcard__title`, `topcard__org-name-link`, `description__job-criteria-*`, `show-more-less-html__markup`); schema.org `JobPosting` data embedded for search engines; then the page title and visible text. The LinkedIn class names come from one saved public job page (October 2026). If LinkedIn changes its markup, the app falls back to the generic layout; check a fresh saved page and update the class names.

## Documents

- [`docs/plan.md`](docs/plan.md): goal, scope, boundary, design decisions, limitations.
- [`docs/spec.md`](docs/spec.md): what the code implements, project by project.
- [`docs/database.md`](docs/database.md): the tables and how they relate, with a diagram (`docs/database.svg`).
- [`CLAUDE.md`](CLAUDE.md): how changes are made in this repository (plan first, then "go") and the architecture rules.

## History

The earlier probe version (crawler-policy check, tests, plan and spec) is kept in Git history under the tag `probe-v1`. Earlier versions of this app also had an interactive prompt, a single-job command, a search-by-link command and JSON-file storage; they were removed to keep one flow, and are in the Git history.
