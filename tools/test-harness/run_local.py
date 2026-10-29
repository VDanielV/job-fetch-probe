"""Starts a complete test setup that never asks LinkedIn or the real Ollama anything:

  fake Ollama (11999) + test site (8766) + the API (5097) + the web app (5099), on a NEW, EMPTY data folder,
  with the saved search page test-site/search.html already in its input folder.

The real data folder and the normal ports (5080, 5081) are not touched, so this can run next to the real apps.
Ctrl+C stops everything. Python 3, standard library only; the .NET SDK must be on the PATH.

  python -I tools/test-harness/run_local.py                 # a new data folder under the temp folder
  python -I tools/test-harness/run_local.py --data <folder> # reuse a folder (for example a COPY of your database)
"""
import argparse
import glob
import json
import os
import shutil
import signal
import subprocess
import sys
import tempfile
import time
import urllib.request

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.dirname(os.path.dirname(HERE))
STATE = os.path.join(tempfile.gettempdir(), "jfp-harness")


def real_data_root():
    """Paths:DataRoot from the API's appsettings.json, to refuse it."""
    try:
        with open(os.path.join(REPO, "src", "JobFetchProbe.Api", "appsettings.json"), encoding="utf-8-sig") as f:
            return os.path.normcase(os.path.abspath(json.load(f)["Paths"]["DataRoot"]))
    except (OSError, KeyError, ValueError):
        return None


def wait_for(url, procs, seconds=180):
    """True once the URL answers; False after the time-out, or at once when one of the processes has ended."""
    end = time.time() + seconds
    while time.time() < end:
        if any(p.poll() is not None for p in procs):
            return False
        try:
            with urllib.request.urlopen(url, timeout=2):
                return True
        except Exception:  # not up yet
            time.sleep(1)
    return False


def start_app(project, env):
    """Starts a built app from its output folder (its content root), with the given environment."""
    dlls = glob.glob(os.path.join(REPO, "src", project, "bin", "Debug", "*", project + ".dll"))
    if not dlls:
        sys.exit(f"{project}.dll not found under src/{project}/bin/Debug.")
    dll = max(dlls, key=os.path.getmtime)
    return subprocess.Popen(["dotnet", dll], env=env, cwd=os.path.dirname(dll))


def main():
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--data", help="data folder to use (default: a new one under the temp folder)")
    parser.add_argument("--delay", type=int, default=3, help="FetchJobDetails:DelaySeconds (default 3)")
    parser.add_argument("--api-port", type=int, default=5097)
    parser.add_argument("--web-port", type=int, default=5099)
    parser.add_argument("--no-web", action="store_true", help="start the API only")
    parser.add_argument("--site-mode", default="ok", help="test site: ok, once, always or errors (see README)")
    parser.add_argument("--ollama-mode", default="apply",
                        help="fake Ollama: apply, maybe, skip, bad, slowN or seq:a,b,c (see README)")
    args = parser.parse_args()

    data = args.data or os.path.join(STATE, "data-" + time.strftime("%Y%m%d-%H%M%S"))
    data = os.path.abspath(data)
    real = real_data_root()
    if real and os.path.normcase(data) == real:
        sys.exit(f"Refused: {data} is the real data folder. Use a copy.")
    if os.path.normcase(data).startswith(os.path.normcase(REPO) + os.sep):
        sys.exit(f"Refused: {data} is inside the repository.")

    os.makedirs(os.path.join(data, "input"), exist_ok=True)
    if not os.path.exists(os.path.join(data, "database", "jobs.db")):
        shutil.copy(os.path.join(HERE, "test-site", "search.html"), os.path.join(data, "input", "test-search.html"))
    os.makedirs(STATE, exist_ok=True)
    for log in ("site-requests.log", "ollama-calls.log"):
        open(os.path.join(STATE, log), "w", encoding="utf-8").close()  # each setup starts with empty logs
    for name, value in (("site-mode", args.site_mode), ("ollama-mode", args.ollama_mode)):
        with open(os.path.join(STATE, name), "w", encoding="utf-8") as f:
            f.write(value)  # change them while it runs: the servers read them on every request

    api_url = f"http://127.0.0.1:{args.api_port}"
    web_url = f"http://127.0.0.1:{args.web_port}"
    env = dict(os.environ)
    env.update({
        "Paths__DataRoot": data,
        "Ollama__BaseUrl": "http://127.0.0.1:11999",
        "FetchJobDetails__DelaySeconds": str(args.delay),
        "FetchJobDetails__CircuitBreaker__CooldownSeconds": "2",
        "FetchJobDetails__CircuitBreaker__MaxCooldownSeconds": "2",
        "NO_PROXY": "127.0.0.1,localhost",
    })

    def stop(signum, frame):
        raise KeyboardInterrupt  # closing the window or a kill stops everything, as Ctrl+C does

    signal.signal(signal.SIGTERM, stop)
    if hasattr(signal, "SIGBREAK"):
        signal.signal(signal.SIGBREAK, stop)  # Windows: Ctrl+Break, closing the console

    # Build first, then start the built apps themselves (not "dotnet run"), so stopping them really stops them.
    print("Building...", flush=True)
    for project in ("JobFetchProbe.Api", "JobFetchProbe.Web"):
        if subprocess.run(["dotnet", "build", os.path.join(REPO, "src", project), "--nologo", "-v", "q"], cwd=REPO).returncode != 0:
            sys.exit(f"The build of {project} failed; see the output above.")

    procs = []
    try:
        py = sys.executable
        procs.append(subprocess.Popen([py, "-I", os.path.join(HERE, "fake-ollama", "fake_ollama.py"), "--state", STATE]))
        procs.append(subprocess.Popen([py, "-I", os.path.join(HERE, "test-site", "test_site.py"), "--state", STATE]))
        api_env = dict(env, ASPNETCORE_URLS=api_url)
        procs.append(start_app("JobFetchProbe.Api", api_env))
        print("Starting the API...", flush=True)
        if not wait_for(api_url + "/api/Journal/runs", procs):
            sys.exit("The API (or a test server) did not start; see the output above.")
        if not args.no_web:
            web_env = dict(env, ASPNETCORE_URLS=web_url, ASPNETCORE_ENVIRONMENT="Development",
                           JobCatalogApi__BaseUrl=api_url)
            procs.append(start_app("JobFetchProbe.Web", web_env))
            if not wait_for(web_url + "/", procs):
                sys.exit("The web app did not start; see the output above.")
        print(flush=True)
        if args.no_web:
            print(f"Ready.  API {api_url}   data {data}", flush=True)
        else:
            print(f"Ready.  Web {web_url}   API {api_url}   data {data}", flush=True)
        print(f"Modes and logs: {STATE}  (ollama-mode, site-mode, ollama-calls.log, site-requests.log)", flush=True)
        print("Start an acquisition on the Acquire page. Ctrl+C stops everything.", flush=True)
        while all(p.poll() is None for p in procs):
            time.sleep(1)
    except KeyboardInterrupt:
        pass
    finally:
        for p in procs:
            if p.poll() is None:
                p.terminate()
        for p in procs:
            try:
                p.wait(timeout=10)
            except subprocess.TimeoutExpired:
                p.kill()


if __name__ == "__main__":
    main()
