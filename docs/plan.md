# Plan: JobFetchProbe

Personal study project. Local only. One operator, one machine.

Updated: 18 Oct 2026. Replaces the original probe plan, which is kept in Git history under the tag `probe-v1`.

## Goal

Collect job postings in one database, in one fixed flow (an **acquisition**) that runs each time the operator starts it from the web page:

1. **Saved search pages → job links.** The operator saves search result pages from the browser into an input folder. The app reads them, stores every job link they contain (URL, title, company), and archives the pages it read.
2. **Job links → job descriptions.** The app goes through every job without a description, one at a time, reads its page, and stores the description, details and status.
3. **Summary** with the totals, shown on the web page.

A **REST API** does the work: it runs acquisitions in the background (`POST api/JobFetcher`, stopped with `POST api/JobFetcher/stop`), records their progress in a journal, and returns the stored jobs as JSON (`JobCatalog`: one page of jobs, or one job in full). A **web app** is the operator's screen: it starts, follows and stops an acquisition, and shows what is stored (a paged table of all jobs with a details pane). It talks only to the API.

Until 18 Oct 2026 the acquisition ran in a **console app**; once the web page could do everything it did, the console app was removed.

## How it got here

The first plan was an experiment: does a plain .NET client retrieve a usable LinkedIn job description? It answered itself early:

- LinkedIn's `robots.txt` disallows automated access to job pages. A general-purpose fetch tool refused the job URL for that reason.
- The public (anonymous) job page, saved from a private browser window, does contain the full job: title, company, location, criteria and description. It has no embedded schema.org data, so it needs its own parsing step.

The project then grew step by step (14–15 Oct 2026): reading one job from a link or a saved page; reading a search results page; an inbox of saved search pages, because fetching search pages by link ran into bot checks; reading the descriptions of the listed jobs behind a circuit breaker; a SQLite database instead of JSON files; and a data folder outside the repository.

On 15 Oct 2026 it was **simplified to the one flow above**. The interactive prompt, the single-job command, the search-by-link command, the `details` command and JSON-file storage were removed, along with the code only they used. All of it remains in Git history.

## Scope

| In scope | Out of scope |
| --- | --- |
| Saved search result pages from an input folder, merged without duplicates | Requesting search result pages (pagination, `/jobs-guest/` endpoints) |
| The description of every job not read yet, one request at a time | Reading jobs in parallel, or faster than one per `FetchJobDetails:DelaySeconds` |
| Title, company, location, posted, type, details, description, status | Recruiter names, contact details, profiles |
| One local SQLite file (EF Core), created and migrated by the API at start-up | A database server, JSON files, imports and exports |
| LinkedIn public job and search page layouts, schema.org `JobPosting`, generic fallback | Login, cookies, browser automation, applying to jobs |
| One acquisition per press of Start, one at a time | Commands, a prompt, watching the folder, scheduled runs |
| A web page: menu, jobs table with pagination, details pane with a draggable divider; start, follow and stop an acquisition | Editing, filtering, searching or deleting jobs in the web page; logins |
| A REST API: a page of jobs, one job; the journal of the runs; starting and stopping an acquisition | Endpoints that change jobs, authentication |

## Boundary

The app identifies itself honestly, sends no cookies, and never tries to get around a refusal: it does not retry quickly, change identity, imitate a browser, or run or solve bot-check scripts.

Step 1 sends no requests at all: it reads pages the operator saved. For more results than one page shows, the operator scrolls in the browser or saves more pages.

Step 2 sends many requests, so it is strict: one request at a time, a pause of `FetchJobDetails:DelaySeconds` between them, and a circuit breaker. A single refusal opens the circuit; nothing is sent until the cooldown has passed, then one trial request decides whether to go on. After `StopAfterOpenings` openings the run stops, and the remaining jobs wait for a later start. LinkedIn's `robots.txt` disallows automated access to job pages, so in practice expect the circuit to open early; step 2 is meant for small numbers of jobs and for sites that allow it. LinkedIn's terms prohibit unauthorized automated copying; the tool is meant for reading postings the operator chose to collect, not for bulk collection.

## How it works

Step 1, **FetchJobListing** (`JobListingFetcher`):

1. Every saved page in `<DataRoot>\input` is read, oldest first. A Chrome view-source save is unwrapped first.
2. The page's address comes from its canonical link, `og:url` or the browser's "saved from" note; a page without one, a signed-in view-source save (only the selected job), a page without job links or a bot-check page is kept in `input` with the reason.
3. The result cards are read (LinkedIn search layout, else any `/jobs/view/` links); duplicates are dropped and tracking parameters removed.
4. All pages' jobs are stored together as one listing; new jobs get a `Pending` row.
5. Only then is each page read moved to `input\archived`.

Step 2, **FetchJobDetails** (`JobDetailsFetcher`):

1. The work list is every job `Pending`, `Blocked` or `Failed`: never-tried jobs first.
2. Each job page is read with a factory-managed `HttpClient` and parsed with the first layout that matches (LinkedIn public page, schema.org JSON-LD, generic fallback).
3. The result is stored at once: `Read` with the description and details, `Blocked` or `Failed` with the reason (tried again next time), or `Gone` for a 404/410 (not tried again).
4. Every result also feeds the circuit breaker: read (success), refused (opens the circuit), network or server error (opens it after a few in a row), 404 (neutral).
5. The web page shows each job with progress and time left, every circuit breaker change, and a countdown while it is open (from the journal); the API's log has one line per job and change.

Both steps record their progress in the journal while they run (see the README, "Journal"); the API reads it.

The summary (the web page's Summary tab) shows pages found, archived and kept; job links stored; descriptions read, failed and not tried; how many jobs are still to read; requests, time and circuit openings.

## Design decisions

| Decision | Reason |
| --- | --- |
| Web app as its own project, reading only through the API (`IJobCatalogClient`, two GET calls) | The web page never opens the database, so it can stay open while an acquisition runs. One source of the reading rules (the API). Razor Pages: a menu, the jobs table and a details pane in one page; selection lives in the address (`?job=`), so it works with Back, reload and bookmarks |
| A journal of every run, one pair of tables per step, written while the step runs and read through `api/Journal`; it does not record who started a run (always the API, since 18 Oct 2026) | Progress can be followed from another process (the web page), during the run and after a restart; the steps do not know about it (the acquisition feeds their progress to a recorder); writes are queued and never stop a fetch; a sign of life every 30 s, Abandoned after 2 minutes without one |
| Journal isolated in its own folders in every layer (`Models/Journal`, `Services/Journal`, `Data/Journal`, `JournalController`) | It can change, or be removed, without touching the steps; the folder structure shows the boundary |
| One small script (`split.js`) only for the draggable divider and clickable rows | Everything else is server-rendered HTML and CSS; without the script the page still works, only dragging stops |
| API as its own project with controllers | The only reader of the database for display; the web page uses it; OpenAPI describes it for clients. Read-write since it starts acquisitions |
| Stopping from the web (`POST api/JobFetcher/stop`): step 1 stops between pages and stores nothing, step 2 after the current job; the pane's tabs (Progress, Whole run, Summary) and a red error pane with the error's kind and message | Everything the console offered is on the web page, so the console could be retired (done 18 Oct 2026); stopping never leaves a half-stored listing; errors are explained where they are seen |
| The web app starts acquisitions with a button on the Acquire page (form post, then redirect) and follows them in a progress pane read from the journal, reloaded every 15 s without script | The web app still talks only to the API; the journal already holds everything the pane shows; a refresh never starts a second run |
| Acquisition from the API in the background (`AcquisitionRunner`, a hosted service), answered with 202; one at a time, checked through the journal | A run takes minutes or hours, an HTTP request cannot wait; the journal check also sees a run of another API instance, so the site is never asked twice as often; stopping the API stops the run cleanly |
| The API's controllers use a service (`IJobRetriever`), not the repository | Layers Api → Services → Data: the reading rules (page limits, job ID) live in Services; the data layer says whether the database exists; the controller only maps results to HTTP. A missing database is one exception turned into 503 by one handler |
| One flow, no commands or arguments | The work always happens in the same order; nothing to remember, nothing to choose wrongly |
| Step 2 works from the database, not from the listing just written | A stopped run resumes on the next start by itself, even with an empty input folder; no job is read twice |
| Status per job (`Pending`, `Read`, `Blocked`, `Failed`, `Gone`) | The database itself says what is done and what is left; `Gone` stops asking for pages that no longer exist |
| Each result stored as soon as it is known | A crash, Stop or a stop by the circuit breaker loses nothing |
| SQLite, not a server database | No installation, no Docker, survives restarts like any file, backed up by copying one file; plenty for one user |
| EF Core with migrations, applied at start-up | The schema can evolve safely and without manual steps; moving to PostgreSQL later means another provider package and a new migration |
| `IDbContextFactory`, one context per call | Contexts are short-lived and never shared, the same reasoning as the HTTP client factory |
| One row per job, cards stored per listing | No duplicate jobs or requests; each listing still shows exactly what its pages showed |
| Data folder outside the repository, one `Paths:DataRoot` setting | Git can never delete the database or saved pages; one folder to back up; one line to move it all |
| Database rows (`Database/Entities`, `…Row`) separate from the shared models | Models say what the app works with; rows say how SQLite stores it. Tables can change without touching Services or the apps; a small mapping class is the price |
| The two steps are named for what they do: **FetchJobListing** (step 1) and **FetchJobDetails** (step 2), in type, folder, namespace and settings names; code used by both lives in `Services/Parsing` | Each step reads as what it does, can be found and run on its own, and step 1 never depends on step 2's code (only on shared helpers) |
| One type per file; folders per step; one EF configuration class per table | Easy to find, small diffs; the C# convention (StyleCop SA1402) |
| Six projects: Web, Api, Services, Models, Infrastructure, Data (the Console project was removed on 18 Oct 2026) | Separates the apps, the logic and the outside world; Services never touches HTTP, files or the database directly |
| Data layer as its own project (`JobFetchProbe.Data`): context, entities, migrations, all three repositories | One project owns the database; Models stays DTOs only; the interfaces stay in `Services/Abstractions` (Services defines what it needs, Data implements it). Only the API reads it for display (the web page goes through the API) |
| Typed `HttpClient` from `IHttpClientFactory`; services that use it are transient | Pooled, rotated handlers: no socket exhaustion, no stale DNS, no manual disposal; nothing long-lived pins a handler |
| `appsettings.json` instead of `app.config` | Standard configuration for modern .NET; binds to options classes and supports environment-variable overrides |
| `SourceUrl` and `JobId` are `required`; `SourceUrl` is a `Uri` | A stored job must always be reachable again |
| Regex-based HTML reading, no parser package | Keeps the dependency list short; acceptable for the known layouts |
| Archive only after the listing is stored | A failed write never loses track of a page: it stays in `input` for the next run |
| Circuit breaker opens on the first refusal; the trial request is the job that opened it | A refusal is the site saying no; no job is skipped because of a temporary refusal |
| Progress through `IProgress<T>` and `IAcquisitionObserver`, written to the API's log and the journal | Services stay free of output code; the log and the web page show what happens as it happens |
| Repeated actions in Services as helpers (`Parsing/HtmlQuery`, `UrlHelper`, `JsonLd`; step 2's `FetchOutcome`, `TimeLeftEstimate`; the journal's `RunRecorderCore`) | Each parser keeps only its layouts and each recorder only its step; the helpers are pure functions, easy to read and to test |
| The order of the steps in Services (`IAcquisitionService`, told about progress through `IAcquisitionObserver`); step 2's settings in Services | Api → Services → (Data \| Infrastructure): only the API's `Program.cs` knows Data and Infrastructure |

## Known limitations

- LinkedIn class names come from one saved job page and one saved search page (October 2026). A markup change sends pages to the generic fallback until the names are updated.
- LinkedIn is likely to refuse step 2's requests after a few (or at once); the circuit breaker then stops the run, and later starts continue.
- `Posted` is the text shown on the page (for example "Hace 23 horas"), not a date.
- On the LinkedIn layout, employment type and seniority appear under details, not in `EmploymentType`.
- A saved search page holds only the jobs it shows (100 of 197 in the saved example); several saved pages are merged to get more.
- A view-source save of LinkedIn's signed-in search page contains only the selected job, not the list. The signed-out search page, or "Save page as → Webpage, Complete", is needed.
- Cooldowns are waited inside the app; closing it during a cooldown is fine, the next start continues.
- Viewing the database needs a tool (DB Browser for SQLite, or a VS Code extension). Copy `jobs.db` only while the app is closed.
- There are no automated tests in the current build. The first probe (tag `probe-v1`) has tests for its own design, not for this one.

## Possible next steps

Each is optional and independent.

1. Add a test project with the saved pages as local fixtures (kept out of Git) and synthetic pages for the committed tests.
2. Replace the regex HTML reading with a parser library such as AngleSharp if more layouts are added.
3. Parse `Posted` into a date relative to `RetrievedAt`.
4. PostgreSQL in Docker with a named volume, using the same EF Core model (`Npgsql.EntityFrameworkCore.PostgreSQL`).
