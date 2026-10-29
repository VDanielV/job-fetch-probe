"""A stand-in for Ollama, for testing the job fit without the real model.

Answers POST /api/chat with a fixed fit, chosen by the mode file in the state folder (see README).
Python 3, standard library only. Run:  python -I fake_ollama.py [--port 11999] [--state <folder>]
"""
import argparse
import http.server
import json
import os
import tempfile
import time

ANSWERS = {
    "apply": {"decision": "Apply", "fit": 78, "reason": "Strong .NET API and SQL match at senior level.",
              "hardSkip": False, "missingInformation": ["Salary range", "Office days per month"]},
    "maybe": {"decision": "Maybe", "fit": 52, "reason": "Good backend match but the domain is new.",
              "hardSkip": False, "missingInformation": []},
    # The model says Apply, but marks a hard skip: the app must store Skip.
    "skip": {"decision": "Apply", "fit": 46, "reason": "The role is platform engineering (Kubernetes, Terraform).",
             "hardSkip": True, "missingInformation": []},
}


def next_mode(state):
    """The mode for this call. 'seq:a,b,c' uses a, then b, then c, then keeps c."""
    path = os.path.join(state, "ollama-mode")
    mode = "apply"
    if os.path.exists(path):
        with open(path, encoding="utf-8") as f:
            mode = f.read().strip() or "apply"
    if mode.startswith("seq:"):
        items = mode[4:].split(",")
        mode = items[0]
        rest = items[1:] or items[:1]
        with open(path, "w", encoding="utf-8") as f:
            f.write("seq:" + ",".join(rest))
    return mode


def make_handler(state):
    class Handler(http.server.BaseHTTPRequestHandler):
        protocol_version = "HTTP/1.1"

        def read_body(self):
            if self.headers.get("Content-Length"):
                return self.rfile.read(int(self.headers["Content-Length"]))
            data = b""
            while True:  # chunked
                size = int(self.rfile.readline().strip(), 16)
                if size == 0:
                    self.rfile.readline()
                    return data
                data += self.rfile.read(size)
                self.rfile.readline()

        def reply(self, code, body):
            payload = json.dumps(body).encode()
            self.send_response(code)
            self.send_header("Content-Type", "application/json")
            self.send_header("Content-Length", str(len(payload)))
            self.end_headers()
            self.wfile.write(payload)

        def do_GET(self):
            self.reply(200, {"status": "fake Ollama is running"})

        def do_POST(self):
            self.read_body()
            mode = next_mode(state)
            with open(os.path.join(state, "ollama-calls.log"), "a", encoding="utf-8") as f:
                f.write(f"{time.strftime('%H:%M:%S')} {self.path} {mode}\n")
            if mode == "down":
                self.close_connection = True  # no answer at all, as when Ollama is not running
                self.connection.close()
                return
            if mode.startswith("slow"):
                time.sleep(int(mode[4:] or 8))
                mode = "apply"
            if mode == "bad":
                content = "this is not json"
            else:
                content = json.dumps(ANSWERS.get(mode, ANSWERS["apply"]))
            self.reply(200, {"model": "fake", "message": {"role": "assistant", "content": content}, "done": True})

        def log_message(self, *args):
            pass

    return Handler


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--port", type=int, default=11999)
    parser.add_argument("--state", default=os.path.join(tempfile.gettempdir(), "jfp-harness"))
    args = parser.parse_args()
    os.makedirs(args.state, exist_ok=True)
    server = http.server.ThreadingHTTPServer(("127.0.0.1", args.port), make_handler(args.state))
    print(f"fake Ollama on http://127.0.0.1:{args.port}, mode file {os.path.join(args.state, 'ollama-mode')}", flush=True)
    server.serve_forever()


if __name__ == "__main__":
    main()
