# JobFetchProbe

A .NET console app: give it a link to a job posting, or a page you saved from your browser. It prints the job's title, company, location, date, details and description, and stores them in a SQLite database (or as JSON files, if you prefer).

## Folders

The project keeps **code** and **data** apart. The repository holds only what belongs in Git; everything the app reads or writes lives in one data folder **next to it**, never inside it:

```text
C:\Code\
  job-fetch-probe\                 ← the repository: code and documents only (this is what goes to GitHub)
    docs\                          plan and spec
    src\                           the four projects (Console, Services, Models, Infrastructure)
    .gitignore  Directory.Build.props  JobFetchProbe.sln  nuget.config  README.md

  job-fetch-probe-data\            ← your data: never committed, never touched by Git
    database\
      jobs.db                      every job, listing, search and description (Storage:Provider = Sqlite)
    input\                         ← save search result pages here; the app reads them at start-up
      archived\                    pages already read (moved here automatically)
    pages\                         job pages you saved yourself, to give to the job step by path
    output\                        only with Storage:Provider = Json
      jobs\  listings\  descriptions\  searches\
```

Why this way:

- **Your data is safe from Git.** Commands such as `git clean -fdx`, or deleting and re-cloning the repository, cannot touch the database or your saved pages.
- **One place for everything you work with.** Saving pages, finding the database and making a backup all happen in `job-fetch-probe-data`. Back up that one folder (with the app closed) and you have everything.
- **One setting.** All of these paths come from `Paths:DataRoot` in `appsettings.json`:

  ```json
  "Paths": { "DataRoot": "C:\\Code\\job-fetch-probe-data" }
  ```

  Change that line to move all your data, for example to OneDrive or another drive. The app creates `input`, `input\archived`, `pages` and the database on its first start, and prints the data folder on every start.
- **Individual overrides still work.** A single folder can be put elsewhere by setting it on its own; it then wins over `DataRoot`. For example, `"Inbox": { "Folder": "D:\\Downloads\\job-pages" }` reads search pages from your downloads folder and keeps everything else under `DataRoot`.

| Setting (derived from `DataRoot` unless set) | Path |
| --- | --- |
| `Storage:Sqlite:Path` | `<DataRoot>\database\jobs.db` |
| `Inbox:Folder` | `<DataRoot>\input` |
| `Inbox:ArchiveFolder` | `<DataRoot>\input\archived` |
| `Output:Folder` (Json) | `<DataRoot>\output\jobs` |
| `Inbox:OutputFolder` (Json) | `<DataRoot>\output\listings` |
| `Details:OutputFolder` (Json) | `<DataRoot>\output\descriptions` |
| `Search:OutputFolder` (Json) | `<DataRoot>\output\searches` |

The first version of the probe used to be in an `archive` folder in the repository. It is kept in Git history under the tag `probe-v1` (`git checkout probe-v1` to look at it).

## Run

```sh
dotnet run --project src/JobFetchProbe.Console
```

```text
Paste a job link, or the path of a page you saved from your browser. Type 'exit' to quit.

Job link or file: C:\Code\job-fetch-probe-data\pages\job-4478111194.saved.html
Job ID:    4478111194
Title:     Técnico/a de Desarrollo
Company:   Improving Logistics & Consulting S.L.
Location:  Almussafes
...
Saved to C:\Code\job-fetch-probe-data\output\jobs\4478111194.json
```

You can also pass one link or file directly: `dotnet run --project src/JobFetchProbe.Console -- <job-link-or-file>`.

### The whole process: inbox → job descriptions → summary

Save search result pages from your browser into `C:\Code\job-fetch-probe-data\input`. Every time the app starts:

1. **Inbox:** it reads the pages one by one, stores all their jobs (link, title, company…), merged and without duplicates, as one listing named after the time (`20261014_183201`, in the database or as `output\listings\20261014_183201.json`), and moves each page it read to `input\archived`.
2. **Job descriptions:** it reads the listed jobs one by one, with a pause between requests and a circuit breaker, and stores each description as soon as it is read (in the database, or in one file with the same name, `output\descriptions\20261014_183201.json`). With the database, jobs read in an earlier listing are skipped.
3. **Summary:** it prints the totals.

Type `inbox` at the prompt, or run `dotnet run --project src/JobFetchProbe.Console -- inbox`, to do it again without restarting. `details` (or `details 20261014_183201`) runs only step 2, on the newest listing by default; jobs already stored are skipped, so it **continues where an earlier run stopped**. Ctrl+C stops after the current job and keeps the progress.

```text
===== Step 1: inbox =====
[18:32:01] Reading 2 saved page(s) from C:\Code\job-fetch-probe-data\input
[18:32:01] [1/2] 197 .net jobs in Valencia.html: 100 jobs (100 new)
[18:32:01] [2/2] view-source_….html: kept in the inbox. …
[18:32:01] Listing written: 100 unique jobs -> C:\Code\job-fetch-probe-data\output\listings\20261014_183201.json

===== Step 2: job descriptions =====
[18:32:02] Listing 20261014_183201.json: 100 jobs, 0 already stored, 100 to read.
[18:32:03] [   1/100]   1%  ok       4476446382  Senior .NET Developer · W Hunt España  (ok 1, failed 0, 99 to go, ~9m 54s left)
[18:32:09] [   2/100]   2%  REFUSED  4476446383  (ok 1, failed 1, 98 to go, ~9m 48s left)
[18:32:09] Circuit OPEN (1 of 3): es.linkedin.com refused the request for job 4476446383 …
[18:32:39]   … circuit open, trial request in 4m 30s

===== Summary =====
Inbox pages        2 found, 1 read, 1 archived, 1 kept in the inbox
Jobs listed        100 unique  ->  C:\Code\job-fetch-probe-data\output\listings\20261014_183201.json
Job descriptions   97 of 100 stored  (97 read in this run, 0 from earlier runs)
...
```

**Circuit breaker.** A refusal (401, 403, 429, 999 or a sign-in page) opens the circuit at once; 3 network or server errors in a row do too; a 404 only marks that job. While open, nothing is sent for 5 minutes (doubling each time, at most 30); then the same job is tried once. If it works, the run continues; at the third opening the run stops, and `details` later continues from there. LinkedIn is likely to refuse these requests, so expect the run to stop early.

- Pages that can't be read stay in `input` with the reason shown, and are tried again next time.
- To get more results than one page shows, save several pages (scroll further, or use different filters); they are merged.
- Use the signed-out search page (a private window) and "Save page as". A view-source save of the signed-in search page only contains the selected job, because LinkedIn loads the list afterwards.

### Search results

To list the jobs in a search results page, use `search`:

```text
Job link or file: search C:\Code\job-fetch-probe-data\pages\197 .net jobs in Valencia.html
Search:    .net Jobs in Valencia
Search ID: net-empleos-valencia
Jobs:      100 in this page (the site reports 197 in total)
...
Saved to C:\Code\job-fetch-probe-data\output\searches\net-empleos-valencia.json
```

- `search` alone uses `Search:DefaultUrl` from `appsettings.json`.
- From the command line: `dotnet run --project src/JobFetchProbe.Console -- search [<link-or-file>]`.
- Each job is stored with its ID, clean link, title, company, location and listing date, one file per search, overwritten on re-read.
- Only the jobs in the page are read; the app does not request further result pages. Scroll further in the browser before saving to include more.
- If LinkedIn answers with its bot-check page instead of results, the app says so and saves nothing.

### Saved pages

Sites such as LinkedIn often refuse plain requests. When that happens, open the job in your browser (a private window shows what an anonymous visitor gets), save it, and give the app the file path instead of the link. Both a normal "Save page as" file and a Chrome **view-source** save (Ctrl+U, then Ctrl+S) work. Save them in `C:\Code\job-fetch-probe-data\pages`.

Every saved job keeps the address it can be reached at (`sourceUrl`). For a link, that is the link you gave; for a saved page, it is the page's own canonical link or `og:url`. A saved page without either is rejected, because the job could not be found again.

## Storage: database or JSON files

`Storage:Provider` in `appsettings.json` decides where everything is kept:

- **`Sqlite`** (set in `appsettings.json`): one database file, `<DataRoot>\database\jobs.db`. Created at the first start, and upgraded automatically when a new version of the app changes its tables. Each job is stored once, however many listings or searches show it, so a job already read is never requested again.
- **`Json`**: the files described below, in `<DataRoot>\output`, as before. The code default if the setting is missing.

The first console line says which one is in use. The database survives restarts like any other file.

Looking at the data: open `jobs.db` with [DB Browser for SQLite](https://sqlitebrowser.org/) (or a SQLite extension for VS Code). For example:

```sql
-- jobs still to read, or that failed, with the reason
SELECT JobId, Status, Title, Company, LastError FROM Jobs WHERE Status <> 'Read';

-- what one listing contained
SELECT lj.Position, lj.JobId, lj.Title, lj.Company, j.Status
FROM ListingJobs lj JOIN Jobs j ON j.JobId = lj.JobId
WHERE lj.BatchId = '20261014_183201' ORDER BY lj.Position;
```

Backup: copy `jobs.db` while the app is closed. Existing JSON files are not imported into the database.

Tables: `Jobs` (one per job: listing data, page data, description, status `Pending` / `Read` / `Blocked` / `Failed`, last error), `JobDetails`, `Listings` with `ListingSources` and `ListingJobs`, `Searches` with `SearchJobs`. See [`docs/spec.md`](docs/spec.md#storage).

## Configuration

`src/JobFetchProbe.Console/appsettings.json`:

| Setting | Value | Meaning |
| --- | --- | --- |
| `Paths:DataRoot` | `C:\Code\job-fetch-probe-data` | Where all data lives; see [Folders](#folders) for the folders derived from it |
| `Storage:Provider` | `Sqlite` | `Sqlite` (database) or `Json` (files) |
| `Inbox:ProcessOnStart` | `true` | Run the whole process every time the app starts |
| `Details:DelaySeconds` | `5` | Pause between two requests (1–120) |
| `Details:RunAfterInbox` | `true` | Read the job descriptions after the inbox writes a listing |
| `Details:CircuitBreaker:FailureThreshold` | `3` | Network or server errors in a row that open the circuit |
| `Details:CircuitBreaker:CooldownSeconds` | `300` | First wait while the circuit is open |
| `Details:CircuitBreaker:MaxCooldownSeconds` | `1800` | The wait doubles up to this |
| `Details:CircuitBreaker:StopAfterOpenings` | `3` | The run stops when the circuit opens this many times |
| `Search:DefaultUrl` | `https://es.linkedin.com/jobs/%2Enet-empleos-valencia` | Search used when `search` is given no link or file |
| `HttpClient:TimeoutSeconds` | `20` | Request timeout |
| `HttpClient:UserAgent` | `JobFetchProbe/0.3 (personal study project)` | How the app identifies itself |
| `HttpClient:HandlerLifetimeMinutes` | `5` | How long the client factory reuses a connection handler before rotating it |

Any setting can be overridden with an environment variable, using `__` for `:`, e.g. `Paths__DataRoot=D:\job-data`. The folder settings in [Folders](#folders) can also be set one by one; a relative path starts from the folder you run the app in.

## Output (JSON files)

With `Sqlite` storage the same fields are columns of the `Jobs` table.


With `Json` storage, the job step writes one file per job, named after the job ID (the number at the end of the `/jobs/view/` link, or of the page's canonical link for saved files). Reading the same job again overwrites its file; no previous versions are kept. If no ID can be found, the name is `job-<date>-<time>`.

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
| `JobFetchProbe.Models` | Data shapes shared by all layers. No dependencies | `JobPosting`, `JobDetail`, `FetchedPage`, `JobResult`; search: `JobSearch`, `JobListing`, `SearchResult`; inbox: `JobSearchBatch`, `InboxRunResult`; details: `JobDetailsBatch`, `DetailsRunResult` |
| `JobFetchProbe.Services` | Business logic and the interfaces it needs | `JobService`, `Parsing/JobPageParser`, `Parsing/JobIdResolver`, `Abstractions/IPageFetcher`, `ISavedPageReader`, `IJobRepository`, `ISearchRepository`; search: `Search/SearchService`, `SearchPageParser`, `SearchIdResolver`; inbox: `Inbox/InboxService`, `Abstractions/IInboxFolder`, `IBatchRepository`; details: `Details/DetailsService`, `CircuitBreaker`, `Abstractions/IListingStore`, `IDetailsRepository` |
| `JobFetchProbe.Infrastructure` | HTTP, files and JSON output | `Http/HttpPageFetcher` (typed client from `IHttpClientFactory`), `Files/SavedPageReader`, `Storage/JsonJobRepository`, settings classes, `DependencyInjection`; search: `Search/JsonSearchRepository`, `SearchSettings`; inbox: `Inbox/InboxFolder`, `JsonBatchRepository`, `InboxSettings`; details: `Details/JsonListingStore`, `JsonDetailsRepository`, `DetailsSettings`; database: `Database/JobsDbContext`, `Sqlite…Repository`, `SqliteListingStore`, `Migrations/`, `Storage/StorageSettings` |
| `JobFetchProbe.Console` | Entry point: configuration, dependency injection, prompt | `Program`, `JobConsole`, `SearchConsole`, `InboxConsole`, `DetailsConsole`, `PipelineConsole`, `ConsoleLog`, `appsettings.json` |

### HTTP client

The client is a **typed client** registered with `AddHttpClient<IPageFetcher, HttpPageFetcher>()`. `IHttpClientFactory` creates the `HttpClient` instances and pools the underlying connection handlers, rotating them every `HandlerLifetimeMinutes`. This avoids the two classic problems of `new HttpClient()`: running out of sockets when clients are created and disposed often, and stale DNS when one client lives forever. `HttpPageFetcher` never creates or disposes an `HttpClient`, and `IJobService` is transient and resolved in a new scope for each request, so no long-lived object pins a handler.

The client sends no cookies and makes one attempt per request. If the site refuses the request (401, 403, 429, 999) or redirects to a sign-in page, the app says so and stops; it does not retry or try to get around the block.

### Page layouts

`JobPageParser` tries three layouts in order: LinkedIn's public job page (`topcard__title`, `topcard__org-name-link`, `description__job-criteria-*`, `show-more-less-html__markup`); schema.org `JobPosting` data embedded for search engines; then the page title and visible text. The LinkedIn class names come from one saved public job page (October 2026). If LinkedIn changes its markup, the app falls back to the generic layout; check a fresh saved page and update the class names.

## Documents

- [`docs/plan.md`](docs/plan.md): goal, scope, boundary, design decisions, limitations.
- [`docs/spec.md`](docs/spec.md): what the code implements, project by project.

## History

The earlier probe version (crawler-policy check, tests, plan and spec) is kept in Git history under the tag `probe-v1`. It is not part of the build.
