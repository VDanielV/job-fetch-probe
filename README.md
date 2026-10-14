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
dotnet run --project src/JobFetchProbe -- policy
dotnet run --project src/JobFetchProbe -- extract --html fixtures/sample-job.html
dotnet run --project src/JobFetchProbe -- fetch --url https://www.JobSeekerPortal.com/jobs/view/<id>/ --live
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

## Notes on the layout

The layout follows [`docs/spec.md`](docs/spec.md). Two small additions: `Policy/RobotsPolicyParser.cs` holds the pure parser that `RobotsPolicyParserTests` exercises, and `tests/` has `ProbeOptionsTests.cs` (URL and flag rules) and `ProbeRunnerTests.cs` (end-to-end runs against an in-memory fetcher, including "fetch without --live creates no client").

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
