# JobFetchProbe

A .NET web app that collects job postings in two steps and keeps them in a SQLite database:

1. **Job links.** It reads the search result pages you saved from your browser into the `input` folder, stores every job link they contain (URL, title, company), and moves each page it read to `input\archived`.
2. **Job descriptions.** It goes through every job in the database that has no description yet, one at a time, reads its page, and stores the description and details with the job's status.

Both steps together are an **acquisition**. You start, follow and stop it on the web page's **Acquire** screen; the **Overview** shows what is stored (see [Web app](#web-app)). A small **REST API** does the work and returns the same data as JSON (see [API](#api)).

## Run

The web app shows what it gets from the API, so both must run. In two terminals:

```sh
dotnet run --project src/JobFetchProbe.Api
dotnet run --project src/JobFetchProbe.Web
```

Then open <http://localhost:5080>. Stop each with Ctrl+C. In Visual Studio, choose the startup profile **Web + Api** (from `JobFetchProbe.slnLaunch`, Visual Studio 2022 17.10 or later) and press F5: it starts both.

The API creates the data folders and the database (or upgrades it) when it starts, and writes one line per page, job and circuit breaker change to its window while an acquisition runs.

### Step 1, FetchJobListing: saved search pages → job listing

- Save search result pages from your browser into `input` with **"Save page as → Webpage, Complete"** (Ctrl+S). Both work:
  - the **signed-in** search (`linkedin.com/jobs/search-results/…`): scroll through the whole result list first, because LinkedIn only builds the cards that have been on screen, and save each results page;
  - the **signed-out** search (a private window, `linkedin.com/jobs/…`).
- Do **not** use view-source (Ctrl+U): it shows the page before LinkedIn's scripts load the list, so it contains only the selected job. Such pages are kept in `input` with that explanation.
- To get more results than one page shows, save several pages (scroll further, or use other filters); their jobs are merged without duplicates.
- Pages that cannot be read stay in `input` with the reason shown, and are tried again on the next start.
- The pages are only read from disk: step 1 sends no requests.

### Step 2, FetchJobDetails: job listing → job details

Each job in the database has a status:

| Status | Meaning | Tried by step 2? |
| --- | --- | --- |
| `Pending` | Listed, page not read yet | yes, first |
| `Read` | Description and details stored | no |
| `Blocked` | The site refused the last attempt | yes, again on the next run |
| `Failed` | Network or server error, or nothing readable | yes, again on the next run |
| `Gone` | The page no longer exists (HTTP 404 or 410) | no |

Each result is stored as soon as it is known, so a stopped run loses nothing: the next start continues with what is still unread. A job already read is never requested again, whichever pages listed it. **Stop** stops after the current job.

**Politeness and the circuit breaker.** One request at a time, a pause between requests (`FetchJobDetails:DelaySeconds`), an honest User-Agent, no cookies, no quick retries. A refusal (401, 403, 429, 999 or a sign-in page) opens the circuit at once; 3 network or server errors in a row do too; a 404 only marks that job as gone. While open, nothing is sent for 3½ minutes (doubling each time, at most 30); then the same job is tried once. If it works, the run continues; at the third opening the run stops. LinkedIn is likely to refuse these requests, so expect runs to stop early and to continue on later starts.

## Web app

Start it with the API (see [Run](#run)).

The screen has a narrow menu on the left (the app's logo and name, **Overview** and **Acquire**) and the content on the right:

- **Overview** (`/`) lists all jobs, newest job IDs first, 25 per page: title, company, location, posted date and a coloured status tag (Read green, Pending blue, Blocked orange, Failed red, Gone grey). Below the table: "Showing 1–25 of 107 jobs" and the page buttons.
- **Clicking a row** opens that job in a **details pane** on the right (`/?p=2&job=<id>`): status, title, company and location, a link to the job's own page, the labelled details (seniority, employment type, sectors…), when it was first seen and read, the last error if any, and the full description. ✕ closes it. The address can be bookmarked; old `/jobs/<id>` links open the same view.
- **The divider** between the table and the pane can be dragged left or right; the table's text wraps, and on a narrow table the Posted and Location columns step aside. The width is remembered in your browser; double-click the divider to reset it (or focus it and use the arrow keys).
- **Acquire** (`/acquire`) starts, follows and stops an acquisition. A short explanation (save search pages into `input`; each page's jobs are stored; each new job's description is fetched) sits above a launch box with a colourful **Start acquisition** button and the last acquisition's state. Pushing the button hides the explanation (a "How it works" link brings it back) and shows a **progress pane** below the button: step 1 (pages read, job links stored), step 2 (job n of N with a progress bar, ok and failed counts, time left, the circuit breaker and its countdown) and the latest activity, newest first. Two more tabs show the **whole run** (every page and job, oldest first) and the **summary** (pages found, archived and kept with the reasons, job links stored, descriptions read, failed and not tried, stored in total and still to read, requests and time taken). The page reloads itself every 15 seconds while the acquisition runs; then it shows the summary and says how it ended (completed, or stopped and why); **Close** brings back the explanation. While a run is going, a red-outlined **Stop** button stops it: during step 1 nothing is stored and the pages stay in `input`, during step 2 it stops after the current job (at once while waiting). Errors (configuration, database, files, a process that died, an API that is not running) show in a red pane at the end, with the error's message and type.

It never touches the database: it only calls the API (`JobCatalog` for the jobs, `JobFetcher` to start an acquisition, `Journal` for its progress), at `JobCatalogApi:BaseUrl` in `src/JobFetchProbe.Web/appsettings.json` (`http://localhost:5081`). If the API is not running, it says so. Reload Overview to see new jobs arrive.

## API

```sh
dotnet run --project src/JobFetchProbe.Api
```

Listens on <http://localhost:5081> (Visual Studio: set `JobFetchProbe.Api` as the startup project). `src/JobFetchProbe.Api/JobFetchProbe.Api.http` has ready-made requests for Visual Studio or VS Code.

| Request | Returns |
| --- | --- |
| `POST /api/JobFetcher/stop` | `202` and the acquisition started by the API stops: during step 1 nothing is stored (the pages stay in `input`), during step 2 after the current job. `409` if nothing runs here |
| `POST /api/JobFetcher` | `202` and the run starts in the background: step 1, then step 2 (body: `state` `Started`, `at`, `message`; `Location: /api/Journal/runs`). `409` problem details if an acquisition is already running. See [Acquisition from the API](#acquisition-from-the-api) |
| `GET /api/JobCatalog?page=1&pageSize=25` | `200` with a `JobSummaryPage`: `jobs` (jobId, title, company, location, posted, status), `page`, `pageSize`, `total`, `totalPages`. Newest job IDs first. `page` defaults to 1, `pageSize` to 25 (1–100); a page past the end returns the last page. `400` with the errors if a value is out of range |
| `GET /api/JobCatalog/{jobId}` | `200` with a `StoredJob`: url, title, company, location, posted, employment type, `details` (label/value), description, status, last error, first seen, read and last attempt times. `404` if the job is not in the database |
| `GET /api/Journal/runs?take=10` | `200` with the newest runs of both steps (step, run id, start, last sign of life, end, state, message). `take` 1–50. See [Journal](#journal) |
| `GET /api/Journal/job-listing/runs/{id}?after={entryId}` | `200` with a step 1 run (where it stands) and its entries after `entryId` (0: all). `404` if unknown |
| `GET /api/Journal/job-details/runs/{id}?after={entryId}` | `200` with a step 2 run (where it stands, circuit breaker included) and its entries after `entryId`. `404` if unknown |
| `GET /openapi/v1.json` | The OpenAPI description of the API (Development only) |

Errors are returned as standard problem details. The database and input folder are under `Paths:DataRoot` in the API's `appsettings.json`. Since it runs acquisitions, the API opens the database **read-write**, and creates or updates it at start-up (`Database: …` in its log). A `503` ("No database yet") can therefore only happen if the file is removed while the API runs. The web app gets its jobs only from the two `JobCatalog` endpoints. Enum values (states, steps…) are sent as their names.

### Acquisition from the API

`POST /api/JobFetcher` runs the acquisition (`IAcquisitionService`: step 1, then step 2) in the background inside the API process: the request is answered at once, because a run can take minutes or hours (pauses, cooldowns).

- **Progress**: in the journal (`GET /api/Journal/runs`) and in the API's window, one line per page, job and circuit breaker change.
- **One at a time**: a second `POST` gets `409` while a run is going. The check uses the journal, so it also sees a run of another instance of the API. A process killed in the middle counts as running until its run is Abandoned (2 minutes without a sign of life). Two starts within the same second or two could still both go ahead; a lock file would close that gap (backlog).
- **Stopping it**: `POST /api/JobFetcher/stop` (the web page's Stop button), or stopping the API, stops the run after the current page or job; its state ends `Stopped`.
- **Errors** the API cannot handle are answered with `500` problem details holding the error's message and type (`errorType`); the web page shows them in its red error pane.
- **Settings**: see [Configuration](#configuration). A bad value stops the API at start-up with `Configuration error: …` (exit code 2).

## Journal

While a step runs, it records what it is doing in the **journal**, four tables in the same database (see [`docs/database.md`](docs/database.md)). Anyone else can read it at the same time through the API, without touching the running process:

```text
 API (fetching)                                         a reader (the web page)
 step 1 / step 2  ──progress──►  journal recorder  ──►  journal tables  ◄──  JournalController ◄── IJournalService
 (unchanged)                      (queued, in order)      (SQLite)            GET api/Journal/...
```

- **One run per step.** Each start of step 1 and step 2 is a run (the API's window shows `Journal: run 12`). Its row says where it stands: state, counters, circuit breaker, and `LastSeenAt`, a sign of life refreshed at least every 30 seconds. Its entries say what happened, in order: each page (step 1); each job and each circuit breaker change (step 2).
- **Following a run.** `GET api/Journal/runs` gives the newest runs. Then ask `GET api/Journal/<step>/runs/<id>?after=<last entry id>` every few seconds: you get the current state and only the new entries. Stop when the state is no longer `Running`: `Completed`, `Stopped` (circuit breaker gave up, or Stop), `Failed` (the error is in the message) or `Abandoned`.
- **Abandoned.** If a run shows no sign of life for 2 minutes, the process ended without saying so (closed, killed, crashed): the API reports it as `Abandoned` with the time of the last sign of life, and the next run of that step also writes it into the database.
- **Never in the way.** The fetch does not know about the journal: the acquisition passes its progress to the recorder as well as to the log. Journal writes happen in the background, in order; if one fails, it is logged once and skipped, and the fetch carries on. If the journal cannot be started at all, the log says `Journal: not recorded this time` and the run goes ahead.
- **Size.** Each step keeps its last 50 runs; older ones are deleted when a new run starts. This is to be reviewed (see `docs/backlog.md`).

The isolation shows in the folders: `Models/Journal`, `Services/Journal` (with `Services/Abstractions/Journal`), `Data/Journal` and `Api/Controllers/JournalController`. Nothing in the step folders (`FetchJobListing`, `FetchJobDetails`) refers to the journal.

## Folders

Code and data are kept apart. The repository holds only what belongs in Git; everything the app reads or writes lives in one data folder next to it:

```text
C:\Code\job-fetcher\
  job-fetch-probe\                 ← the repository: code and documents only (this is what goes to GitHub)
    docs\                          plan, spec and database diagram
    src\                           the six projects (Web, Api, Services, Models, Infrastructure, Data)
    .gitignore  CLAUDE.md  Directory.Build.props  JobFetchProbe.sln  JobFetchProbe.slnLaunch  nuget.config  README.md

  job-fetch-probe-data\            ← your data: never committed, never touched by Git
    database\
      jobs.db                      every job, listing and description
    input\                         ← save search result pages here
      archived\                    pages already read (moved here automatically)
```

- **Your data is safe from Git.** `git clean -fdx`, or deleting and re-cloning the repository, cannot touch the database or your saved pages.
- **One place to back up.** Copy `job-fetch-probe-data` while the API is closed.
- **One setting.** All of these paths come from `Paths:DataRoot` in the API's `appsettings.json` (the web app has no data paths, it asks the API). Change that line to move all your data. The API creates `input`, `input\archived` and the database when it starts.
- **Individual overrides still work.** `FetchJobListing:Folder`, `FetchJobListing:ArchiveFolder` and `Database:Path` can each be set on their own; they then win over `DataRoot`.

## The database

`jobs.db` is created when the API first starts and upgraded automatically when a new version changes its tables (EF Core migrations). Open it with [DB Browser for SQLite](https://sqlitebrowser.org/) or a SQLite extension for VS Code. For example:

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

The tables are defined in `src/JobFetchProbe.Data` (one entity and one configuration class per table). After changing them, add a migration from the repository folder; the API applies it on its next start:

```sh
dotnet ef migrations add <Name> --project src/JobFetchProbe.Data --startup-project src/JobFetchProbe.Api --output-dir Migrations
```

## Configuration

`src/JobFetchProbe.Api/appsettings.json`:

| Setting | Value | Meaning |
| --- | --- | --- |
| `Paths:DataRoot` | `C:\Code\job-fetcher\job-fetch-probe-data` | Where all data lives (see [Folders](#folders)) |
| `FetchJobDetails:DelaySeconds` | `45` | Pause between two requests (1–120) |
| `FetchJobDetails:CircuitBreaker:FailureThreshold` | `3` | Network or server errors in a row that open the circuit |
| `FetchJobDetails:CircuitBreaker:CooldownSeconds` | `210` | First wait while the circuit is open |
| `FetchJobDetails:CircuitBreaker:MaxCooldownSeconds` | `1800` | The wait doubles up to this |
| `FetchJobDetails:CircuitBreaker:StopAfterOpenings` | `3` | The run stops when the circuit opens this many times |
| `HttpClient:TimeoutSeconds` | `20` | Request timeout |
| `HttpClient:UserAgent` | `JobFetchProbe/0.3 (personal study project)` | How the app identifies itself |
| `HttpClient:HandlerLifetimeMinutes` | `5` | How long the client factory reuses a connection handler before rotating it |

Any setting can be overridden with an environment variable, using `__` for `:`, e.g. `Paths__DataRoot=D:\job-data`. (The sections were called `Inbox` and `Details` before 16 Oct 2026; overrides with the old names are ignored.)

## Structure

A picture of the whole thing, layers and one acquisition step by step: [`docs/architecture.svg`](docs/architecture.svg). The same acquisition as UML sequence diagrams: [`docs/sequence.md`](docs/sequence.md).

```text
Web ──HTTP──► Api ──► Services (IJobRetriever) ──► IJobCatalog ◄── Data                       (reading jobs)
               Api ──► Services (IJournalService) ──► IJournalRepository ◄── Data             (journal)
               Api ──► Services (IAcquisitionRunner → IAcquisitionService: step 1 → step 2)   (acquisition)
                          │  ▲  ▲
                          │  │  └── Data             (SQLite: implements the repository interfaces)
                          │  └───── Infrastructure   (HTTP, saved pages, input folder: implements the Services interfaces)
                          └──► Models
               Api/Program.cs ──► Data, Infrastructure                                        (registration only)
```

Services defines the interfaces it needs (`Services/Abstractions`); Infrastructure and Data implement them, so Services never touches HTTP, files or the database itself. Everything follows Web → Api → Services → (Data | Infrastructure): the web app calls the API over HTTP and references only Models (the DTOs in the JSON); the API's controllers use only the service layer (`IJobRetriever`, `IJournalService`, `IAcquisitionRunner`); the order of the steps is in Services (`IAcquisitionService`); only the API's `Program.cs` refers to Infrastructure and Data, to register them.

| Project | Role | Main files |
| --- | --- | --- |
| `JobFetchProbe.Models` | Data shapes shared by all layers. No dependencies | `Jobs/` (`JobPosting`, `JobResult` …), `Search/` (`JobListing`, `JobSearch`, `SearchResult`), `FetchJobListing/` (step 1: `JobSearchBatch`, `JobListingFetchResult` …), `FetchJobDetails/` (step 2: `JobDetailsFetchResult`, `JobDetailsFetchEvent` …), `Acquisition/` (`AcquisitionResult`, `AcquisitionStart`), `Catalog/` (`JobSummary`, `JobSummaryPage`, `StoredJob`: what the web app shows), `Journal/` (the journal's runs, entries and states) |
| `JobFetchProbe.Services` | The two steps, reading stored jobs for the API, and the interfaces they need | `Catalog/JobRetriever` (`IJobRetriever`, used by the API), `FetchJobListing/` (step 1: `JobListingFetcher`, `SearchService` + `SearchPageParser` reading saved search pages), `FetchJobDetails/` (step 2: `JobDetailsFetcher` with its helpers `FetchOutcome`, `TimeLeftEstimate`, `JobDetailsFetchState`; its settings, `CircuitBreaker`, `JobService` + `JobPageParser` reading a job page), `Acquisition/` (`AcquisitionService`: step 1 → step 2, with the journal; `IAcquisitionObserver`; `AcquisitionRunner`: runs it in the background for the API; `LoggingAcquisitionObserver`), `Parsing/` (shared by both: `JobIdResolver`, `HtmlText`, and the helpers `HtmlQuery`, `UrlHelper`, `JsonLd`), `Abstractions/`, `Journal/` (`JournalService`: all journal operations; the run recorders, `RunRecorderCore` shared by both, and their write queue), `Abstractions/Journal/` (`IJournalRepository`) |
| `JobFetchProbe.Infrastructure` | HTTP and files | `Http/HttpPageFetcher`, `Files/SavedPageReader`, `FetchJobListing/InputFolder` and settings, `DependencyInjection` (`AddInfrastructure`) |
| `JobFetchProbe.Data` | The data layer: the SQLite database (EF Core) | `JobsDbContext`, `Entities/` (table rows), `Configurations/` (one per table), `Repositories/` (`SqliteBatchRepository`, `SqliteJobRepository`, `SqliteJobCatalog`), `Migrations/`, `JobRowMapping`, `DatabaseInitializer`, `Configuration/DataPaths`, `DependencyInjection` (`AddJobStore` read-write, for the API; `AddJobCatalog` read-only, for an app that only reads); `Journal/` (its own `Entities/`, `Configurations/`, `Repositories/SqliteJournalRepository`) |
| `JobFetchProbe.Web` | Web page over the API (Razor Pages) | `Program`, `Api/` (`JobCatalogClient`, `JobFetcherClient`, `JournalClient`: typed clients for the three controllers; `ApiUnavailableException`), `Pages/Index` (table, pagination and details pane), `Pages/Acquire` (start and stop buttons, the acquisition pane with its Progress, Whole run and Summary tabs, the red error pane; `AcquisitionView` works out what to show from the journal), `Pages/Job` (redirect of old links), `Pages/Shared/_Layout` (menu), `wwwroot/css/site.css`, `wwwroot/js/split.js` (divider and clickable rows), `wwwroot/img/` (logo), `appsettings.json` |
| `JobFetchProbe.Api` | REST API over the database: reads jobs and the journal, starts acquisitions (ASP.NET Core controllers, OpenAPI) | `Program` (the only file that knows Data and Infrastructure), `Controllers/JobFetcherController` (uses `IAcquisitionRunner`), `Controllers/JobCatalogController` (uses `IJobRetriever`), `Handlers/JobStoreUnavailableHandler` (503), `Handlers/UnexpectedErrorHandler` (500 with the error's type and message), `JobFetchProbe.Api.http`, `appsettings.json`; `Controllers/JournalController` (reads the journal through `IJournalService`), `Handlers/JournalUnavailableHandler` |

Each file holds one type and is named after it. The database rows (`Data/Entities`, suffix `Row`) are kept apart from the shared models on purpose: the models (DTOs) describe what the app works with, the rows how SQLite stores it. Rows never leave the Data project; its repositories return models, so the tables can change without touching the other projects.

### HTTP client

The client is a **typed client** registered with `AddHttpClient<IPageFetcher, HttpPageFetcher>()`. `IHttpClientFactory` creates the `HttpClient` instances and pools the underlying connection handlers, rotating them every `HandlerLifetimeMinutes`. This avoids the two classic problems of `new HttpClient()`: running out of sockets when clients are created and disposed often, and stale DNS when one client lives forever. `HttpPageFetcher` never creates or disposes an `HttpClient`, and the services that use it are transient and resolved in a scope per step, so no long-lived object pins a handler.

### Page layouts

`JobPageParser` tries three layouts in order: LinkedIn's public job page (`topcard__title`, `topcard__org-name-link`, `description__job-criteria-*`, `show-more-less-html__markup`); schema.org `JobPosting` data embedded for search engines; then the page title and visible text. The LinkedIn class names come from one saved public job page (October 2026). If LinkedIn changes its markup, the app falls back to the generic layout; check a fresh saved page and update the class names.

## Documents

- [`docs/plan.md`](docs/plan.md): goal, scope, boundary, design decisions, limitations.
- [`docs/spec.md`](docs/spec.md): what the code implements, project by project.
- [`docs/database.md`](docs/database.md): the tables and how they relate, with a diagram (`docs/database.svg`).
- [`docs/sequence.md`](docs/sequence.md): UML sequence diagrams (Mermaid, editable as text) of an acquisition started from the API, and of following a run through the journal; rendered as `docs/acquisition-sequence.svg` and `docs/journal-sequence.svg`.
- [`docs/architecture.svg`](docs/architecture.svg): how the processes interact — the layers and who calls whom, and one acquisition step by step, with the places that are not idempotent yet (backlog section 5).
- [`docs/backlog.md`](docs/backlog.md): recommendations and open items kept for later.
- [`CLAUDE.md`](CLAUDE.md): how changes are made in this repository (plan first, then "go") and the architecture rules.

## History

The earlier probe version (crawler-policy check, tests, plan and spec) is kept in Git history under the tag `probe-v1`. Earlier versions of this app were a console app (removed on 18 Oct 2026, once the web page could do everything it did), and also had an interactive prompt, a single-job command, a search-by-link command and JSON-file storage; they were removed to keep one flow, and are in the Git history.
