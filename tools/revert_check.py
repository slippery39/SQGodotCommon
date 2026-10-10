"""Revert-checks: break each piece on purpose, confirm its test FAILS, restore. One table out.

    python -u tools/revert_check.py SPEC                       # MagicCore.Tests by default
    python -u tools/revert_check.py SPEC --project MtgCore.Tests --timeout 300

SPEC is tools/edit.py's format, with the test filter on the file line:

    @@ MagicCore/Effects/Effects.cs | GideonTheOathless_WardDiscard
    <<<< OLD
    Otherwise = [new Counter(What)],
    ==== NEW
    Otherwise = [],
    >>>> END

First every distinct filter runs on the UNBROKEN code: a test that already fails would make every
revert look caught, so those checks are skipped and reported. Then each block is applied alone,
`dotnet test --filter FullyQualifiedName~<filter>` runs, and the file is written back from its
original text in a `finally` — a NEW mtime, so the next build recompiles (restoring a .bak with its
old mtime once left the reverted code in the DLL). Each run has a timeout that kills the whole process
tree (a revert once made the mana solver exponential; a surviving testhost would hang the script).

A check passes when its test FAILS. "NOT CAUGHT" means the test did not see the break — it is vacuous,
or the revert hit the wrong line; fix one before trusting it.
"""

import subprocess
import sys
import time
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
from edit import ROOT, parse as parse_edits, splice  # noqa: E402


def parse(spec_text):
    """[(path, filter, old, new)] — edit.py's parser, with '| filter' split off the path."""
    out = []
    for path, old, new in parse_edits(spec_text):
        if "|" not in path:
            raise SystemExit(f"spec: '@@ {path}' needs '| TestFilter'")
        p, flt = (x.strip() for x in path.split("|", 1))
        out.append((p, flt, old, new))
    return out


def run_tests(project, flt, timeout):
    """(verdict, seconds). Kills the whole tree on timeout — taskkill /T on Windows."""
    t0 = time.time()
    proc = subprocess.Popen(
        ["dotnet", "test", str(ROOT / project), "--filter", f"FullyQualifiedName~{flt}"],
        stdout=subprocess.PIPE, stderr=subprocess.STDOUT, text=True, encoding="utf-8", errors="replace",
        creationflags=getattr(subprocess, "CREATE_NEW_PROCESS_GROUP", 0),
    )
    try:
        out, _ = proc.communicate(timeout=timeout)
    except subprocess.TimeoutExpired:
        if sys.platform == "win32":
            subprocess.run(["taskkill", "/T", "/F", "/PID", str(proc.pid)], capture_output=True)
        else:
            proc.kill()
        proc.communicate()
        return "TIMEOUT", time.time() - t0
    if "error CS" in out or "Build FAILED" in out:
        verdict = "BUILD ERROR"
    elif "Failed!" in out:
        verdict = "FAILED"
    elif "Passed!" in out:
        verdict = "PASSED"
    else:
        verdict = "NO TEST MATCHED" if "No test matches" in out else "UNKNOWN"
    return verdict, time.time() - t0


def main():
    sys.stdout.reconfigure(encoding="utf-8")
    args = sys.argv[1:]
    project = args[args.index("--project") + 1] if "--project" in args else "MagicCore.Tests"
    timeout = int(args[args.index("--timeout") + 1]) if "--timeout" in args else 300
    checks = parse(Path(args[0]).read_text(encoding="utf-8"))

    baseline = {}
    for flt in dict.fromkeys(c[1] for c in checks):
        verdict, secs = run_tests(project, flt, timeout)
        baseline[flt] = verdict
        print(f"baseline {flt}: {verdict} ({secs:.0f}s)", flush=True)

    rows = []
    for k, (path, flt, old, new) in enumerate(checks, 1):
        if baseline[flt] != "PASSED":
            rows.append((k, flt, f"SKIPPED — unbroken run was {baseline[flt]}"))
            print(f"[{k}] {flt}: {rows[-1][2]}", flush=True)
            continue
        p = ROOT / path
        raw = p.read_bytes().decode("utf-8")
        try:
            broken, _ = splice(raw, old, new)
        except LookupError as ex:
            rows.append((k, flt, "ANCHOR — " + str(ex).splitlines()[0]))
            print(f"[{k}] {flt}: ANCHOR — {ex}", flush=True)
            continue
        try:
            p.write_bytes(broken.encode("utf-8"))
            verdict, secs = run_tests(project, flt, timeout)
        finally:
            p.write_bytes(raw.encode("utf-8"))  # the original text — and a fresh mtime
        result = {"FAILED": "ok (test failed)", "PASSED": "NOT CAUGHT — test still passes"}.get(verdict, verdict)
        rows.append((k, flt, result))
        print(f"[{k}] {flt}: {result} ({secs:.0f}s)", flush=True)

    bad = [r for r in rows if not r[2].startswith("ok")]
    print(f"\n{len(rows) - len(bad)}/{len(rows)} caught" + (" — not caught, or not run:" if bad else ""))
    for k, flt, result in bad:
        print(f"  [{k}] {flt}: {result}")
    sys.exit(1 if bad else 0)


if __name__ == "__main__":
    main()
