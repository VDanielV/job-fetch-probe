# Working agreement for Claude

## How we work

1. **Plan first.** For every request, first present a plan that makes clear what will change: new files, changed files (project and file), what moves where, what stays as it is, and how it will be checked. Explain the reasoning.
2. **No changes before "go".** Do not change code, documents or files until the owner approves the plan. If the owner adjusts the plan, follow the adjusted version.
3. **Report what changed.** After the work, say briefly what changed, how it was checked, and anything left open.
4. **Keep the owner's edits.** Before overwriting a file, check that it has not changed since it was last delivered; if it has, keep the owner's version and work on top of it. Never write temporary files into the repository or the data folder.

## Architecture rules

- **Layers:** Web → Api → Services → (Data | Infrastructure). Each layer uses only the one below it.
  - Controllers use Services types only (for example `IJobRetriever`, `IAcquisitionRunner`). Data and Infrastructure are referred to only in the API's `Program.cs`, which wires them into dependency injection.
  - The order of the steps lives in Services (`IAcquisitionService`). Infrastructure (HTTP, saved pages, input folder) and Data (SQLite) implement the Services interfaces.
- **Models** (`JobFetchProbe.Models`) holds DTOs only: no interfaces, no logic, no database rows.
- **Interfaces** that Services needs from the outside world live in `Services/Abstractions`; Infrastructure and Data implement them. Interfaces are never moved to Models.
- **Data** (`JobFetchProbe.Data`) owns the database: `JobsDbContext`, entities (`Entities/`, suffix `Row`, never `Entity`), one configuration class per table, migrations and repositories. Entities never leave the Data project; repositories return models.
- **The journal** (progress of each run) is isolated in `Journal` folders in every layer; the step folders (`FetchJobListing`, `FetchJobDetails`) never refer to it. Writing it must never stop or slow a step.
- **One type per file**, named after the type; folders group files by area; namespaces follow folders.
- **Helpers** in Services: repeated or self-contained actions (parsing, URL handling, small calculations) go into `internal static` helper classes of pure functions (no state, no I/O, not in dependency injection). Shared by both steps → `Services/Parsing/`; one step only → that step's folder; journal → `Journal/`. The classes that do the work stay behind their interfaces.
- SOLID: depend on abstractions, keep each class to one responsibility.

## Boundaries of the tool

- Identify honestly (fixed User-Agent), send no cookies, one request at a time with a pause, no quick retries.
- Never get around a refusal: no browser imitation, no running or solving bot checks, no identity rotation, no requests to `/jobs-guest/` pagination.

## Practical

- Code lives in this repository; data lives outside it, under `Paths:DataRoot` (see README, "Folders").
- Documents to keep up to date with every change: `README.md`, `docs/spec.md`, `docs/plan.md`, and `docs/database.md` when tables change.
