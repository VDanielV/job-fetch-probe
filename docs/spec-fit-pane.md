# Spec: checking a job's fit from the details pane

Status: agreed 18 Oct 2026. Stands on its own, like the other `spec-*.md` files. The scoring itself (`POST api/JobFit`, the Fit files, Ollama) is unchanged; this is only how the Overview uses it.

## Decisions

| # | Decision |
| --- | --- |
| 1 | A **Check fit** button in the details pane, next to "Open the job page"; the key **F** does the same. Disabled, with the reason, when the job has no description yet. |
| 2 | The result shows in the pane, above the archive box: decision (Apply, Maybe, Skip), fit 0–100 on a bar marked at 40 and 60, the reason, "Hard skip" when it applies, and what the job did not state. |
| 3 | After a score, the archive box's note is filled in: `Fit 46% · Skip: <reason>`, so 1, 2, 3 or Other record it. A note the owner typed is not replaced. |
| 4 | Nothing is saved. Opening another job, archiving or reloading clears the score. |
| 5 | While scoring: a timer and **Cancel**. Cancel (or leaving the page) stops the request, and with it the API's call to Ollama. |
| 6 | Errors (Ollama down, too slow, an unreadable answer, a job gone) show the API's message in a red card with **Try again**. |

## How it is built

- Web only. `IJobFitClient` / `JobFitClient` call `POST api/JobFit` with a 130-second time-out (the API allows Ollama 120 seconds).
- `Index` page handler `OnPostFitAsync`: with the script, it returns the card (`_FitResult`) for the pane; without it, the page comes back once with the card (carried over the redirect, not stored).
- `fit.js`: the button and F, the timer and Cancel, the card, the note.
- The filters, page size and open job are kept, as for archiving.

## Acceptance

1. Apply, Maybe and a hard Skip each show the right card; the note is filled in; key 2 then archives with that note and opens the next job.
2. Cancel empties the card, frees the button, and the API stops waiting for Ollama.
3. Ollama down and an unreadable answer show the red card; a job without a description has the button disabled.
4. Without the script, the button still scores the job (page reload).
5. Archiving, the text filter and the chips behave as before.
