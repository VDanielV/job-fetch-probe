# Spec: find an application by company or title, and record the rejection

Status: agreed and built 21 Oct 2026. Like the other `spec-*.md` files, this spec stands on its own; `spec.md`, `plan.md` and the diagrams are not updated until the owner asks for the docs to be brought up to date.

## Goal

A rejection usually arrives by email: "Thank you for applying to *Senior .NET Developer* at *Flywire*…". Today, recording it means scrolling the Applications list, perhaps opening "Show closed", to find the right application. After this change the owner types a few words of the **company** or the **title** in a search box on the Applications page. The list narrows to the matching applications, open and closed, and he opens the right one and presses **Rejected**, as today. It takes three steps: type, click, Rejected.

## Today

- The list shows the open applications (or the closed ones with "Show closed"), 25 per page, with stage chips and the follow-up flags.
- The pane's stage buttons (Applied, Interview, Offer, Rejected, Withdrawn, No answer) change the stage. Rejected closes the application: it leaves the open list, the next one opens, and a green line offers Undo.
- There is no search. The Overview has one, but it does not list applied jobs.

## Decisions

| # | Decision |
| --- | --- |
| 1 | **One search box** at the top of the Applications list: "Search company or title". It searches both at once: an email names both, and either may be written slightly differently from the stored job. |
| 2 | **How it matches:** like the Overview's filter. Capitals and accents are ignored ("telefonica" finds "Telefónica"), and a word also matches inside a longer word ("flywi" finds "Flywire"). With several words, **each word** must appear in the title or the company, so "flywire data" finds "Senior Data Engineer · Flywire". Quotes keep a phrase together ("data engineer"). |
| 3 | **Searches everything:** while a search is on, the list shows matching applications **open and closed**, ignoring "Show closed", the stage chips and the follow-up chip. A rejection may come for one already marked No answer, and seeing it there avoids recording it twice. Each row shows its stage, so a closed one is easy to tell apart. Open ones come first (flagged first, as today), then closed ones (most recently closed first). |
| 4 | **The words are highlighted** in the title and company of each row (the Overview's highlight). The list's footer says "3 applications match "flywire" · Clear". With no match: "No application matches "flywire". The job may not be marked as applied: search for it in the Overview." |
| 5 | **Recording the rejection:** open the row (click it; a search with a single match opens it at once) and press **Rejected** in the pane, unchanged. While a search is on, the application **stays in the list and in the pane** with its new stage, instead of leaving it, so the owner can add a note right away (e.g. paste the email). The green line still says "Closed as Rejected: Senior .NET Developer. Undo". |
| 6 | **The search stays** in the address (`/applications?q=flywire`): it survives the stage change, a note, Undo, a fit check and the page arrows, until **Clear** (or an empty box). Clearing returns to the list as it was before (open list, or closed with "Show closed"). |
| 7 | **Keys:** `/` puts the cursor in the search box (unless typing in a note), **Enter** searches (a single match opens at once), and **Esc** in the box clears the search. The arrow keys move through the matches as they move through the list today. |
| 8 | **No other change:** the stages, their rules, the follow-up flags, the notes, Undo of "I applied" and the menu counts work as today. The date of a stage change is still *now* (see below). No database change. |

### Date of the rejection

The rejection is recorded **today**, even when the email arrived a few days earlier. **Kept as it is** (owner, 21 Oct 2026): the exact date does not matter. If needed, the email can be pasted as a note.

## Layers

| Layer | Change |
| --- | --- |
| Models | `ApplicationPage` gains `Query` and `Terms` (the words looked for, to highlight them), as the Overview's page has; null without a search. |
| Services | `IApplicationTracker.GetPageAsync` takes an optional search text. With one, it filters open and closed applications by title and company with the Overview's `TextFilter` (same parsing and folding), and orders them as in decision 3. The counts for the chips and the menu are unchanged. |
| API | `GET api/Applications?q=flywire` (other parameters as today; `closed`, `stage` and `flagged` are ignored while `q` is set). A search over 200 characters is a 400. |
| Web | `Applications.cshtml`: the search form above the chips, the matches line in the footer, highlighted title and company. `Applications.cshtml.cs`: `?q=` read, kept in every link and in every form (`_ApplicationsState`); after a stage change during a search, stay on the same application (decision 5). `applications.js`: the `/` and Esc keys. `site.css`: the search box. |

## Out of scope

Searching notes or descriptions; searching jobs not marked as applied (the Overview does that); reading the email itself; recording a rejection for several applications at once; dating a stage change.

## Acceptance

1. On a copy of the owner's database: "flywire" lists the Flywire applications, open and closed, with the word highlighted; "senior .net hunt" requires all three words (title and company together); a company with an accent is found when typed without it.
2. A search finds an application whatever its stage, with "Show closed", a stage chip or the follow-up chip on or off.
3. Opening a match and pressing Rejected: the application closes (stage Rejected, a Stage line in its timeline, gone from the menu's open count), stays in the search results and in the pane, and Undo puts the previous stage back.
4. The search survives a note, a stage change, Undo, a fit check and the page arrows; Clear returns to the list as before the search.
5. `/` focuses the box, Esc clears it, a search with one match opens it.
6. No match shows the message pointing to the Overview.
7. Without a search, the page is exactly as today (the existing Applications UI tests pass).
