# Spec: archiving jobs

Status: agreed 18 Oct 2026, not built yet. This spec stands on its own; `spec.md` is not updated until the owner asks for the docs to be brought up to date.

## Goal

The Overview should hold only the jobs the owner may still apply to. Jobs he will not apply to are **archived**: they disappear from the Overview at once, and the acquisition never spends a request on them again. Archiving has to be quick: decide, click, next.

## Decisions

| # | Decision |
| --- | --- |
| 1 | Archiving **marks** a job; it never moves or deletes it. The `Jobs` row, its details and its listings stay as they are. |
| 2 | Step 2 (FetchJobDetails) **never requests an archived job**, whatever its status. |
| 3 | No restore. (Possible later: a Restore button in the "Show archived" view.) |
| 4 | Each archive has a **reason** from a fixed list, and an optional free-text **note**. |
| 5 | Archive at once, no "are you sure?". |
| 6 | Two ways to archive on the Overview: **A**, tick rows and archive them together; **C**, archive from the details pane, which then opens the next job. |
| 7 | The Overview shows only jobs **to review** (not archived). A "Show archived" checkbox brings archived jobs back, greyed, with their reason. |
| 8 | The feature is kept apart from the acquisition: its own `Archive` folders in every layer; the acquisition's only change is one condition in step 2's work list. |

## Reasons

| Value (stored) | Shown as |
| --- | --- |
| `Relocation` | Requires relocation |
| `TechStack` | Not applicable tech stack |
| `Salary` | Salary range too low |
| `Other` | Other (the note is then required) |

The list lives in code (`ArchiveReason`); adding a reason later is one line, no migration.

## Data

New table `ArchivedJobs`, one row per archived job (migration `AddArchivedJobs`):

| Column | Type | Meaning |
| --- | --- | --- |
| `JobId` | TEXT, PK, FK → `Jobs.JobId` (cascade) | the job |
| `ArchivedAt` | TEXT (date and time) | when it was archived |
| `Reason` | TEXT(16) | one of the reasons above, by name |
| `Note` | TEXT(500), nullable | the owner's note |

A job is archived when it has a row here. Nothing else in the database changes. The existing jobs, listings and journal are untouched by the migration.

How the rest of the app treats an archived job:

| Where | Behaviour |
| --- | --- |
| Step 1 (FetchJobListing) | Unchanged. A saved page that lists an archived job adds it to the listing as usual; the job stays archived (its `Jobs` row exists, so it is not "new"). |
| Step 2 (FetchJobDetails) | Its work list leaves archived jobs out, so they are never requested. |
| Overview, API job list | Left out, and left out of the count, unless archived jobs are asked for. |
| API one job (`GET api/JobCatalog/{id}`) | Still returned, with its archive information. |
| Journal, Acquire page | Unchanged. |

## API

`POST api/ArchivedJobs`: archives one or more jobs in one transaction.

```json
{ "jobIds": ["4477387672", "4477373226"], "reason": "TechStack", "note": "Databricks, Spark" }
```

- `200` with `{ "archived": 2, "alreadyArchived": 0, "notFound": 0 }`. Archiving an archived job again changes nothing (it keeps its first reason and date) and counts as `alreadyArchived`; an unknown ID counts as `notFound`.
- `400` (validation problem) when: no job IDs, more than 100, an unknown reason, `Other` without a note, a note over 500 characters.
- `503` while there is no database, as for the other endpoints.

`GET api/JobCatalog?page=&pageSize=&includeArchived=false`: as today, plus:

- Archived jobs are left out unless `includeArchived=true`.
- Each job carries `archive` (`archivedAt`, `reason`, `note`), null when not archived.
- The page carries `archivedCount`, the number of archived jobs (for "Show archived (3)"). `total` counts the jobs listed, so to review only, unless `includeArchived=true`.

`GET api/JobCatalog/{jobId}`: as today, plus `archive`.

## Overview

The header shows **"108 to review"** and, on the right, a **Show archived (3)** checkbox (`?archived=1`, kept while paging). The pager reads "Showing 1–25 of 108 jobs to review".

### A: tick and archive together

- A checkbox at the start of every row; one in the header ticks every row on the current page. Ticked rows are highlighted.
- While at least one row is ticked, a dark bar appears above the table: "3 selected", the reason list, the note field, **Clear** and **Archive 3**.
- Clicking a checkbox never opens the details pane; clicking anywhere else on the row still does.
- Ticks belong to the current page; moving to another page drops them.

### C: archive from the details pane

- The pane of a job to review shows a box "Not for me? Archive and open the next job" with three buttons (Requires relocation, Not applicable tech stack, Salary range too low), **Other**, and the note field.
- After archiving, the pane opens the job that came after it in the table (the first job of the next page if it was the last row; the pane closes if it was the very last job).
- Keys while the pane is open (and the cursor is not in the note): **1**, **2**, **3** archive with that reason; **↓** / **↑** open the next / previous job without archiving.

### After archiving (A or C)

- The page reloads at the same page number, refilled to 25 rows, with a green line: "3 jobs archived: Not applicable tech stack." If that page no longer exists (the last rows were archived), the new last page is shown.
- Errors (API not running, database) show in the existing banner; nothing is archived.

### Show archived

- Archived jobs appear in their place in the table, greyed, without a checkbox, with a small tag (the reason).
- Their pane shows "Archived on 2026-10-18 14:20 · Not applicable tech stack · note" instead of the archive box.

Without JavaScript everything still works (forms post to the page and redirect back); only the bar's appearing, select-all and the keys need the script.

## Out of scope

Restoring, editing a reason, deleting jobs, archiving from the Acquire page, bulk selection across pages, filters or search.

## Acceptance

1. The migration adds `ArchivedJobs` and changes nothing else: on a copy of the owner's database every existing table keeps its rows and values.
2. Archiving 3 jobs with a reason (A) removes them from the Overview and the count at once; "Show archived" shows them greyed with the reason.
3. Archiving from the pane (C, button or key) opens the next job; on the last row of a page, the first job of the next page.
4. Archiving an archived or unknown job is harmless and reported; `Other` without a note, no IDs or a bad reason give 400.
5. Step 2 never requests an archived job (checked against the test site's request log), while still reading the other jobs.
6. Step 1 reading a page that lists an archived job keeps it archived and out of the Overview.
7. The acquisition, journal and Acquire page behave exactly as before (the existing UI and API tests pass).
