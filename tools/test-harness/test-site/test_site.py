"""A stand-in for the job site, so an acquisition can run without asking LinkedIn anything.

Serves job.html for /jobs/view/<anything>-<number>; the mode file in the state folder makes some jobs fail (see README).
Every request is logged to site-requests.log in the state folder, with its time, to check the pause between requests.
Python 3, standard library only. Run:  python -I test_site.py [--port 8766] [--state <folder>]
"""
import argparse
import http.server
import os
import re
import tempfile
import time

HERE = os.path.dirname(os.path.abspath(__file__))


def make_handler(state):
    seen = {}

    def mode():
        path = os.path.join(state, "site-mode")
        if os.path.exists(path):
            with open(path, encoding="utf-8") as f:
                return f.read().strip()
        return "ok"

    class Handler(http.server.BaseHTTPRequestHandler):
        def status(self, code):
            self.send_response(code)
            self.send_header("Content-Length", "0")
            self.end_headers()

        def do_GET(self):
            with open(os.path.join(state, "site-requests.log"), "a", encoding="utf-8") as f:
                f.write(f"{time.strftime('%H:%M:%S')} {self.path}\n")
            m = re.search(r"/jobs/view/.*-(\d+)", self.path)
            if not m:
                return self.status(404)
            job = int(m.group(1))
            seen[job] = seen.get(job, 0) + 1
            current = mode()
            if current == "once":
                # job 3 is gone; job 5 is refused once (999, as LinkedIn does), then read
                if job == 1000003:
                    return self.status(404)
                if job == 1000005 and seen[job] == 1:
                    return self.status(999)
            if current == "always" and job >= 1000005:
                return self.status(999)  # refused every time: the circuit breaker stops the run
            if current == "errors" and job in (1000002, 1000003, 1000004) and seen[job] == 1:
                return self.status(500)
            with open(os.path.join(HERE, "job.html"), "rb") as f:
                body = f.read()
            self.send_response(200)
            self.send_header("Content-Type", "text/html; charset=utf-8")
            self.send_header("Content-Length", str(len(body)))
            self.end_headers()
            self.wfile.write(body)

        def log_message(self, *args):
            pass

    return Handler


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--port", type=int, default=8766)
    parser.add_argument("--state", default=os.path.join(tempfile.gettempdir(), "jfp-harness"))
    args = parser.parse_args()
    os.makedirs(args.state, exist_ok=True)
    server = http.server.ThreadingHTTPServer(("127.0.0.1", args.port), make_handler(args.state))
    print(f"test site on http://127.0.0.1:{args.port}, mode file {os.path.join(args.state, 'site-mode')}", flush=True)
    server.serve_forever()


if __name__ == "__main__":
    main()
