# Spec: filtering the Overview by city and work mode

Status: agreed 18 Oct 2026. Stands on its own, like `spec-archive.md` and `spec-filter.md`.

## Goal

Narrow the Overview to the cities the owner can work in, and to remote, hybrid or on-site jobs, with one click each, alone or together with the text filter.

## Decisions

| # | Decision |
| --- | --- |
| 1 | A row of **city** chips under the search box, each with its count: the 10 places with the most jobs, "Spain, no city" always, and **Other (n)** for the rest. No full list of places. |
| 2 | A row of **work mode** chips: Remote, Hybrid, On-site, Not stated. |
| 3 | Within a row: or. Between the rows and the text filter: and. |
| 4 | The counts follow the other filters (text, and the other row); a chip at 0 stays, greyed. |
| 5 | The Location column shows the cleaned city and a work-mode tag, with the original text underneath. |
| 6 | No database change, nothing in the acquisition: the location text is read as it is stored. |

## Reading a location

The stored text (from the job card or page) is split into a place and a work mode by `LocationText` (Services/Filter):

1. **Work mode** from the text in brackets, or anywhere in it: "Híbrido"/"Hybrid" → Hybrid; "En remoto"/"Remote" → Remote; "Presencial"/"On-site" → On-site; none → Not stated. Accents and capitals are ignored.
2. **Place**: the bracketed part is removed; the text before the first comma, then before the first "/", is the place ("Valencia/València (Híbrido)" → Valencia; "Paterna, Valencian Community, Spain" → Paterna). "Greater X Area" and "X y alrededores" become X.
3. **Variants** are merged by comparing without accents and capitals, plus a short list of spellings: Almusafes → Almussafes, Picasent → Picassent, Sagunt → Sagunto, Puzol → Puçol.
4. "Spain" or "España" alone → **Spain, no city**. No location at all → **No location**.

A chip's key is the place without accents, in lower case, with dashes ("valencia", "spain", "other"); its label is the spelling seen first.

## API

`GET api/JobCatalog?…&place=valencia&place=paterna&mode=hybrid` (both repeatable; unknown values ignored). The page gains `places` and `modes`, the chips: `key`, `label`, `count`, `selected`. Each job gains `place` and `workMode`. `unfilteredTotal` is set whenever any filter (text, place or mode) is on.

## Overview

- Under the search box: "Location" with the city chips, then "Work mode" with its four chips. A selected chip is dark with ✕; a click toggles it. Changing a chip goes back to page 1.
- The result line ("**12** of 106 match"), "Tick all matches", the footer wording and the empty row apply whenever any filter is on; "Clear filters" clears text and chips.
- The selection is in the address and kept by paging, archiving (A and C, which then opens the next matching job), Show archived and the page size.

## Acceptance

1. Every location text in the owner's data is read as the table delivered with this change shows (reviewed by the owner).
2. Each city chip, each mode chip, combinations, and combinations with a text filter list exactly the expected jobs; the chip counts are right.
3. Archiving, paging, page size and the text filter keep working with chips on; without chips the Overview is as before.
