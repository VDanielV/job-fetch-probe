# Sequence diagrams (UML)

Two UML sequence diagrams, written in [Mermaid](https://mermaid.js.org/syntax/sequenceDiagram.html), so GitHub, Azure DevOps and VS Code (with a Mermaid preview) draw them from this file, and they can be edited as text. Rendered copies: [`acquisition-sequence.svg`](acquisition-sequence.svg) and [`journal-sequence.svg`](journal-sequence.svg); after editing this file, render them again (for example with the Mermaid CLI or mermaid.live). For the components and layers, see [`architecture.svg`](architecture.svg).

How to read them: time runs downwards; a solid arrow is a call, a dashed arrow its answer, an open arrow (`-)`) a message that does not wait (background work, queued journal writes). The notes marked **§5.n** are the places that are not idempotent yet, see [`backlog.md`](backlog.md), section 5.

## 1. An acquisition started from the API

```mermaid
sequenceDiagram
    autonumber
    actor Client as Client (Acquire page, .http file)
    participant Ctl as JobFetcherController
    participant Run as AcquisitionRunner
    participant Acq as AcquisitionService
    participant Jnl as JournalService + recorders
    participant S1 as JobListingFetcher (step 1)
    participant Fs as InputFolder + SavedPageReader
    participant S2 as JobDetailsFetcher (step 2)
    participant Cb as CircuitBreaker
    participant Http as JobService + HttpPageFetcher
    participant Li as LinkedIn
    participant Db as Repositories (SQLite)

    Client->>Ctl: POST api/JobFetcher
    Note over Client,Ctl: §5.1 no Idempotency-Key, a retry is a new request
    Ctl->>Run: TryStartAsync(Api)
    Run->>Acq: IsRunningAsync()
    Acq->>Jnl: IsAnyRunActiveAsync()
    Jnl->>Db: runs still Running (both steps)
    Db-->>Jnl: runs
    Jnl-->>Acq: active? (Running and a sign of life in the last 2 min)
    Acq-->>Run: true / false
    Note over Run,Acq: §5.5 check, then start: two starts in the same second can both pass
    alt an acquisition is running (here, or in the console app)
        Run-->>Ctl: AcquisitionStart(AlreadyRunning)
        Ctl-->>Client: 409 Conflict (problem details)
    else nothing running
        Run-)Acq: RunAsync(Api, LoggingAcquisitionObserver, token) on a background task
        Run-->>Ctl: AcquisitionStart(Started)
        Ctl-->>Client: 202 Accepted, Location /api/Journal/runs
        Note over Client,Ctl: §5.3 no acquisition id to follow

        rect rgb(239, 246, 255)
        Note over Acq,Db: Step 1, FetchJobListing: saved pages to listing (no network)
        Acq->>Jnl: StartJobListingRunAsync(Api)
        Jnl-->>Acq: recorder (run id)
        Acq->>S1: FetchAsync(progress, token)
        S1->>Fs: ListPending()
        Fs-->>S1: *.html in input/, oldest first
        loop each saved page
            S1->>Fs: ReadAsync(path)
            Fs-->>S1: html
            S1->>S1: parse (SearchService, SearchPageParser)
            S1-)Jnl: progress (page read or kept), queued write
        end
        S1->>Db: SaveAsync(batch), one transaction, jobs upserted by id
        Db-->>S1: listing id
        S1->>Fs: Archive(path) for each page read
        Note over S1,S2: §5.4 stored but not archived (crash, locked file): stored again next run
        S1-->>Acq: JobListingFetchResult
        Acq->>Jnl: recorder.CompleteAsync(result)
        end

        rect rgb(245, 243, 255)
        Note over Acq,Db: Step 2, FetchJobDetails: one job at a time, behind the circuit breaker
        Note over Acq,S2: §5.2 starts at once even if the last run stopped on refusals a minute ago
        Acq->>Jnl: StartJobDetailsRunAsync(Api)
        Jnl-->>Acq: recorder (run id)
        Acq->>S2: FetchAsync(progress, token)
        S2->>Db: GetUnreadAsync()
        Db-->>S2: jobs not Read and not Gone, never tried first
        loop each job (or until stopped)
            S2->>Cb: TryAcquire()
            opt circuit open
                S2-)Jnl: Waiting (time left), every 30 s
                S2->>S2: wait for the cooldown (5 min, doubling, max 30)
            end
            S2->>S2: pause DelaySeconds (not before the first request or a trial)
            S2->>Http: ReadUrlAsync(job url)
            Http->>Li: GET /jobs/view/{id} (honest User-Agent, no cookies)
            Li-->>Http: page, or 429 / 999 / sign-in, or 404 / 410, or 5xx
            Http-->>S2: JobResult (Read, Blocked or Failed)
            S2->>Db: SaveDescriptionAsync or SaveFailureAsync (gone on 404 / 410)
            S2->>Cb: RecordSuccess / RecordRefusal / RecordFailure / RecordNeutral
            S2-)Jnl: Job event, queued write
            alt circuit opened, fewer than StopAfterOpenings times
                S2-)Jnl: CircuitOpened (cooldown)
                Note over S2,Cb: the same job is the trial request after the cooldown
            else circuit opened StopAfterOpenings times
                S2-)Jnl: Stopped
                Note over S2,Cb: the run stops, the next run continues with what is left
            end
        end
        S2-->>Acq: JobDetailsFetchResult
        Acq->>Jnl: recorder.CompleteAsync(result)
        end

        Acq-->>Run: AcquisitionResult (written to the API log)
    end

    opt the API is stopping
        Run->>Acq: cancel the token, wait for the run
        Note over Run,S2: step 2 stops after the current job, the run ends Stopped
    end
```

The console app follows the same path from `AcquisitionService` on: `PipelineConsole` calls `IsRunningAsync()` (exit code 4 if an acquisition is running), then `RunAsync(Console, PipelineConsole, token)` directly, without a runner, and prints instead of logging. Ctrl+C cancels the token during step 2.

## 2. Following a run through the journal

```mermaid
sequenceDiagram
    autonumber
    actor Reader as Reader (Acquire page every 15 s, .http file)
    participant JCtl as JournalController
    participant Jnl as JournalService
    participant Db as SqliteJournalRepository
    participant Rec as Recorder (in the process that fetches)

    loop while the run goes on
        Rec-)Db: state and new entries (queued, in order)
        Rec-)Db: sign of life every 30 s
    end

    Reader->>JCtl: GET api/Journal/runs?take=10
    JCtl->>Jnl: GetRecentRunsAsync(10)
    Jnl->>Db: newest runs of both steps
    Db-->>Jnl: runs
    Jnl-->>JCtl: runs (Running with no sign of life for 2 min reads Abandoned)
    JCtl-->>Reader: 200 runs

    loop every few seconds, until the state is not Running
        Reader->>JCtl: GET api/Journal/job-details/runs/{id}?after={last entry id}
        JCtl->>Jnl: GetJobDetailsRunAsync(id, after)
        Jnl->>Db: run and entries with id > after
        Db-->>Jnl: run, entries
        Jnl-->>JCtl: run (Abandoned rule applied), new entries
        JCtl-->>Reader: 200 (or 404 unknown run)
    end
    Note over Reader,JCtl: the end state: Completed, Stopped, Failed or Abandoned
```
