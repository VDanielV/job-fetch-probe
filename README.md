# JobFetchProbe

Local academic probe for the question in [`docs/plan.md`](docs/plan.md): can a plain .NET client, with ordinary HTTPS and no access-control bypass, retrieve a usable JobSeekerPortal job description?

The app reads `robots.txt`, extracts a job from a local HTML file, and can send one anonymous GET of one job URL. A block, challenge or auth wall ends the live phase. There is no second method.

## Build and test

```sh
dotnet build
dotnet test          # offline; no test touches the network
```

## Commands

```sh
dotnet run --project src/JobFetchProbe.Console -- policy
dotnet run --project src/JobFetchProbe.Console -- extract --html fixtures/sample-job.html
dotnet run --project src/JobFetchProbe.Console -- fetch --url https://www.JobSeekerPortal.com/jobs/view/<id>/ --live
```

Shared flags: `--out <dir>` (default `out`), `--timeout-seconds <5-30>` (default 15).

| Exit | Meaning |
| --- | --- |
| 0 | Finished and wrote a report |
| 2 | Invalid arguments (no report, no request) |
| 3 | Stopped on a block, challenge, auth wall or disallow. Report written |
| 4 | Transport failure or timeout. Report written |

Every run overwrites `out/probe-report.json` and appends one line to `out/probe-log.jsonl`. `fetch` copies the two policy booleans from the latest `policy` line in that log, so run `policy` first.

## Operating rules (from [the plan](docs/plan.md))

1. Run `policy`. Read the result. If job paths are disallowed, decide whether phase 2 still adds information. `fetch` needs `--live` regardless.
2. `fetch` accepts one `https://www.JobSeekerPortal.com/jobs/view/...` URL. Search pages, guest endpoints, `start=` and `currentJobId` are rejected. The query string and fragment are dropped before sending.
3. Do not rerun `fetch` on the same URL to get a different outcome.
4. Write `out/lab-note.md` by hand. Delete any browser-saved HTML and the `out/` directory when the note is done.

## What the client does and does not do

- User-Agent `JobFetchProbe/0.1 (academic exercise; unattended-contact: none)`. Accept `text/plain` for policy, `text/html` for fetch.
- No cookies (`UseCookies = false`), no `Authorization`, no proxy configuration, no retry handler, one attempt.
- Redirects are walked by hand, at most 3, only within `https://*.JobSeekerPortal.com`. A redirect to `/login`, `/uas/login`, `/authwall` or `/checkpoint` is recorded and **not followed**.
- The response body stays in memory. The report holds status, content type, body length, final host, redirect hops (status and host only), class and reason. No body, headers or URL path.

## Classes

First match wins: `Error` (transport/status 0) → `Denied` (401, 403, 429, 999) → `AuthWall` (final URL path) → `Challenge` (visible text has "security verification" or "captcha") → `EmptyShell` (200, visible text < 400 chars) → `DescriptionPresent` (200, ≥ 400 chars, `h1` present) → `Error` (anything else, including a 200 with enough text but no `h1`).

`DescriptionPresent` means the response looks like a document. It does not mean the fixture extractor ran on JobSeekerPortal markup.

## Extractors

`extract` uses `FixtureJobExtractor`, which reads only the `data-probe` / `data-field` contract used by `fixtures/sample-job.html`. A browser-saved page will fail with a clear error. `--extractor saved` exits 2 with `saved extractor is not part of this build`; [the spec](docs/spec.md) leaves that extractor for later, behind `IJobExtractor`, with a golden test kept outside the repo.

## Documents

- [`docs/plan.md`](docs/plan.md): question, hypothesis, phases, stop rules.
- [`docs/spec.md`](docs/spec.md): software spec this build implements.

## Architecture

Four projects, Clean Architecture style. Dependencies point inward only.

```text
Console ──► Application ──► Domain
   │              ▲
   └──► Infrastructure (implements Application's interfaces)
```

| Project | Role | Contents |
| --- | --- | --- |
| `JobFetchProbe.Domain` | Models and pure rules. References nothing | `Models/` (`JobPosting`, `RobotsPolicy`, `FetchClassification`, ...), `Rules/` (`RobotsPolicyParser`, `ResponseClassifier`, `HtmlText`, `JobUrlRules`) |
| `JobFetchProbe.Application` | Use cases and the contracts they need. References Domain only | `Abstractions/` (`IPageFetcher`, `IJobExtractor`, `IReportWriter`), `Dtos/` (`ProbeReport`, `FetchResult`, ...), `Services/` (`PolicyService`, `ExtractService`, `FetchService`), `Results/` |
| `JobFetchProbe.Infrastructure` | Real HTTP, file and JSON implementations | `Http/HttpPageFetcher`, `Extraction/FixtureJobExtractor`, `Reporting/JsonReportWriter`, `DependencyInjection` |
| `JobFetchProbe.Console` | Composition root and CLI. No business logic | `Program`, `ProbeApp` (DI wiring), `Cli/` (`ProbeOptions`, `ExitCodes`) |

Each project has a matching test project under `tests/`:

| Test project | What it covers |
| --- | --- |
| `JobFetchProbe.Domain.Tests` | robots.txt parsing, every classifier class, URL rules, posting field rules |
| `JobFetchProbe.Application.Tests` | The three services against in-memory fakes, including "report holds no body or path" |
| `JobFetchProbe.Infrastructure.Tests` | Fixture extractor, JSON report writer, and the real `HttpPageFetcher` against a stub handler (identity headers, no cookies, redirect rules, no retry) |
| `JobFetchProbe.Console.Tests` | Argument rules and end-to-end runs through the real composition root, including "fetch without --live builds no services" |

Build settings are shared in `Directory.Build.props`; package versions are managed centrally in `Directory.Packages.props`.

## Lab note template

Create `out/lab-note.md` by hand after the run:

```text
Question:
Hypothesis:
Date:

Phase 0 result:
Phase 1 result:
Phase 2 ran: yes/no
Phase 2 class:

One-sentence answer:
Raw HTML deleted: yes/no
```
