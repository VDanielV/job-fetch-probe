# Spec: filtering the Overview by text, and the page size

Status: agreed 18 Oct 2026. Like `spec-archive.md`, this spec stands on its own; `spec.md` is not updated until the owner asks for the docs to be brought up to date.

## Goal

Find jobs by what they say: a word or phrase in the title, or in the title and the description. Together with archiving, it lets the owner clear "everything that mentions X" quickly, and find the jobs worth applying to.

## Decisions

| # | Decision |
| --- | --- |
| 1 | One search box above the table. The filter applies on **Enter** (not while typing); changing the scope applies at once. |
| 2 | Scope: **Title + description** (default) or **Title**. |
| 3 | Rules: every word must appear; `-word` must not appear; `"two words"` is a phrase. Capitals and accents are ignored; a word also matches inside a longer word. |
| 4 | "Tick all n matches" ticks the matches **on the current page** for the archive bar. |
| 5 | 25 or 50 jobs per page, chosen in the table footer and remembered in the browser. |
| 6 | Keys: **/** focuses the box; in the details pane **N** / **Shift+N** jump to the next / previous match in the description (↓ / ↑ and 1 / 2 / 3 keep their meaning). |
| 7 | Nothing in the acquisition or the database changes; no migration. |

## Matching

The query is split into terms:

| Typed | Meaning |
| --- | --- |
| `senior .net` | both words must appear, anywhere, in any order |
| `.net -java` | ".net" must appear, "java" must not |
| `"tech lead"` | the exact phrase |
| `-relocation` | every job that does not mention relocation |

- Text is compared after removing accents and capitals: `tecnico` finds "Técnico", `DESARROLLADOR` finds "desarrollador".
- A term matches inside a longer word: `java` also finds "JavaScript".
- With the scope **Title**, only the title is searched; with **Title + description**, a term may be in either. An excluded term (`-java`) excludes the job if it appears in the searched text.
- An empty query, or one made only of spaces or quotes, means no filter.

The filter applies to the jobs listed: the jobs to review, or all jobs when "Show archived" is ticked. Jobs keep their order (newest job IDs first); paging and counts are over the matches.

## API

`GET api/JobCatalog?page=&pageSize=&includeArchived=&q=&in=all|title` (`in` defaults to `all`; `pageSize` 1–100 as before).

The page gains:

- `terms`: the positive terms (words and phrases) of the query, for highlighting; empty without a filter.
- `unfilteredTotal`: the number of jobs listed without the filter (the "of 106"); null without a filter.
- On each job, `snippet`: when the scope includes the description and the job matches there but not in its title, about 140 characters of the description around the first match; otherwise null.

`total` is the number of matches.

## Overview

- Above the table: the search box (with ✕ to clear), the scope switch, and "**7** of 106 match" while a filter is on. "Tick all 7 matches" sits on the right while a filter is on and the page has matches to tick (it ticks this page's matches only: "Tick all 7 matches on this page" when there are more pages).
- In the table: matched terms highlighted in the titles; under a title, "in the description: …snippet…" when only the description matched. With no match: "No job to review matches "java"." and a link to clear the filter.
- In the details pane: matched terms highlighted in the description, a line "“java” appears 3 times in the description" (all terms counted together), and N / Shift+N scroll to the next / previous highlight.
- Footer: "Showing 1–25 of 7 matches" and a selector **25 / 50** per page. The choice is kept in a cookie (`overview.size`) and in the address (`?size=50`).
- Everything is kept in the address: `?q=java&in=all&size=50&archived=1&p=2&job=…`. Paging, opening a job, Show archived, archiving with the bar (A) or from the pane (C, which then opens the next **match**) all keep it.

## Out of scope

Searching company or location, saved searches, archiving all matches across pages, sorting by relevance, a search index (worth it only at many thousands of jobs: the filter reads the title and description of the listed jobs on each filtered view).

## Acceptance

1. `senior .net`, `.net -java`, `"tech lead"`, `tecnico`, `DESARROLLADOR` each list exactly the jobs the rules say, in both scopes, checked against the database.
2. The counts are right: "n of m match", the footer and the page numbers; page 2 of a filter shows the next matches.
3. Highlights and snippets show; the pane counts the matches and N / Shift+N move between them.
4. "Tick all matches" then Archive archives only this page's matches; the filter stays; C under a filter opens the next match.
5. 25 / 50 per page works and is remembered after a restart of the browser tab.
6. Without a filter the Overview, archiving and the Acquire page behave as before.
