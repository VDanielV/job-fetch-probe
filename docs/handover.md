# Handover

29 Oct 2026. Where Job Fetch Probe stands, how the work is done, and what is open, so a new chat (or a new Claude
project) can continue without the conversation that built it. Read this with `CLAUDE.md` (the working agreement).

## The application

A single-user tool, run on the owner's Windows computer, to find, score and follow job applications.

| Part | Where | Normal address |
| --- | --- | --- |
| Web app (Razor Pages): Overview, Applications, Acquire, Runs, Chat | `src/JobFetchProbe.Web` | http://localhost:5080 |
| API (REST): the only way into the database and Ollama | `src/JobFetchProbe.Api` | http://localhost:5081 |
| Services, Data (SQLite, EF Core), Infrastructure (HTTP, files, Ollama), Models | `src/JobFetchProbe.*` | |
| Data folder: `database/jobs.db`, `input/` (saved search pages), backups | `C:\Code\job-fetcher\job-fetch-probe-data` | outside the repository |
| Local model | Ollama, `qwen2.5:7b` | http://localhost:11434 |

The layers and patterns are described in `docs/patterns-review.md`; the original design in `docs/spec.md` and
`docs/plan.md`.

## What is built (newest first)

| Agreed | Feature | Spec |
| --- | --- | --- |
| 21 Oct | Search applications by company or title; reject from the results | `spec-application-search.md` |
| 20 Oct | Runs page: past acquisitions, new vs already known job links, what step 2 did with each; "new" counted against the database | `spec-run-history.md` |
| 20 Oct | Acquire page keeps the run's summary when opened mid-run | (fix, no spec) |
| 20 Oct | Fit scored during acquisition, right after each job is read; adds to the pause between requests | `spec-acquisition-fit.md` |
| 20 Oct | The app's own job ID (`Jobs.Id`) with `Source` + `SourceJobId`; Notion import mapped to `notion` | `spec-job-identity.md` |
| 19 Oct | Fit history: every score kept, shown in the panes | `spec-fit-history.md` |
| 19 Oct | Applications: stages, follow-up flags (7 and 21 days), notes timeline, undo | `spec-applications.md` |
| 18 Oct | Check fit in the Overview pane (scored by the local model) | `spec-fit-pane.md` |
| 18 Oct | Archive with reasons; text and location filters in the Overview | `spec-archive.md`, `spec-filter.md`, `spec-location.md` |

The last database migration is `20261020121029_UseOwnJobIds`. Backups made before risky changes are in
`job-fetch-probe-data\database\Backup` (`jobs(beforeOwnJobIds).db` and two zips).

## How the work is done

1. **Spec first.** For a feature: a `docs/spec-<name>.md` with Goal, Decisions (a numbered table), Layers,
   Out of scope and Acceptance (numbered checks). Open questions go in the spec for the owner to answer.
2. **Plan, then "go".** Nothing changes before the owner says go (`CLAUDE.md`).
3. **Check the owner's files before overwriting.** Compare each file to be changed with the last delivered or
   committed version (git status, a checksum); the owner edits and commits between sessions. His version wins.
4. **Build with 0 errors and 0 warnings**, then test on the harness (`tools/test-harness`) and, for data
   changes, on a **copy** of the owner's database. Never the real data folder, never LinkedIn.
5. **Deliver, then verify** that the files on disk match what was tested. The owner commits.
6. **Report briefly:** what changed, how it was checked, what is left.

Docs: `README.md`, `docs/spec.md`, `docs/plan.md`, `docs/database.md` and the diagrams are **frozen** until the
owner asks to bring them up to date; new `docs/spec-*.md` files are always fine. Style rules (block bodies, if/else,
one type per file, `internal static` helpers, entities named `…Row`) are in `CLAUDE.md`.

## Standing rules

- **Fit documents are private.** `src/JobFetchProbe.Api/Fit/*.json` (profile, policy, rubric) are gitignored: never
  read them, never commit them, never copy profile text into source, settings or commits. `FitDocuments.cs` is source.
- **Never test against LinkedIn.** Use the harness's test site and fake Ollama.
- **Do not restart the API while a scoring batch is running**, unless the owner asks.
- **No database schema change unless the owner asks**; never invent experience in fit prompts or documents.
- **`tools/JobFetchProbe.NotionImport` is not run again** unless the owner asks (the import is done).
- **Write temporary files only in your own workspace**, never in the repository or the data folder.

## Tools

- `tools/test-harness`: run everything locally with a test site and a fake Ollama; checks used for every feature
  (see its README).
- `job-fetch-probe-data\script\score-all-jobs.ps1`: one-time scoring of all jobs through the API, with its logs.

## Open items

- `docs/backlog.md`: idempotent start and an acquisition record (§5), Retry-After and a quiet period after a
  refusal (§3, §5 C), live progress without page reloads (§6), journal retention (§4).
- `docs/patterns-review.md`: a test project, architecture tests and CI first; then health checks, central package
  management, structured logging; a DOM parser instead of regular expressions later.
- The frozen docs (README, `spec.md`, `plan.md`, `database.md`, diagrams) describe the app before 19 Oct: they lack
  the job identity change, applications, fit, runs and search. Update them when the owner asks.
- Journal rows from before 20 Oct count "new" as "not on an earlier page of the run"; the Runs page works the real
  figures out from the data, so only the old journal text is affected.
