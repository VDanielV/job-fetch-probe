# Backlog

Recommendations and open items, kept so they can be picked up later. Nothing here is implemented unless it says so.
Each item follows the working agreement in `CLAUDE.md`: a plan first, changes only after "go".

Updated: 16 Oct 2026 (evening).

## 1. Move acquisition from the console app to the API (proposed 16 Oct 2026) — **done 16 Oct 2026, except the web button**

The first proposal, recorded as given. Topics A and B (section 2) refined it.

**Done 16 Oct 2026:** `POST api/JobFetcher` (`JobFetcherController` → `IAcquisitionRunner`, a hosted service in Services/Acquisition) answers 202 and runs `IAcquisitionService` in the background, with progress in the journal (`startedBy: Api`) and the API log; 409 while one runs. One-at-a-time uses the journal instead of a lock file (decision 2 below): the API and the console (exit code 4) refuse while either runs. The API opens the database read-write (`AddJobStore`, which now also registers `IJobCatalog`), creates or migrates it at start-up, and has its own `FetchJobDetails` and `HttpClient` settings. Stopping the API stops the run after the current job. **Still open:** the web button (`IJobFetcherClient`, Acquire page) and a status on the page; a lock file, only if the small gap of two starts in the same second matters.

**Problem.** The console app mixes four things: the work (`InboxService`, `DetailsService`), running the flow (step 1 → step 2 → totals, in `PipelineConsole`), reporting progress (printing in `InboxConsole`, `DetailsConsole`, `ConsoleLog`) and lifetime (start, Ctrl+C, exit codes). The API cannot reuse the flow, cannot print, and an HTTP request cannot wait for a run that takes minutes or hours (pauses, circuit breaker cooldowns).

**Proposed pieces**

- `IAcquisitionService` (Services): runs step 1 then step 2 and returns one `AcquisitionResult`; the ordering and totals move here from `PipelineConsole`. Progress keeps going out through `IProgress<>`.
- `IAcquisitionRunner` (Services, singleton): `TryStart()` starts the run on a background task and returns at once; only one run at a time; keeps a `Current` status (state, step, counts, last message, result); cancels after the current job when the API stops.
- `JobFetcherController` (Api): `POST api/JobFetcher` → `202 Accepted` with the status, or `409 Conflict` if a run is already going. Answers immediately; the run continues in the background and is logged in the API window.
- The API then **writes** to the database: `AddJobStore` (read-write) also registers `IJobCatalog`; the API switches from `AddJobCatalog` to `AddJobStore`, creates and migrates the database at start-up (the "no database yet" 503 then practically disappears), and wires Infrastructure (HTTP client, saved pages, input folder) in `Program.cs` only. API `appsettings.json` gets the `Details` and `HttpClient` sections.
- Web: `IJobFetcherClient` / `JobFetcherClient` (`POST api/JobFetcher`); the Acquire page gets a "Start acquisition" button (form post, no script) and shows started / already running / API not reachable.
- Console keeps working the same way, calling the shared service; `PipelineConsole`, `InboxConsole` and `DetailsConsole` only print.

**Partly done 16 Oct 2026:** `IAcquisitionService` (Services/Acquisition, with `IAcquisitionObserver` instead of `IProgress<>`) runs step 1 then step 2 with the journal and returns `AcquisitionResult`; step 2's settings moved from Infrastructure to Services; the console classes only print (`ConsolePaths` replaces the settings they read), and only the console's `Program.cs` knows Data and Infrastructure. Still open: `IAcquisitionRunner`, `JobFetcherController`, the API writing, the web button and the lock file.

**Decisions proposed with it**

1. Progress in the web: POST only first; a status endpoint (`GET api/JobFetcher`) and a live status on the Acquire page as the next step.
2. Console and API running at the same time would double the requests: a lock file `<DataRoot>\acquisition.lock`, held during a run; a second run is refused.
3. The console app stays as a second entry point, sharing the same service; it can be retired later.

**Checks proposed:** console flow test unchanged; API 202 / 409, jobs and descriptions stored, pages archived, circuit breaker still opens, database created when missing, lock file refuses a parallel run; web button states; docs.

## 2. Topics to settle before section 1 (analysis of 16 Oct 2026)

### A. Two clearly separated steps, named for what they do — **done 16 Oct 2026**

- Step 1 **FetchJobListing**: reads the saved HTML pages, parses them, stores the job listing.
- Step 2 **FetchJobDetails**: reads the jobs to fetch from the database and fetches their details behind the circuit breaker.

Done as a pure refactoring (no behaviour change): types, folders, namespaces and settings sections renamed; `HtmlToText` moved from step 2's `JobPageParser` to the shared `Parsing/HtmlText`, so step 1 no longer uses step 2's code; `IInboxFolder` became `IInputFolder`. Still open: making each step runnable on its own from outside the console, and keeping "acquisition" only as the optional sequence of both. See the name table in the chat of 16 Oct 2026; the main moves are `InboxService` → `JobListingFetcher` (`Services/FetchJobListing/`), `DetailsService` → `JobDetailsFetcher` (`Services/FetchJobDetails/`), settings sections `Inbox` → `FetchJobListing` and `Details` → `FetchJobDetails`.

### B. Reading progress independently of the running process — **done 16 Oct 2026**

Recommendation: a **run journal in the database**. The process writes what happens (run started, page read, job read/failed, circuit opened until …, run finished) to two tables; a separate reader (API `GET` endpoints, then the web page) reads it from there, whether the run happens in the API, the console app or a future worker, and even after a restart. Rejected alternatives: in-memory status in the API (lost on restart, console runs invisible), a JSON-lines log file (hard to query), a message broker (too much for one user). Later option: the database also holds a queue of requested runs and a separate worker (the console app, turned long-running) executes them, so the API only records requests.

Done as decided: one pair of tables per step (`JobListingJournalRuns`/`…Entries`, `JobDetailsJournalRuns`/`…Entries`, migration `AddJournal`); `JournalController` (read-only: recent runs, a run with its entries after N) → `IJournalService` → `IJournalRepository` → `SqliteJournalRepository`, isolated in `Journal` folders in every layer; the console records both steps; Abandoned after 2 minutes without a sign of life (reported by the API, and written by the next run). Still open: the web page that follows a run, and starting runs from the API (section 1).

## 3. Step 2 (FetchJobDetails): refusals and disappearing jobs (analysis of 16 Oct 2026)

Seen in real runs: at one request every 5 s, LinkedIn answered 429 after about 6 requests; after a cooldown the trial worked and the next request was refused again. With `DelaySeconds` 45 and `CooldownSeconds` 210 (doubling), a new run started shortly after a stopped one was refused on its very first request: the site's limit outlives the app's run, while the circuit breaker starts fresh each run.

Settings advice (configuration only, already possible): keep a slow steady pace (`FetchJobDetails:DelaySeconds` 30–60), keep the cooldown doubling (`CooldownSeconds` 210, `MaxCooldownSeconds` 1800), and leave an hour or more between runs after a run stopped on 429s. A flat, short cooldown (Cooldown = MaxCooldown) asks again sooner after each refusal: against the "no quick retries" rule, and it usually reaches StopAfterOpenings faster.

Proposed changes, in this order, each small and independent (plan first when picked up):

1. **Remember the last refusal across runs, and honour `Retry-After`.** At start, step 2 looks at the most recent refusal (jobs `Blocked`, `LastAttemptAt`, already stored; no new table, no use of the journal). If it is within the cooldown, wait out the rest, or stop with "the site refused at hh:mm; next try after hh:mm". When a 429 carries a `Retry-After` header, wait at least that long (the larger of it and the configured cooldown); today the response headers are not read.
2. **Detect closed jobs.** LinkedIn often keeps an expired job's page online ("No longer accepting applications") or redirects to search results instead of answering 404; such jobs are stored as `Read` and look open. Recognise the closed marker and store it (a new status `Closed`, or a flag shown in the web table).
3. **Give up on jobs that fail repeatedly.** A job that always gets a 5xx or cannot be parsed is retried on every run, forever. Count attempts per job; after 3 failed runs mark it (new status, e.g. `GaveUp`): excluded from the work list, still visible. Refusals (`Blocked`) do not count: they are about the request rate, not the job. Needs one column and a migration.

Not to do: "skip to the next job" after a refusal. A 429 is about how often requests are sent, not about one job; moving on would send another request into the same refusal. Broken links are already skipped: 404/410 marks the job `Gone` (never tried again) and the run moves on, neutral to the circuit breaker; never-tried jobs always come first in the work list.

Small related bug seen in the log: the "time left" estimate is unreliable when the first request of a run is refused (it showed "~10s left" for 9 jobs at 45 s each).

## 4. Other open items

- **Journal retention (review later).** Each step keeps its last 50 runs, pruned when a new run starts (TODO in `Services/Journal/JournalService.cs`). Every run adds entries, and a process that fails over and over keeps adding runs, so the database could grow out of control: decide on a limit by age or size, and maybe a cleanup that does not depend on a run starting.

- `DataPaths` holds input folders as well as the database path; rename (for example `InputPaths`) or split. The owner's TODO comments are in `DataPaths.cs` and the console's `Program.cs`.
- Delete the hidden `.dev.md5` left in `job-fetch-probe-data` by mistake.
- The web Overview could show summary cards (totals per status) and search or filters, as in the reference design; both need new data from the API (counts per status, filtering), so a change across Api, Services and Data.
- The web app could wait for the API before opening the browser, or retry once, if starting both together often shows "API not reachable".
- A writing style rule for `CLAUDE.md` (block bodies and if/else rather than expression-bodied members and ternaries), if the owner wants it as a rule.
- From `plan.md`, "Possible next steps": a test project with fixtures; AngleSharp instead of regex parsing; `Posted` as a date; PostgreSQL in Docker.
- Unused folders `pages` and `output` in the data folder; `197 .net jobs in Valencia.html` in `pages` could move to `input`.
- Git: the tag `probe-v1` and removing the `archive` folder, if not done yet.
