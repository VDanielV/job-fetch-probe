# Spec: scoring each job's fit during the acquisition

Status: draft 20 Oct 2026, to be agreed. Like the other `spec-*.md` files, this spec stands on its own; `spec.md`, `plan.md` and the diagrams are not updated until the owner asks for the docs to be brought up to date.

## Goal

Every job that step 2 reads is scored at once, with the same Check fit the owner uses in the details pane. The score is ready when the owner opens the Overview (the Fit column and Best fit sort work straight away).

The time the local model takes also spaces out the requests to LinkedIn: the next job page is asked for only after the score **and** the usual pause, which makes a 429 refusal less likely.

## Today

```
step 1: read the saved search pages  →  step 2, for each unread job:
        pause (DelaySeconds) → read the job page → store it → next job
```

## After

```
step 1: unchanged  →  step 2, for each unread job:
        pause (DelaySeconds) → read the job page → store it → score its fit (when read) → next job
```

## Decisions

| # | Decision |
| --- | --- |
| 1 | **Which jobs:** every job whose page was **read successfully in this run**, right after its description is stored. A refused, failed or gone job is not scored (it has no description). Jobs read in earlier runs are not scored by the acquisition (the one-time script covers those). |
| 2 | **The pause adds up:** the score's time comes on top of `DelaySeconds`. The gap between two LinkedIn requests becomes *score time + DelaySeconds* (typically 30–90 s + 5 s), never shorter than today. |
| 3 | **The score is the normal Check fit:** same prompt, same Fit files, same rules (HardSkipStack included), stored in `JobFitAssessments` like any other check, with the model's name and the profile version. |
| 4 | **The model never stops the acquisition.** If a score fails (Ollama down, too slow, an answer it cannot read), the job stays unscored, the failure is reported, and the run goes on to the next job. After **3 failed scores in a row**, scoring is switched off for the rest of the run (Ollama is most likely not running) and the run continues as it does today; the journal says so once. |
| 5 | **The circuit breaker counts only LinkedIn.** A failed score is not a refusal and does not touch the breaker, the openings or the failure counts. |
| 6 | **Stop:** pressing Stop (or stopping the API) while a score is running cancels that score (nothing is stored for it) and the run stops as today, after the current job. The job's description is already stored. |
| 7 | **On by default, with a switch:** `FetchJobDetails:AssessFit` (true by default). False gives exactly today's behaviour. |
| 8 | **The journal tells it:** a new journal line after the job's line, e.g. `Fit 78 · Apply (42 s)`, `Fit 46 · Skip · hard skip`, or `Not scored: Ollama is not reachable`. The run's summary counts scored and not scored jobs. No database change: the line is a new *kind* of journal entry, stored by name in the existing columns. |
| 9 | **Time left (ETA)** on the Acquire page includes the scoring time, measured as the run goes. |
| 10 | **Kept apart:** step 2 does not refer to the Fit code. It calls a small "after a job is read" hook (`IJobReadFollowUp`, in the step's folder); the Fit feature implements it. Without the hook (or with the switch off), step 2 is unchanged. |
| 11 | **Nothing is decided automatically:** a Skip or a hard skip does **not** archive the job. The owner still decides in the Overview (sorted by Best fit if he likes). |

## Flow of one job

1. Pause `DelaySeconds` (unchanged; not before the first request).
2. Read the job page (unchanged, with the circuit breaker).
3. Store the description or the failure (unchanged). Storing the description now also returns the job's app ID.
4. **If read and scoring is on:** score it (`IJobFitService.AssessAsync`), store the score, report the result.
5. Report the job, let the circuit breaker react (unchanged), next job.

## Acquire page

- The progress list shows the score line under each read job: `✦ Fit 78 · Apply (42 s)` (green / amber / grey as in the Fit column), or `Not scored: …` in grey.
- The run summary adds: "Scored 5 of 5 read jobs" (or "Scored 3; 2 not scored: Ollama was not reachable").
- The explanation of step 2 on the page mentions the scoring and that it spaces out the requests.

## Settings

| Setting | Default | Meaning |
| --- | --- | --- |
| `FetchJobDetails:AssessFit` | `true` | Score each job read; false switches it off. |
| `FetchJobDetails:FitFailuresBeforeOff` | `3` | Failed scores in a row after which scoring is off for the rest of the run (1–10). |

The model's own timeout stays `Ollama:TimeoutSeconds` (300 s).

## Out of scope

Scoring jobs read in earlier runs, archiving automatically by score, scoring in parallel, scoring when the acquisition runs from anything other than the API, changing the prompt or rules.

## Acceptance

1. On the local test site with the fake Ollama: each job read gets one `JobFitAssessments` row, and the journal shows its score line; refused and gone jobs get none.
2. The time between two requests to the test site is at least score time + `DelaySeconds` (checked in the test site's request log with a slow fake model).
3. Ollama down: the run reads every job as today; 3 "Not scored" lines, then one "scoring switched off" line, then no more scoring; the breaker is untouched.
4. A bad answer for one job: that job is not scored, the next one is.
5. Stop during a score: no score stored for that job, its description is kept, the run ends as Stopped.
6. `AssessFit = false`: the run, the journal and the requests are exactly as before this change.
7. The Overview shows the new jobs with their Fit, and Best fit sorts them.
