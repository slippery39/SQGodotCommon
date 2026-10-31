"""What a Claude Code session cost, and where it went — to check whether a workflow change saves usage.

    python tools/usage_report.py              # the most recent session in this project
    python tools/usage_report.py SESSION_ID   # a given session (its .jsonl name)
    python tools/usage_report.py --all        # every session, one line each
    python tools/usage_report.py --log "MC-5.5 registry, 0 cards"   # this session -> a row in
                                              # docs/findings/card-batch-usage.md (one batch = one session)

Costs are in INPUT-TOKEN EQUIVALENTS, weighting each token kind as the API prices it (input 1, output
5, cache read 0.1, cache write 2): subscription usage tracks the same weights. Subagents (code reviews)
are included. "Batches" are split at each `git commit` the session ran.

Measured 2026-10-10 on the FRA rare batches (session e24a44f6): 81.8M total, 70% of the main session
in cache reads — every call re-read a ~442k-token median context. Starting each batch in a fresh
session was simulated to save 37–40%; compare a later session's per-batch cost to check it.
"""

import json
import re
import statistics
import sys
from collections import Counter
from datetime import date
from pathlib import Path

PROJECT = Path.home() / ".claude" / "projects" / "c--SQGodotHelperApps-SQGodotCommon"
W = dict(input_tokens=1.0, output_tokens=5.0, cache_read_input_tokens=0.1, cache_creation_input_tokens=2.0)


def cost(u):
    return sum(W[k] * u.get(k, 0) for k in W)


def calls(path):
    """[(usage, tool names, commit message or None)] — one per API request, in order."""
    reqs, order = {}, []
    for line in open(path, encoding="utf-8"):
        try:
            o = json.loads(line)
        except Exception:
            continue
        if o.get("type") != "assistant":
            continue
        m = o["message"]
        rid = o.get("requestId") or m.get("id")
        if rid not in reqs:
            order.append(rid)
            reqs[rid] = [{}, [], None]
        reqs[rid][0] = m.get("usage") or reqs[rid][0]
        for c in m.get("content") or []:
            if c.get("type") == "tool_use":
                reqs[rid][1].append(c["name"])
                cmd = (c.get("input") or {}).get("command", "")
                if c["name"] == "Bash" and re.search(r"(^|[;&|]\s*)git commit\b", cmd, re.M):
                    # the title: a heredoc's first line (cat > msg <<'EOF'), else -m "…"
                    msg = re.search(r"<<'?EOF'?\n([^\n]+)", cmd) or re.search(r"-m ['\"]([^'\"\n]{4,80})", cmd)
                    reqs[rid][2] = msg.group(1) if msg else "commit"
    return [reqs[r] for r in order]


def subagent_cost(session_dir):
    total = 0.0
    for f in session_dir.glob("subagents/*.jsonl"):
        total += sum(cost(u) for u, _, _ in calls(f))
    return total


def report(path):
    cs = calls(path)
    if not cs:
        print("no calls")
        return
    main = sum(cost(u) for u, _, _ in cs)
    sub = subagent_cost(path.with_suffix(""))
    kinds = Counter()
    for u, _, _ in cs:
        for k in W:
            kinds[k] += W[k] * u.get(k, 0)
    ctx = [u.get("cache_read_input_tokens", 0) + u.get("cache_creation_input_tokens", 0) for u, _, _ in cs]
    print(f"session {path.stem}: {len(cs)} calls")
    print(f"  total {(main + sub) / 1e6:.1f}M (main {main / 1e6:.1f}M + subagents {sub / 1e6:.1f}M) input-token equivalents")
    print("  main by kind: " + ", ".join(f"{k.replace('_input_tokens', '').replace('_tokens', '')} {100 * v / main:.0f}%" for k, v in kinds.most_common()))
    print(f"  context per call: median {statistics.median(ctx) / 1e3:.0f}k, max {max(ctx) / 1e3:.0f}k")
    tools = Counter()
    tool_cost = Counter()
    for u, names, _ in cs:
        key = names[0] if names else "(text)"
        tools[key] += 1
        tool_cost[key] += cost(u)
    print("  by tool: " + ", ".join(f"{k} {tools[k]}× {100 * v / main:.0f}%" for k, v in tool_cost.most_common(8)))
    print("  batches (split at commits):")
    acc, n = 0.0, 0
    for u, _, commit in cs:
        acc += cost(u)
        n += 1
        if commit:
            print(f"    {acc / 1e6:6.2f}M {n:4} calls  {commit[:70]}")
            acc, n = 0.0, 0
    if n:
        print(f"    {acc / 1e6:6.2f}M {n:4} calls  (after the last commit)")


def main():
    sys.stdout.reconfigure(encoding="utf-8")
    files = sorted(PROJECT.glob("*.jsonl"), key=lambda p: p.stat().st_mtime)
    if "--log" in sys.argv:
        label = sys.argv[sys.argv.index("--log") + 1]
        cs = calls(files[-1])
        total = sum(cost(u) for u, _, _ in cs) + subagent_cost(files[-1].with_suffix(""))
        ctx = statistics.median(u.get("cache_read_input_tokens", 0) + u.get("cache_creation_input_tokens", 0) for u, _, _ in cs)
        log = Path(__file__).resolve().parent.parent / "docs" / "findings" / "card-batch-usage.md"
        row = f"| {date.today()} | {label} | {files[-1].stem[:8]} | {len(cs)} | {ctx / 1e3:.0f}k | {total / 1e6:.1f}M |\n"
        with open(log, "a", encoding="utf-8", newline="\n") as f:
            f.write(row)
        print(row, end="")
        return
    if "--all" in sys.argv:
        for f in files:
            cs = calls(f)
            if cs:
                total = sum(cost(u) for u, _, _ in cs) + subagent_cost(f.with_suffix(""))
                print(f"{f.stem}  {len(cs):5} calls  {total / 1e6:7.1f}M")
        return
    path = PROJECT / f"{sys.argv[1]}.jsonl" if len(sys.argv) > 1 else files[-1]
    report(path)


if __name__ == "__main__":
    main()
