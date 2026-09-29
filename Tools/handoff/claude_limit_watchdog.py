#!/usr/bin/env python3
"""Hand the Billiard Rogue build to Cursor's CLI agent while Claude Code is rate-limited.

Watches the Claude Code session transcripts of this project for the synthetic API error
"You've hit your session limit · resets 9:50am (...)". When one appears, it starts
`cursor-agent` headless in the repo with the brief in Docs/BilliardRogue/CURSOR_HANDOFF.md,
and stops it at the reset time so Claude can resume on a quiet tree.

Run:   nohup python3 Tools/handoff/claude_limit_watchdog.py >/dev/null 2>&1 &
Stop:  kill $(cat Tools/handoff/watchdog.pid)
Check: cat Tools/handoff/state.json ; ls Tools/handoff/logs/
Test:  python3 Tools/handoff/claude_limit_watchdog.py --selftest
Auth:  once, in a terminal: `~/.local/bin/cursor-agent login` (or save a key in ~/.cursor/billiard_rogue_api_key)
"""
import argparse
import datetime as dt
import glob
import json
import os
import re
import signal
import subprocess
import sys
import time

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
HERE = os.path.join(ROOT, "Tools", "handoff")
LOGS = os.path.join(HERE, "logs")
STATE = os.path.join(HERE, "state.json")
PIDFILE = os.path.join(HERE, "watchdog.pid")
TRANSCRIPTS = os.environ.get("BR_WATCHDOG_TRANSCRIPTS",
                             os.path.expanduser("~/.claude/projects/-Users-simonbut-project-VibeProject3"))
CURSOR = os.path.expanduser("~/.local/bin/cursor-agent")
# Auth: run `cursor-agent login` once in a terminal, or save an API key in this file (chmod 600).
API_KEY_FILE = os.path.expanduser("~/.cursor/billiard_rogue_api_key")

LIMIT_RE = re.compile(r"hit your session limit\s*·\s*resets\s*(\d{1,2})(?::(\d{2}))?\s*(am|pm)", re.I)
POLL_SECONDS = float(os.environ.get("BR_WATCHDOG_POLL", "20"))
FRESH_SECONDS = 15 * 60          # only react to limit errors written in the last 15 minutes
STOP_MARGIN = dt.timedelta(minutes=2)       # stop Cursor this long after the reset time
MIN_WINDOW = dt.timedelta(minutes=15)       # don't start Cursor for a shorter gap
MAX_RUNS_PER_WINDOW = 4                     # relaunch if Cursor finishes early, at most this often
RELAUNCH_BACKOFF = dt.timedelta(minutes=3)  # never relaunch sooner than this after a start


def log(msg):
    os.makedirs(LOGS, exist_ok=True)
    line = f"{dt.datetime.now():%Y-%m-%d %H:%M:%S} {msg}"
    with open(os.path.join(LOGS, "watchdog.log"), "a", encoding="utf-8") as fh:
        fh.write(line + "\n")


def next_reset(hour, minute, ampm, base):
    """First occurrence of the clock time at or after `base` (when the limit message was written)."""
    h = hour % 12 + (12 if ampm.lower() == "pm" else 0)
    reset = base.replace(hour=h, minute=minute, second=0, microsecond=0)
    if reset < base - dt.timedelta(minutes=1):
        reset += dt.timedelta(days=1)
    return reset


def parse_limit(line, now):
    """Return the reset datetime if this transcript line is a fresh session-limit error."""
    if "hit your session limit" not in line:
        return None
    try:
        rec = json.loads(line)
    except ValueError:
        return None
    if rec.get("type") != "assistant" or not rec.get("isApiErrorMessage"):
        return None
    ts = rec.get("timestamp")
    written = now
    if ts:
        written = dt.datetime.fromisoformat(ts.replace("Z", "+00:00")).astimezone().replace(tzinfo=None)
        if (now - written).total_seconds() > FRESH_SECONDS:
            return None
    m = LIMIT_RE.search(json.dumps(rec.get("message", {}), ensure_ascii=False))
    if not m:
        return None
    return next_reset(int(m.group(1)), int(m.group(2) or 0), m.group(3), written)


class Watchdog:
    def __init__(self, dry_run):
        self.dry_run = dry_run
        self.offsets = {}
        self.proc = None
        self.log_fh = None
        self.stop_at = None
        self.runs_this_window = 0
        self.started_at = None

    def scan(self, now):
        reset = None
        for path in glob.glob(os.path.join(TRANSCRIPTS, "*.jsonl")):
            size = os.path.getsize(path)
            if path not in self.offsets:
                self.offsets[path] = size  # start at the end: never react to history
                continue
            if size < self.offsets[path]:
                self.offsets[path] = 0
            with open(path, "r", encoding="utf-8", errors="replace") as fh:
                fh.seek(self.offsets[path])
                for line in fh:
                    found = parse_limit(line, now)
                    if found:
                        reset = found
                self.offsets[path] = fh.tell()
        return reset

    def running(self):
        return self.proc is not None and self.proc.poll() is None

    def start_cursor(self, now):
        stop_at = self.stop_at
        prompt = (
            f"You are continuing the Billiard Rogue build (repo {ROOT}) because Claude Code hit its usage limit. "
            f"It is now {now:%Y-%m-%d %H:%M}. Claude resumes at {stop_at:%H:%M}; you will be stopped then, so stop starting "
            f"new work at {(stop_at - dt.timedelta(minutes=10)):%H:%M}. Read Docs/BilliardRogue/CURSOR_HANDOFF.md and follow it "
            f"exactly (its Active queue is your work list; resume killed items from their progress notes). Commit after every "
            f"compiling milestone and log your work in Docs/BilliardRogue/progress/cursor-session.md."
        )
        cmd = [CURSOR, "-p", "--force", "--trust", "--output-format", "text", "--workspace", ROOT, prompt]
        os.makedirs(LOGS, exist_ok=True)
        log_path = os.path.join(LOGS, f"cursor-{now:%Y%m%d-%H%M%S}.log")
        log(f"starting cursor-agent until {stop_at:%H:%M} (log {log_path})" + (" [dry-run]" if self.dry_run else ""))
        if self.dry_run:
            print("DRY RUN:", cmd[:-1], "<prompt>")
            self.runs_this_window += 1
            self.started_at = now
            return
        env = dict(os.environ)
        if os.path.exists(API_KEY_FILE):  # optional: a key the user saved themselves; never logged
            with open(API_KEY_FILE, encoding="utf-8") as fh:
                env["CURSOR_API_KEY"] = fh.read().strip()
        self.log_fh = open(log_path, "w", encoding="utf-8")
        self.proc = subprocess.Popen(cmd, cwd=ROOT, stdout=self.log_fh, stderr=subprocess.STDOUT,
                                     stdin=subprocess.DEVNULL, start_new_session=True, env=env)
        self.runs_this_window += 1
        self.started_at = now

    def stop_cursor(self, reason):
        if not self.running():
            return
        log(f"stopping cursor-agent ({reason})")
        try:
            os.killpg(self.proc.pid, signal.SIGTERM)
            self.proc.wait(timeout=60)
        except (subprocess.TimeoutExpired, ProcessLookupError):
            try:
                os.killpg(self.proc.pid, signal.SIGKILL)
            except ProcessLookupError:
                pass
        self.proc = None
        if self.log_fh:
            self.log_fh.close()
            self.log_fh = None

    def write_state(self, now):
        state = {
            "updated": f"{now:%Y-%m-%d %H:%M:%S}",
            "cursor_running": self.running(),
            "cursor_pid": self.proc.pid if self.running() else None,
            "stop_at": f"{self.stop_at:%Y-%m-%d %H:%M}" if self.stop_at else None,
            "runs_this_window": self.runs_this_window,
        }
        with open(STATE, "w", encoding="utf-8") as fh:
            json.dump(state, fh, indent=1)

    def tick(self):
        now = dt.datetime.now()
        reset = self.scan(now)
        if reset and (self.stop_at is None or reset > self.stop_at):
            log(f"limit detected, resets at {reset:%Y-%m-%d %H:%M}")
            self.stop_at = reset + STOP_MARGIN
            self.runs_this_window = 0
        if self.stop_at:
            if now >= self.stop_at:
                self.stop_cursor("reset time reached")
                self.stop_at = None
                self.runs_this_window = 0
                self.started_at = None
            elif not self.running() and self.stop_at - now >= MIN_WINDOW \
                    and self.runs_this_window < MAX_RUNS_PER_WINDOW \
                    and (self.started_at is None or now - self.started_at >= RELAUNCH_BACKOFF):
                if self.runs_this_window:
                    log("cursor-agent exited before the reset; relaunching for the remaining window")
                self.start_cursor(now)
        self.write_state(now)

    def run(self):
        with open(PIDFILE, "w") as fh:
            fh.write(str(os.getpid()))
        log(f"watchdog started (pid {os.getpid()})")
        signal.signal(signal.SIGTERM, lambda *_: (self.stop_cursor("watchdog terminated"), sys.exit(0)))
        while True:
            try:
                self.tick()
            except Exception as exc:  # keep watching whatever happens
                log(f"error: {exc!r}")
            time.sleep(POLL_SECONDS)


def selftest():
    now = dt.datetime(2026, 9, 29, 9, 40)
    msg = {"type": "assistant", "isApiErrorMessage": True, "timestamp": "2026-09-29T01:39:00Z",
           "message": {"content": [{"type": "text", "text": "You've hit your session limit · resets 9:50am (Asia/Hong_Kong)"}]}}
    got = parse_limit(json.dumps(msg), now)
    assert got == dt.datetime(2026, 9, 29, 9, 50), got
    assert next_reset(11, 50, "pm", dt.datetime(2026, 9, 29, 23, 45)) == dt.datetime(2026, 9, 29, 23, 50)
    assert next_reset(4, 50, "am", dt.datetime(2026, 9, 29, 23, 55)) == dt.datetime(2026, 9, 30, 4, 50)
    assert next_reset(9, 50, "am", dt.datetime(2026, 9, 29, 9, 50, 30)) == dt.datetime(2026, 9, 29, 9, 50)
    stale = dict(msg, timestamp="2026-09-28T01:39:00Z")
    assert parse_limit(json.dumps(stale), now) is None
    assert parse_limit(json.dumps(dict(msg, isApiErrorMessage=False)), now) is None
    assert os.path.exists(CURSOR), CURSOR
    print("selftest OK")


if __name__ == "__main__":
    ap = argparse.ArgumentParser()
    ap.add_argument("--selftest", action="store_true")
    ap.add_argument("--dry-run", action="store_true")
    a = ap.parse_args()
    if a.selftest:
        selftest()
    else:
        Watchdog(a.dry_run).run()
