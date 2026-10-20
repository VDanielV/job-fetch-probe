# Spec: Runs, the history of past acquisitions

Status: agreed and built 20 Oct 2026. Like the other `spec-*.md` files, this spec stands on its own; `spec.md`, `plan.md`, `database.md` and the diagrams are not updated until the owner asks for the docs to be brought up to date.

## Goal

A new **Runs** entry in the menu lists every past acquisition. Opening one shows what happened in it: the same Summary and Whole run the Acquire page shows at the end of a run, and a new **Job links** tab with every job link the run found. The tab says whether each job was **new** or **already in the database**, what the job is now (read, archived, applied, from Notion…), and what step 2 did with it in that run.

## Why: what happens today to a job that is already known

Step 1 stores every job link it finds in the run's listing (`ListingJobs`), but it adds a row to `Jobs` only for a job it has never seen. A job already in `Jobs` is not added again. Its card (title, company, location, address) is refreshed, unless the job is already read. Step 2 then asks only for jobs without a description that are neither archived nor applied to. So a job already known **is not lost**, but it is not read again either.

The problem is that this is invisible, and the "new" counts are wrong. Step 1's "new" means *"not on an earlier page of the same run"*, not *"not in the database"*. The owner's database shows it:

| Run (step 1) | Shown today | In fact |
| --- | --- | --- |
| 16 Oct 14:44, listing `20261016_144432` | 53 jobs, 53 new | **0 new**: all 53 were found at 13:45 the same day (47 now archived, 6 applied) |
| 18 Oct 11:41, listing `20261018_114123` | 61 jobs, 61 new | 58 new, **3 already known** |
| 18 Oct 17:48, listing `20261018_174839` | 29 jobs, 29 new | **0 new**: all 29 found at 11:41 the same day; step 2 had nothing to read |

## Decisions

| # | Decision |
| --- | --- |
| 1 | **Menu:** a new entry **Runs** between Acquire and Chat (`/runs`). |
| 2 | **One line per acquisition**, newest first: a step 1 run together with the step 2 run that followed it (the same pairing the Acquire page uses). Columns: started, how it ended (Completed / Stopped / Failed), duration, pages read, job links, **new**, **already known**, descriptions read and failed, fits scored, circuit openings. 25 per page. |
| 3 | **Empty runs** (no saved page and nothing read by step 2) are hidden by default; a chip "Show empty runs" shows them. The owner's database has 22 runs, 7 of them empty; most of the others found no page but read jobs left over from an earlier run. |
| 4 | **A run's page** (`/runs/{id}`, the step 1 run's journal ID) has three tabs: **Summary** and **Whole run** (exactly as on the Acquire page) and **Job links** (new). It opens on Summary. The Acquire page's "see the summary" link and the end of a run keep working as today. |
| 5 | **Job links tab:** one row per job link of the run's listing, in the order found: the page it came from, title and company, **New** or **Already known (first seen 16 Oct 13:45)**, the job's state now (Read, Pending, Blocked, Gone, External; plus Archived or Applied, with the date), its fit if any, and **what step 2 did in this run**: "Read", "Read · Fit 78", "Failed: …", "Refused", or "Not asked for" with the reason (already read on 16 Oct / archived on 18 Oct / applied on 19 Oct / not reached: the run stopped). Each row opens the job in the Overview (or in Applications when applied). Chips filter the list: All / New / Already known. |
| 6 | **"New" is worked out from the data**, so it is right for past runs too: a job is new in a run when it was first seen by that run (`Jobs.FirstSeenAt` equals the listing's `CreatedAt`, which is how step 1 stores new jobs). No migration, no new column. |
| 7 | **The counts are fixed from now on:** step 1 reports "new" as *new to the database* (the storage step knows which jobs it added). The journal's run total and the Acquire page then say e.g. "6 stored as listing … (4 new, 2 already known)". The per-page figures keep their meaning but are labelled honestly: "39 links, 29 not on an earlier page of this run". Past journal rows are not rewritten; the Runs page uses decision 6 for them. |
| 8 | **Before the journal:** the two listings from 15 and 16 Oct 13:45 have no journal run. They appear at the end of the list as "earlier listing (no journal)", with the Job links tab only. The `notion-migration` listing is not a run and is not shown. |
| 9 | **History is kept:** listings are never deleted; the journal keeps the last 50 runs per step (unchanged). A run whose journal was pruned shows as in decision 8. |
| 10 | **Read only:** the page changes nothing (no re-run, no delete). |

### Jobs imported from Notion

Refreshing the card of a known job also overwrites its address unless the job is already read. For the jobs imported from Notion that carry a LinkedIn number (status External), a search that finds them again replaces the stored Notion page address with the LinkedIn one. **Kept as it is** (owner, 20 Oct 2026): the information from the search is newer. The Job links tab shows such a job as "Already known" with status External.

## Layers

| Layer | Change |
| --- | --- |
| Models | `Models/RunHistory/`: `RunHistoryLine` (one line of the list), `RunHistoryPage` (lines, paging), `RunJobLink` (one row of the Job links tab), `RunJobStep2` (what step 2 did: kind and text). |
| Services | `Services/RunHistory/`: `IRunHistoryService` / `RunHistoryService`: pairs step 1 and step 2 runs, hides empty ones, builds the step 2 outcome of each link from the run's journal entries and the job's dates. `Services/Abstractions/IRunHistoryRepository`. Step 1: the storage returns how many jobs it added; the journal records that as the run's `NewJobs`. |
| Data | `Repositories/SqliteRunHistoryRepository`: reads `Listings`, `ListingSources`, `ListingJobs`, `Jobs`, `ArchivedJobs`, `Applications`, `JobFitAssessments` (latest) and the journal tables. `SqliteBatchRepository.SaveAsync` also returns the number of jobs added. No migration. |
| API | `RunsController`: `GET api/Runs?page=1&empty=false`, `GET api/Runs/{id}/job-links`. The Summary and Whole run tabs use the existing `api/Journal` routes. |
| Web | Menu entry; `Pages/Runs.cshtml` (list) and `Pages/Run.cshtml` (`/runs/{id}`, tabs). Summary and Whole run reuse `_AcquireSummary`, `_AcquireWholeRun` and `AcquisitionView`; Job links is a new partial. The Acquire page's summary line uses the new/known counts. |

## Out of scope

Re-running or deleting a run, comparing two runs, reading a job again from the Runs page, changing the journal's retention, rewriting past journal rows, linking the same offer found under two sources.

## Acceptance

1. On a copy of the owner's database: Runs lists the 15 non-empty runs plus the 2 earlier listings, newest first; "Show empty runs" adds the 7 empty ones.
2. The 16 Oct 14:44 run shows 53 links, **0 new, 53 already known**; 18 Oct 11:41 shows 58 new and 3 known; 18 Oct 17:48 shows 0 new and 29 known. The figures in the table above match.
3. Job links of 18 Oct 11:41: each of the 58 new jobs shows what step 2 did with it in that run (read, failed, or not reached because the run stopped); the 3 known ones show "Not asked for: already read on …".
4. A new acquisition on the local test site, run twice with the same saved page: the second run shows 0 new and 6 already known, on the Acquire page and in Runs; step 2 asks for nothing it already has.
5. Summary and Whole run on a run's page show exactly what the Acquire page showed when that run ended.
6. Every row of Job links opens the job (Overview, or Applications when applied).
7. Nothing in the database changes when the pages are viewed.
