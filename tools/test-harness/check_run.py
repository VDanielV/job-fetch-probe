"""Waits for the latest acquisition to end, then prints what step 2 did: each job, its fit line, the gaps between
requests to the test site, and the fits stored in the database. Python 3, standard library only.

  python -I tools/test-harness/check_run.py --data <the data folder run_local.py printed>
"""
import argparse
import datetime
import json
import os
import sqlite3
import tempfile
import time
import urllib.request

STATE = os.path.join(tempfile.gettempdir(), "jfp-harness")


def get(url):
    with urllib.request.urlopen(url, timeout=10) as r:
        return json.load(r)


def main():
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--api", default="http://127.0.0.1:5097/api")
    parser.add_argument("--data", required=True)
    parser.add_argument("--timeout", type=int, default=900, help="seconds to wait for the run to end")
    args = parser.parse_args()

    run = None
    end = time.time() + args.timeout
    while time.time() < end:
        runs = [r for r in get(args.api + "/Journal/runs?take=6") if r["step"] == "JobDetails"]
        if runs and runs[0]["state"] not in ("Running", "Starting"):
            run = runs[0]
            break
        time.sleep(2)
    if run is None:
        raise SystemExit("No finished step 2 run within the time-out.")

    print(f"step 2 run {run['runId']}: {run['state']} | {run['message']}")
    details = get(f"{args.api}/Journal/job-details/runs/{run['runId']}?after=0")
    for e in details["entries"]:
        if e["kind"] in ("Job", "Assessed", "AssessOff", "Stopped", "CircuitOpened"):
            print(f"  {e['at'][11:19]} {e['kind']:13} {e.get('jobId') or '':8} {e.get('status') or '':7} {e.get('message') or ''}")

    log = os.path.join(STATE, "site-requests.log")
    if os.path.exists(log):
        with open(log, encoding="utf-8") as f:
            times = [datetime.datetime.strptime(line[:8], "%H:%M:%S") for line in f if line[:2].isdigit()]
        gaps = [int((b - a).total_seconds()) for a, b in zip(times, times[1:])]
        print(f"  requests to the test site: {len(times)}; seconds between them: {gaps}")

    db = sqlite3.connect(os.path.join(args.data, "database", "jobs.db"))
    fits = db.execute("select j.SourceJobId, f.Fit, f.Decision, f.HardSkip from JobFitAssessments f "
                      "join Jobs j on j.Id = f.JobId order by f.Id").fetchall()
    print(f"  fits stored: {fits}")
    print(f"  jobs: {db.execute('select SourceJobId, Status from Jobs order by Id').fetchall()}")


if __name__ == "__main__":
    main()
