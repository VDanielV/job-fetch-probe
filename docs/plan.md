# Plan: JobFetchProbe

Personal study project. Local only. One operator, one machine.

Updated: 14 Oct 2026. Replaces the original probe plan, which is kept in Git history under the tag `probe-v1`.

## Goal

Read a single job posting, given either as a link or as a page saved from the browser, and keep its content as a structured JSON file that can be reopened, compared or processed later.

Second step (added 14 Oct 2026): read a search results page, given the same two ways, and keep the list of job links it shows in one JSON file per search, so the operator can choose which jobs to open.

Third step (added 14 Oct 2026): fetching search pages by link proved unreliable (bot checks), so the main route for search results is an inbox. The operator saves result pages into one folder; the app reads them all, one by one, merges their jobs into one timestamped JSON file, and archives the pages it read. Saving several pages (more scrolling, other filters) and merging them is also how to get more results than one page shows.

Fourth step (added 14 Oct 2026): once the inbox has written a listing, the app reads the listed jobs one by one with the job step's code and keeps all their descriptions in one JSON file, then prints a summary with the totals. It is slow on purpose and guarded by a circuit breaker, and it can be stopped and resumed.

Storage (added 15 Oct 2026): jobs, listings, searches and descriptions can be kept in one SQLite database instead of JSON files (`Storage:Provider`). It keeps one record per job across all listings, so no job is fetched twice, and makes the data easy to query. JSON stays available with one setting. PostgreSQL in Docker was considered and left for later.

Folders (added 15 Oct 2026): code and data are separated. The repository holds code and documents only; everything the app reads or writes lives under one data folder next to it (`Paths:DataRoot`, `C:\Code\job-fetch-probe-data`): `database`, `input` (with `archived`), `pages`, and `output` for JSON storage. The first probe version left the repository and is kept as the Git tag `probe-v1`.

## What changed from the first plan

The first plan was an experiment: does a plain .NET client retrieve a usable LinkedIn job description? It had three phases (read `robots.txt`, parse a local fixture, send one live request) and strict per-run limits.

That experiment answered itself early:

- LinkedIn's `robots.txt` disallows automated access to job pages. A general-purpose fetch tool refused the job URL for that reason.
- The public (anonymous) job page, saved from a private browser window, does contain the full job: title, company, location, criteria and description. It has no embedded schema.org data, so it needs its own parsing step.

The project then became a small tool rather than an experiment. The policy check, per-run limits, report log and test suite were archived.

## Scope

| In scope | Out of scope |
| --- | --- |
| One job at a time, from a link or a saved page | Requesting further result pages (pagination, `/jobs-guest/` endpoints) |
| One search results page at a time: the job links it shows, one JSON file per search | Reading jobs in parallel, or faster than one per `Details:DelaySeconds` |
| Many saved search pages at once from an inbox folder, merged into one `yyyyMMdd_HHmmss.json` | Watching the folder continuously (it is read at start-up and on demand) |
| The descriptions of every job in a listing, one request at a time, in one JSON file | Getting past a refusal: the run stops and waits for the operator |
| Everything in one local SQLite file (EF Core), or JSON files, by setting | A database server; importing old JSON files into the database |
| Title, company, location, posted, type, details, description | Recruiter names, contact details, profiles |
| One JSON file per job, named after the job ID | Databases, scheduled runs, alerts |
| LinkedIn public job page layout, schema.org `JobPosting`, generic fallback | Login, cookies, browser automation, applying to jobs |

## Boundary

The app identifies itself honestly, sends no cookies, and makes one attempt per link. If the site refuses the request (401, 403, 429, 999) or redirects to a sign-in page, the app reports that and stops. It does not retry, change identity, imitate a browser, or try any other way around the block.

When a live link is refused, the supported route is to open the job in a browser, save the page, and give the app the file.

The details step sends many requests, so it is stricter still: one request at a time, a pause of `Details:DelaySeconds` between them, and a circuit breaker. A single refusal opens the circuit; nothing is sent until the cooldown has passed, then one trial request decides whether to go on. After `StopAfterOpenings` openings the run stops, and the remaining jobs wait for a later run. LinkedIn's `robots.txt` disallows automated access to job pages, so in practice expect the circuit to open early; the details step is meant for small listings and sites that allow it.

The same applies to the search step. LinkedIn may answer a plain request for a search with a bot-check page (a meta refresh and a script that sets a challenge cookie). The app recognises that page and reports it; it never runs or solves the script. For more results than the page shows, the operator scrolls in the browser before saving; the app does not request further result pages. LinkedIn's terms prohibit unauthorized automated copying; the tool is meant for reading individual postings the operator has opened, not for collecting them.

## How it works

1. The operator gives a link or a file path at the prompt, or as a single argument.
2. **Link:** the page is downloaded with a factory-managed `HttpClient`. A refusal or sign-in redirect stops here.
   **File:** the saved page is read; a Chrome view-source save is unwrapped first.
3. The job's address (`SourceUrl`) is fixed: the link given, or the saved page's canonical link / `og:url`. A saved page without its own address is rejected.
4. The job ID is taken from the number at the end of the `/jobs/view/` path.
5. The page is parsed with the first layout that matches: LinkedIn public page, schema.org JSON-LD, generic fallback.
6. The job is printed and written to `<Output.Folder>/<JobId>.json` (by default `<DataRoot>\output\jobs`; with Sqlite storage, a row in `Jobs`).

Search step (`search [<link | file>]`; no argument uses `Search:DefaultUrl`):

1. The page is downloaded or read, exactly as in the job step.
2. The result cards are read (LinkedIn search layout, else any `/jobs/view/` links). Duplicates are dropped and tracking parameters removed from each link.
3. A bot-check page with no results is reported, not saved.
4. The search is named after its address (`net-empleos-valencia`) and written to `<Search.OutputFolder>/<SearchId>.json` (by default `<DataRoot>\output\searches`; with Sqlite storage, a row in `Searches`).


Inbox step (at every start when `Inbox:ProcessOnStart` is true, or with `inbox`):

1. Every saved page in `Inbox:Folder` (`C:\Code\job-fetch-probe-data\input`) is read, oldest first, exactly as the search step reads a saved page.
2. Their jobs are merged without duplicates.
3. The result is written to `<Inbox:OutputFolder>/<yyyyMMdd_HHmmss>.json`.
4. Each page read is moved to `Inbox:ArchiveFolder` (`C:\Code\job-fetch-probe-data\input\archived`); pages that could not be read stay in the inbox with the reason shown.
5. If a listing was written and `Details:RunAfterInbox` is true, the details step starts on it.

Details step (after the inbox, or with `details [<listing>]`; no argument takes the newest listing):

1. Jobs already in the descriptions file from an earlier run are skipped; the others are read one by one with the job step's fetch and parser, with a pause between requests.
2. Every result is classified for the circuit breaker: read (success), refused (opens the circuit), network or server error (opens it after a few in a row), 404 (neutral).
3. The descriptions file `<Details:OutputFolder>/<BatchId>.json` is rewritten after every job.
4. The console shows each job with progress and time left, every circuit breaker change, and a countdown while it is open.
5. A summary prints the totals: pages found, read, archived and kept; jobs listed; descriptions stored, failed and pending; requests, time and circuit openings.

## Design decisions

| Decision | Reason |
| --- | --- |
| SQLite, not a server database | No installation, no Docker, survives restarts like any file, backed up by copying one file; plenty for one user |
| EF Core with migrations | The schema can evolve safely; moving to PostgreSQL later means another provider package and a new migration, not new code |
| Database classes only in Infrastructure, behind the existing interfaces | Services, Models and the console steps did not change; `AddStorage` swaps the registrations |
| JSON kept as a provider | Files can be opened in Notepad and diffed in Git; switching back is one setting |
| One row per job, cards stored per listing/search | No duplicate jobs or duplicate requests; each listing still shows exactly what its pages showed |
| Data folder outside the repository, one `Paths:DataRoot` setting | Git can never delete the database or saved pages; one folder to back up; one line to move it all. Individual folder settings still override |
| Database created and migrated at start-up | Nothing to run by hand; a new migration is applied on the next start |
| `IDbContextFactory`, one context per call | Contexts are short-lived and never shared, the same reasoning as the HTTP client factory |
| Four projects: Console, Services, Models, Infrastructure | Separates the user interface, the parsing logic and the outside world; Services never touches HTTP or disk directly |
| Typed `HttpClient` from `IHttpClientFactory` | Pooled, rotated handlers: no socket exhaustion, no stale DNS, no manual disposal |
| `IJobService` transient, one DI scope per request | Nothing long-lived holds a client and pins a handler |
| `appsettings.json` instead of `app.config` | Standard configuration for modern .NET; binds to options classes and supports environment-variable overrides |
| `SourceUrl` and `JobId` are `required`; `SourceUrl` is a `Uri` | A saved job must always be reachable again |
| Regex-based HTML reading, no parser package | Keeps the dependency list short; acceptable for three known layouts |
| One file per job, overwritten on re-read, no history | The latest read is the current state of the posting; earlier versions are not needed |
| Search step in its own classes and registration methods | Adds the feature without changing the job step's code paths |
| One file per search, named after the search, overwritten | Re-reading a search (for example a page scrolled further) replaces the list |
| Inbox batches named by time, never overwritten | Each run is a separate record; the archive shows which pages went into it |
| Archive only after the batch file is written | A failed write never loses track of a page: it stays in the inbox for the next run |
| Failed pages stay in the inbox | The operator sees the reason and can fix or remove the page |
| Details step reuses `IJobService` through a new `ReadUrlAsync` | Same client, parser and refusal rules as the job step; `FromUrlAsync` is unchanged in behaviour |
| Circuit breaker opens on the first refusal | A refusal is the site saying no; asking again soon would ignore it |
| The trial request is the job that opened the circuit | No job is skipped because of a temporary refusal |
| One descriptions file per listing, rewritten after each job, same `BatchId` | Progress survives a stop, a crash or Ctrl+C; the next run resumes |
| Progress through `IProgress<T>`, printed by the console classes | Services stay free of console code; the console shows what happens as it happens |

## Known limitations

- LinkedIn class names come from one saved page (October 2026). A markup change sends pages to the generic fallback until the names are updated.
- Live LinkedIn links are likely to be refused; saved pages are the dependable input.
- `Posted` is the text shown on the page (for example "Hace 23 horas"), not a date.
- On the LinkedIn layout, employment type and seniority appear under `details`, not in `employmentType`.
- The search step reads only the jobs in the page (100 of 197 in the saved example). The inbox step works around this by merging several saved pages.
- A view-source save of LinkedIn's signed-in search page contains only the selected job, not the list (the list is loaded later by scripts). The signed-out search page, or "Save page as → Webpage, Complete", is needed.
- LinkedIn search class names also come from one saved page (October 2026).
- The details step is likely to be refused by LinkedIn after a few requests (or at once); the circuit breaker then stops it. The descriptions file keeps whatever was read.
- Cooldowns are waited inside the app; closing the app during a cooldown is fine, the next run resumes.
- With `Sqlite`, existing JSON files are not imported; the database starts empty.
- Viewing the database needs a tool (DB Browser for SQLite, or a VS Code extension). Copy `jobs.db` only while the app is closed.
- There are no automated tests in the current build. The first probe (tag `probe-v1`) has tests for its own design, not for this one.

## Possible next steps

Each is optional and independent.

1. Add a test project with the saved page as a local fixture (kept out of Git) and a synthetic page for the committed tests.
2. Replace the regex HTML reading with a parser library such as AngleSharp if more layouts are added.
3. Parse `Posted` into a date relative to `RetrievedAt`.
4. Add a command that lists the saved jobs in the output folder.
5. From a saved search file, let the operator pick jobs to read with the job step one at a time.
6. Import the existing JSON files into the database, or export a listing from the database to JSON.
7. PostgreSQL in Docker with a named volume, using the same EF Core model (`Npgsql.EntityFrameworkCore.PostgreSQL`).
