"""A card batch's end-of-batch checks, in order, with ONE summary out (story DEV-5).

    python -u tools/batch_checks.py "Card A;Card B"                     # every step
    python -u tools/batch_checks.py "Card A;Card B" --revert SPEC --bench
    python -u tools/batch_checks.py "Card A;Card B" --from probe150     # resume after a fix

The steps, each with a timeout that kills the whole process tree and names the hung test:

  build       dotnet build MagicCore.Tests
  golden-out  the golden replays with the batch's cards OUT (MAGIC_UNSCRIPTED — no file edited): the
              engine change must move no recorded game. A moved game is named with its trace file.
  golden-in   the same fixtures regenerated with the cards IN (MAGIC_GOLDEN_UPDATE=1): which games moved
  revert      tools/revert_check.py SPEC (only with --revert)
  probe10     the fuzz probe at 10 games — a hang shows here in seconds; on one, each card is timed alone
  probe150    the fuzz probe at 150 games: unfinished games, and each card's casts / activations
  bench       tools/bench_vs_head.sh (only with --bench: a hot path was touched)
  suite       the full MagicCore suite (it builds: a revert-check leaves the DLL stale)
  docs        tools/magic_docs.py --check

Stops at the first failure with the lines that matter, never a whole log. Each step prints as it starts
and ends, with its minutes. The golden-in step rewrites the golden files: read `git diff` on them before
the commit. Long runs (probe150, suite) still need Shayne's go-ahead, as any long run does.
"""

import argparse
import os
import re
import subprocess
import sys
import time
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
TESTS = "MagicCore.Tests"
GOLDEN = ["GoldenReplay.txt", "GoldenCardReplay.txt", "GoldenAiReplay.txt"]
# CardReplayTests is [Explicit]: an OR filter doesn't select it, so it is a run of its own.
GOLDEN_FILTERS = ["FullyQualifiedName~RandomGameTests|FullyQualifiedName~GreedyAiReplayTests",
                  "FullyQualifiedName~CardReplayTests"]
STEPS = ["build", "golden-out", "golden-in", "revert", "probe10", "probe150", "bench", "suite", "docs"]


def run(cmd, timeout, env=None):
    """(exit code or None on timeout, output, seconds). A timeout kills the whole tree."""
    t0 = time.time()
    proc = subprocess.Popen(
        cmd, cwd=ROOT, env={**os.environ, **(env or {})}, stdout=subprocess.PIPE, stderr=subprocess.STDOUT,
        text=True, encoding="utf-8", errors="replace",
        creationflags=getattr(subprocess, "CREATE_NEW_PROCESS_GROUP", 0),
    )
    try:
        out, _ = proc.communicate(timeout=timeout)
        return proc.returncode, out, time.time() - t0
    except subprocess.TimeoutExpired:
        if sys.platform == "win32":
            subprocess.run(["taskkill", "/T", "/F", "/PID", str(proc.pid)], capture_output=True)
        else:
            proc.kill()
        out, _ = proc.communicate()
        return None, out or "", time.time() - t0


def dotnet_test(filt, timeout, env=None, build=False, project=TESTS):
    """(ok, summary line, lines that matter, seconds). --blame-hang names a test that stops progressing;
    no filter runs the normal suite (a name filter would also run the [Explicit] tests)."""
    cmd = ["dotnet", "test", project, "--blame-hang", "--blame-hang-timeout", f"{max(timeout - 20, 10)}s",
           "--blame-hang-dump-type", "none"] + (["--filter", filt] if filt else []) + ([] if build else ["--no-build"])
    code, out, secs = run(cmd, timeout, env)
    summary = next((l.strip() for l in out.splitlines() if re.match(r"\s*(Passed|Failed)!", l)), "")
    lines = out.splitlines()
    notes, failed = [], [i for i, l in enumerate(lines) if re.match(r"\s+Failed \S", l)]
    for i in failed[:8]:  # each failure: its name, then its message's first line and the trace file
        msg = [l.strip() for l in lines[i + 2:i + 8] if l.strip() and not l.strip().startswith("Stack Trace")]
        trace = next((l.split("trace:", 1)[1].strip() for l in msg if "trace:" in l), "")
        notes.append(f"{lines[i].strip()} — {msg[0][:150] if msg else ''}" + (f" — {trace}" if trace else ""))
    if len(failed) > 8:
        notes.append(f"… and {len(failed) - 8} more failed")
    notes += [l.strip() for l in lines if "error CS" in l]
    for i, l in enumerate(lines):
        if "test running when the crash occurred" in l and i + 1 < len(lines):
            notes.append("HUNG: " + lines[i + 1].strip())
    if code is None:
        notes.append(f"TIMEOUT after {secs:.0f}s")
    ok = code == 0 and summary.startswith("Passed!")
    if not ok and not notes:
        notes = [l for l in lines if l.strip()][-8:]
    return ok, summary, list(dict.fromkeys(notes)), secs


def golden_moves():
    """{file: (moved keys, added keys)} from git's diff of the golden files."""
    res = {}
    for name in GOLDEN:
        diff = subprocess.run(["git", "diff", "-U0", "--", f"{TESTS}/{name}"], cwd=ROOT, capture_output=True,
                              text=True, encoding="utf-8").stdout
        key = lambda l: l[1:].split(" :", 1)[0]
        gone = {key(l) for l in diff.splitlines() if l.startswith("-") and not l.startswith("---") and " :" in l}
        came = {key(l) for l in diff.splitlines() if l.startswith("+") and not l.startswith("+++") and " :" in l}
        res[name] = (sorted(gone & came), sorted(came - gone))
    return res


def probe(cards, games, timeout):
    env = {"MAGIC_PROBE_CARDS": ";".join(cards), "MAGIC_PROBE_GAMES": str(games)}
    code, out, secs = run(["dotnet", "test", TESTS, "--no-build", "--filter", "FullyQualifiedName~CardProbeTests",
                           "--logger", "console;verbosity=normal"], timeout, env)
    report = [l.strip() for l in out.splitlines() if re.search(r"\d+ games, \d+ unfinished|: cast \d+", l)]
    failed = [l.strip() for l in out.splitlines() if "Error Message" in l or "most random games" in l]
    return code, report, failed, secs


def main():
    sys.stdout.reconfigure(encoding="utf-8")
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("cards", help="the batch's new card names, ;-separated")
    ap.add_argument("--revert", help="a tools/revert_check.py spec")
    ap.add_argument("--bench", action="store_true", help="time against HEAD (a hot path was touched)")
    ap.add_argument("--from", dest="start", choices=STEPS, default="build", help="resume at this step")
    a = ap.parse_args()
    cards = [c.strip() for c in a.cards.split(";") if c.strip()]
    steps = STEPS[STEPS.index(a.start):]
    steps = [s for s in steps if (s != "revert" or a.revert) and (s != "bench" or a.bench)]
    summary = []
    t_all = time.time()

    def done(step, ok, text, secs, notes=()):
        summary.append(f"{'ok  ' if ok else 'FAIL'} {step:<10} {text} ({secs / 60:.1f} min)")
        print(f"   {'ok' if ok else 'FAILED'}: {text} ({secs / 60:.1f} min)", flush=True)
        for n in notes[:12]:
            print(f"     {n}", flush=True)
        if not ok:
            print("\n" + "\n".join(summary) + f"\n\nstopped at {step} — fix it, then --from {step}")
            sys.exit(1)

    for k, step in enumerate(steps, 1):
        print(f"[{k}/{len(steps)}] {step} … ({(time.time() - t_all) / 60:.1f} min in)", flush=True)
        if step == "build":
            code, out, secs = run(["dotnet", "build", TESTS, "-v", "q"], 900)
            errs = sorted({l.strip() for l in out.splitlines() if " error " in l})
            done(step, code == 0, "built" if code == 0 else f"{len(errs)} errors", secs, errs)
        elif step == "golden-out":
            for f in GOLDEN_FILTERS:
                ok, s, notes, secs = dotnet_test(f, 900, {"MAGIC_UNSCRIPTED": ";".join(cards)})
                done(step, ok, s or "no result", secs, notes)
        elif step == "golden-in":
            for f in GOLDEN_FILTERS:
                ok, s, notes, secs = dotnet_test(f, 900, {"MAGIC_GOLDEN_UPDATE": "1"})
                if not ok:
                    done(step, ok, s or "no result", secs, notes)
            moves = golden_moves()
            text = "; ".join(f"{f}: {len(m)} moved, {len(n)} added" for f, (m, n) in moves.items())
            lines = [f"{f} moved: {', '.join(m[:10])}{' …' if len(m) > 10 else ''}" for f, (m, _) in moves.items() if m]
            done(step, ok, text if ok else s, secs, lines + notes)
        elif step == "revert":
            code, out, secs = run([sys.executable, "-u", "tools/revert_check.py", a.revert], 3600)
            # the DLL still holds the last revert (restored source, stale binary): the probes run --no-build
            run(["dotnet", "build", TESTS, "-v", "q"], 900)
            lines = [l.strip() for l in out.splitlines() if l.strip()]
            at = next((i for i, l in enumerate(lines) if re.match(r"\d+/\d+ caught", l)), max(len(lines) - 8, 0))
            lines = lines[at:]
            done(step, code == 0, lines[0] if lines else "no result", secs, lines[1:])
        elif step == "probe10":
            code, report, failed, secs = probe(cards, 10, 300)
            if code is None:
                slow = []
                for c in cards:
                    c_code, _, _, c_secs = probe([c], 10, 120)
                    slow.append(f"{c}: {'TIMEOUT' if c_code is None else 'ok'} ({c_secs:.0f}s alone)")
                done(step, False, "HUNG at 10 games — each card alone:", secs, slow)
            done(step, code == 0, report[0] if report else "no report", secs, failed + report[1:])
        elif step == "probe150":
            code, report, failed, secs = probe(cards, 150, 1800)
            never = [r for r in report if "NEVER USED" in r]
            done(step, code == 0, (report[0] if report else "no report") + (f", {len(never)} never used" if never else ""),
                 secs, failed + report[1:] if code == 0 else failed + report)
        elif step == "bench":
            code, out, secs = run(["bash", "tools/bench_vs_head.sh"], 1800)
            best = next((l for l in out.splitlines() if l.startswith("best:")), "no result")
            done(step, code == 0, best, secs)
        elif step == "suite":
            ok, s, notes, secs = dotnet_test(None, 1800, build=True)
            done(step, ok, s or "no result", secs, notes)
        elif step == "docs":
            code, out, secs = run([sys.executable, "tools/magic_docs.py", "--check"], 300)
            done(step, code == 0, "current" if code == 0 else "STALE — run python tools/magic_docs.py", secs,
                 [l for l in out.splitlines() if l.strip()][:5])

    print("\n" + "\n".join(summary) + f"\n\nall {len(steps)} steps passed ({(time.time() - t_all) / 60:.1f} min)")


if __name__ == "__main__":
    main()
