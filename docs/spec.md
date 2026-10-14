# Spec: JobFetchProbe

Local academic probe. .NET 10 console app. It reads LinkedIn's public crawler policy, extracts a job from a local HTML file, and can send one anonymous GET.

The implementation must follow `plan.md`. This spec defines the software. It does not define a way around a block.

## Goals

- Record what `https://www.linkedin.com/robots.txt` says about a general client and about job paths.
- Extract a fixed set of fields from local HTML.
- Optionally fetch one operator-supplied job URL and classify the response.
- Leave a JSON report on disk. Leave no secrets and no live page body after the run.

## Non-goals

- Login, cookies, browser profiles, or Chrome remote debugging.
- A headless browser, stealth plugins, proxy pools, or CAPTCHA solving.
- Search pages, pagination, guest APIs, or undocumented JSON endpoints.
- Retries that change the user agent, headers, or source address.
- A scheduler, a database of postings, or an apply flow.
- Copying recruiter names, emails, or phone numbers into the report.

## Solution layout

```text
job-fetch-probe/
  JobFetchProbe.sln
  src/JobFetchProbe/
    Program.cs
    ProbeOptions.cs
    Policy/
      RobotsPolicyClient.cs
      RobotsPolicyReport.cs
    Extract/
      JobPosting.cs
      IJobExtractor.cs
      FixtureJobExtractor.cs
    Fetch/
      IPageFetcher.cs
      HttpPageFetcher.cs
      FetchClassification.cs
      ResponseClassifier.cs
    Report/
      ProbeReport.cs
      ReportWriter.cs
  tests/JobFetchProbe.Tests/
    FixtureJobExtractorTests.cs
    ResponseClassifierTests.cs
    RobotsPolicyParserTests.cs
  fixtures/
    sample-job.html
    robots-sample.txt
  out/                  gitignored
```

Target framework: `net10.0`. Language: C# with nullable reference types and implicit usings enabled.

Dependencies:

- `Microsoft.Extensions.Http` for the typed client.
- No HTML parser package is required for the synthetic fixture. The fixture uses the tags in the contract below, read with `System.Xml.Linq` or a small dedicated reader.
- A saved real page is an operator file, not a committed fixture. Its extractor, if written, lives behind `IJobExtractor` and is covered by a local golden test that is not committed.

## Operator contract

```text
job-fetch-probe policy
job-fetch-probe extract --html <path>
job-fetch-probe fetch --url <https-url> --live
```

| Command | Required input | Network |
| --- | --- | --- |
| `policy` | None | GET `https://www.linkedin.com/robots.txt` |
| `extract` | Existing local HTML file | None |
| `fetch` | Absolute `https://www.linkedin.com/...` URL and `--live` | One GET |

Shared flags:

- `--out <dir>` defaults to `out/`.
- `--timeout-seconds` defaults to 15. Range 5 to 30.

Exit codes:

| Code | Meaning |
| --- | --- |
| 0 | Command finished and wrote a report |
| 2 | Invalid arguments |
| 3 | Policy or fetch stopped on a block, challenge, or disallow. The report still exists |
| 4 | Transport failure. The report still exists |

A block is a completed observation, so the process should not print a stack trace for it.

## HTTP client

One `HttpClient` for the process.

- User-Agent: `JobFetchProbe/0.1 (academic exercise; unattended-contact: none)`
- Accept: `text/plain` for `policy`, `text/html` for `fetch`
- No `Cookie` header
- No `Authorization` header
- Automatic decompression: gzip and br
- Redirects: at most 3. Record every hop's status and final URL
- One attempt. No retry handler
- Timeout from the flag

The client must not copy headers from a browser capture.

## Phase 0: policy

Request `https://www.linkedin.com/robots.txt`.

Parse the body as a `robots.txt` group list. A group starts at `User-agent` and owns the following `Allow` and `Disallow` lines until the next `User-agent`.

The report records:

- HTTP status and body length
- The full rule list for `User-agent: *`
- Whether `*` disallows `/`
- The rule lines, from any group, whose path contains `job` (case-insensitive)
- A boolean `jobPathsDisallowedForGeneralClient`

Matching for that boolean is literal prefix matching of `Disallow` values against these paths:

- `/jobs`
- `/jobs-guest/`
- `/job-apply/`

If `User-agent: *` has `Disallow: /`, the boolean is true.

The parser is tested against `fixtures/robots-sample.txt`, a shortened file with invented groups plus a `*` group that disallows `/` and a named-bot group that disallows `/jobs-guest/`. Tests must not download LinkedIn.

## Phase 1: extract

### Field contract

`JobPosting`:

| Field | Type | Rule |
| --- | --- | --- |
| `Title` | string | Required, trimmed, 1 to 200 chars |
| `Company` | string | Required, trimmed, 1 to 200 chars |
| `Location` | string | Required, trimmed. May be `Unspecified` |
| `Description` | string | Required, trimmed, at least 40 chars |
| `SourcePath` | string | File name only, no directory |

Missing required text throws `FormatException` with the field name. The command exits 2.

### Synthetic fixture

`fixtures/sample-job.html` uses this contract and invented content:

```html
<article data-probe="job">
  <h1 data-field="title">Backend engineer</h1>
  <p data-field="company">Northwind Tools</p>
  <p data-field="location">Valencia</p>
  <section data-field="description">
    Builds HTTP APIs in C# and runs them on Azure.
  </section>
</article>
```

`FixtureJobExtractor` reads those attributes only. Tests cover a valid file, a missing description, and a description of only spaces.

### Saved page

The operator may pass a browser-saved HTML file to `extract`. The first implementation uses `FixtureJobExtractor` only. A second extractor is optional and must be selected with `--extractor saved`.

If `saved` is not implemented, the command exits 2 with the message `saved extractor is not part of this build`.

The saved-page extractor, when added later, must:

- Accept one local file
- Return the same `JobPosting` fields
- Ignore script and style text
- Drop lines that look like email addresses or phone numbers instead of storing them
- Be tested with a golden file kept outside the repo

The spec does not fix selectors for LinkedIn's markup. Those belong in the lab note after looking at the saved file.

## Phase 2: one live fetch

`fetch` without `--live` exits 2 with `pass --live to send the single request`.

URL rules:

- HTTPS only
- Host is `www.linkedin.com`
- Path is a single job view the operator already opened. Reject paths containing `/search`, `/jobs-guest`, `start=`, or a query key `currentJobId` used to walk a result list
- No credentials in the URL

Request: GET the URL with the client above.

Persist the classification. Do not persist the body, the set-cookie values, or any header whose name contains `cookie` or `token`.

### Classes

`ResponseClassifier` assigns exactly one class:

| Class | Rule, first match wins |
| --- | --- |
| `Error` | Transport exception, timeout, or status 0 |
| `Denied` | Status 401, 403, 429, or 999 |
| `AuthWall` | Final URL path contains `/login`, `/uas/login`, `/authwall`, or `/checkpoint` |
| `Challenge` | Status 200 and the visible text contains `security verification` or `captcha` |
| `EmptyShell` | Status 200 and visible text length is under 400 |
| `DescriptionPresent` | Status 200, visible text length is at least 400, and an `h1` exists |
| `Error` | Any other status |

Visible text is the HTML with `script`, `style`, and tags removed, then whitespace collapsed. The classifier receives that text and the status. It does not receive instructions to try another request.

Tests use in-memory strings for each class. They do not call the network.

`DescriptionPresent` in this phase means the response looks like a document. It does not mean the fixture extractor ran against LinkedIn markup.

## Report

Write `out/probe-report.json` pretty-printed. Overwrite the file each run. Also append one line to `out/probe-log.jsonl`.

```json
{
  "runId": "20261014T110600Z",
  "command": "fetch",
  "startedAt": "2026-10-14T11:06:00Z",
  "finishedAt": "2026-10-14T11:06:01Z",
  "liveRequestSent": true,
  "httpStatus": 999,
  "finalUrlHost": "www.linkedin.com",
  "classification": "Denied",
  "visibleTextLength": 0,
  "policy": {
    "generalClientDisallowsAll": true,
    "jobPathsDisallowedForGeneralClient": true
  },
  "posting": null
}
```

`posting` is present only for `extract`. For `fetch` and `policy` it is null.

`finalUrlHost` is the host only. Omit the path if the path includes an id the operator does not want in the repo. Default is host only.

## Configuration and secrets

No configuration file. No environment variable for cookies, tokens, or proxies. `HttpClient` uses the machine default route.

`.gitignore` includes `out/` and `*.saved.html`.

## Logging

Console lines are short:

```text
policy general-client=disallow-all job-paths=disallowed
fetch status=999 class=Denied
```

Do not log the response body. Do not log the full job URL. Log the host and the classification.

## Acceptance

The build is acceptable when:

1. `dotnet test` passes offline.
2. `policy` writes a report with the two booleans.
3. `extract --html fixtures/sample-job.html` returns the four invented fields.
4. `fetch` without `--live` sends no request and exits 2.
5. A classifier test exists for every class in the table.
6. Review of the project shows no cookie handling, no proxy configuration, no browser automation package, and no retry that changes the user agent.

## Lab note template

Create `out/lab-note.md` by hand after the run. Do not generate it from the response body.

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

## Later work this spec does not authorize

A green `extract` test is not a reason to add browser automation. A `Denied` or `AuthWall` result closes the live question. Further product work belongs in a separate intake app that receives text the operator pastes.
