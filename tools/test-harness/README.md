# Test harness

Run the whole application, acquisition and job fit included, **without asking LinkedIn or the real Ollama anything**,
on a new empty database. This is how every feature since 18 Oct 2026 was checked before delivery.

| Part | What it is |
| --- | --- |
| `run_local.py` | Builds and starts everything below plus the API (port 5097) and the web app (5099) on a new data folder under the temp folder. The real data folder and the normal ports (5080, 5081) are never used, so it can run next to the real apps. Ctrl+C stops everything. |
| `test-site/` | A stand-in for the job site (port 8766): `search.html` is a saved search page with 6 made-up jobs whose links point here; `job.html` is a made-up job page with the markup the parser reads. |
| `fake-ollama/` | A stand-in for Ollama (port 11999) that answers every fit request with a fixed answer. |
| `check_run.py` | Waits for the latest acquisition to end and prints each job, its fit line, the seconds between requests, and the fits stored. |
| `db_hash.py` | One fingerprint of a whole database: take it before and after something that must change nothing. |
| `ui-tests/` | Browser checks with Playwright: `smoke.mjs` opens every page; `acquire.mjs` runs an acquisition from the Acquire page and checks the end, the Summary and the Runs page. |

Everything is Python 3 (standard library only) and Node for the browser checks. Modes and logs live in
`%TEMP%\jfp-harness` (Windows) or `/tmp/jfp-harness`, never in the repository or the data folder.

## Run it

```powershell
# from the repository root
python -I tools\test-harness\run_local.py
# then open http://127.0.0.1:5099/acquire and press Start acquisition
```

Options: `--data <folder>` reuses a folder (for example a **copy** of your database; the real data folder is refused),
`--delay 3` the pause between requests, `--no-web` the API only, `--site-mode` and `--ollama-mode` (below).

After a run, in a second window:

```powershell
python -I tools\test-harness\check_run.py --data <the data folder run_local.py printed>
```

## Modes

Set at start with `--site-mode` / `--ollama-mode`, or change them while it runs by writing the file
(`Set-Content $env:TEMP\jfp-harness\ollama-mode slow20`): both servers read their mode on every request.

| Test site (`site-mode`) | What happens |
| --- | --- |
| `ok` (default) | Every job page is served. |
| `once` | Job 1000003 is gone (404); job 1000005 is refused once (999), then served. |
| `always` | Jobs from 1000005 are refused every time: the circuit breaker opens three times and stops the run. |
| `errors` | Jobs 1000002 to 1000004 fail once with 500. |

| Fake Ollama (`ollama-mode`) | Answer |
| --- | --- |
| `apply` (default) | Fit 78, Apply |
| `maybe` | Fit 52, Maybe |
| `skip` | Fit 46, Apply with a hard skip: the app must store Skip |
| `bad` | Not JSON: the job stays unscored |
| `down` | No answer, the connection is closed: as when Ollama is not running |
| `slowN` | Apply after N seconds (`slow20`): to check the pause, or to press Stop during a score |
| `seq:a,b,c` | a for the first call, b for the second, then c for every later one |

## Browser checks

```powershell
cd tools\test-harness\ui-tests
npm install
npx playwright install chromium
node smoke.mjs      # any time, against http://127.0.0.1:5099 (WEB=... for another address)
node acquire.mjs    # on a NEW data folder: it starts an acquisition (SHOTS=<folder> saves screenshots)
```

Both exit with 1 when a check fails.

## Checks used so far

| Feature | Setup | Expected |
| --- | --- | --- |
| Job fit during acquisition | `--site-mode once --ollama-mode seq:maybe,skip,bad,apply` | 5 read, 4 scored (52 Maybe, 46 Skip hard skip, 78, 78), job 3 Gone, job 4 "Not scored" |
| The pause includes the score | `--ollama-mode slow5` | about 8 s between requests (5 + 3) |
| Ollama down | `--ollama-mode down` | 3 "Not scored", then "scoring is off", every job still read |
| Stop during a score | `--ollama-mode slow20`, press Stop | no fit stored for that job, its description kept, run Stopped |
| Circuit breaker | `--site-mode always` | the run stops after 3 openings; the next run continues |
| New vs already known | run twice with the same page (copy `test-site/search.html` into the input folder again) | second run: 0 new, 6 already known, no requests |
| Pages change nothing | `db_hash.py` before and after opening the Runs pages | same fingerprint |
