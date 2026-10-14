# Plan: does a plain .NET client retrieve a LinkedIn job description?

Academic exercise. Local only. One operator, one machine. The result is a lab note, not a job collector.

Date: 14 Oct 2026.

## Question

Can a small .NET console app, using ordinary HTTPS and no access-control bypass, retrieve a usable LinkedIn job description?

## Pre-registered hypothesis

A plain client will not produce a usable description.

Expected observations, in order:

1. `robots.txt` disallows a general client, and disallows job-search and guest-job paths for the clients LinkedIn names.
2. A single anonymous GET of a job URL returns a non-page result: auth wall, checkpoint, HTTP 999, 401, 403, 429, or an HTML shell without the description.
3. A parser can still extract title, company, location, and description from an HTML file saved earlier by hand. That result proves the parser. It does not prove live collection.

If a live GET returns the description, record that as a falsification of point 2. Do not add more URLs to "see how far it goes."

## What this exercise is

Three phases, each with a written result.

| Phase | Proves | Live requests |
| --- | --- | --- |
| 0. Policy read | What LinkedIn publishes for automated access | 1 GET of `robots.txt` |
| 1. Parser on a local file | Whether extraction code works | None |
| 2. One live probe | What one anonymous GET actually returns | 1 GET, only with an explicit flag |

## Boundary

The app identifies itself. It does not log in. It does not read browser cookies. It does not imitate a browser or a search-engine bot. It does not solve challenges, rotate addresses, change fingerprints, or call private endpoints.

A block ends the live phase. The lab note then says the plain client does not work. There is no second method inside this exercise.

LinkedIn's User Agreement and crawling terms prohibit unauthorized automated copying. This plan does not clear that. The exercise stays small so the observation is a test, not a collector: two live requests maximum per run, no scheduler, no search pagination, no account actions.

## Pass and fail

A run is complete when the lab note records all three phases.

| Claim | Pass | Fail |
| --- | --- | --- |
| Policy read | The report quotes the `User-agent: *` rule and the job-path rules that apply | The report guesses instead of storing the response |
| Parser | Tests pass against the local fixture and the golden file | Tests need a live site |
| Live probe | The report stores status, final URL, content type, body length, and one class: `DescriptionPresent`, `AuthWall`, `Challenge`, `Denied`, `EmptyShell`, or `Error` | The app retries with a different identity or header set |
| Overall question | Answered in one sentence from the evidence above | Answered from a vendor blog or from memory |

`DescriptionPresent` means visible text contains a title and a description longer than 400 characters. Anything else is "the plain client did not retrieve a usable description."

## Stop rules

Stop the process and write the note when any of these happen:

- The policy read shows the path is disallowed. Phase 2 stays off unless the operator passes `--live` after reading that result.
- The live response is 401, 403, 429, 999, or a body that indicates an auth wall or checkpoint.
- The live response redirects to a login or checkpoint URL.
- A request throws, times out, or returns an empty body.
- The operator has already used the one job URL allowed for that run.

Do not refresh the same URL to get a different outcome.

## Materials

- .NET SDK 10, console app plus xUnit.
- One public job URL copied from a page already open in the browser. Query strings that are tracking parameters can be removed. The URL is not committed.
- One HTML file saved with the browser's Save Page function, stored outside git, used only as a local fixture. Delete it when the note is done.
- A synthetic HTML fixture committed in the repo, with invented job text, so the parser tests have no live content.

## Work sequence

1. Write the lab note header: question, hypothesis, date, URL host only.
2. Run phase 0. Attach the relevant `robots.txt` lines to the note.
3. Implement the parser against the synthetic fixture. Get the tests green.
4. Point the same parser at the saved HTML. Record which fields came out and which did not.
5. Decide whether phase 2 still adds information. If the policy read already answers the question, say so and skip phase 2.
6. If phase 2 runs, send one GET. Classify it. Quote no more than the HTTP status and the page title.
7. Close the note with the one-sentence answer. Delete saved HTML and response bodies.

## Out of scope

Search-result crawling, alerts, Easy Apply, messaging, profile pages, company employee lists, scheduled runs, storage of recruiter names, and any technique whose purpose is to look like a normal browser session.

## Effort

About half a day. Phase 1 is the coding exercise. Phase 0 and phase 2 are observation, each a few minutes.
