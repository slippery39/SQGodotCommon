"""The tests a working-tree change needs, from the paths it changed (story DEV-8.2).

    python tools/test_changed.py            # which tests, and the commands - runs nothing
    python -u tools/test_changed.py --run   # build what they need, run them one after another with progress
    python -u tools/test_changed.py --run --commit   # what /pre-commit runs: an AI change gets the whole AI slice
    python tools/test_changed.py --paths MagicCore/Ai/Hidden.cs   # what a change to these would run
    python -u tools/test_changed.py --full   # a story's end or after a merge, in the background (~6 min):   
                                            # the whole solution, then CardReplayTests; each project's result as it lands
    python tools/test_changed.py --selftest

The changed paths are `git diff HEAD` plus untracked files. Each path picks slices from RULES (first match),
and the run is their union. Measured on a quiet machine (docs/findings/test-suite.md, DEV-8.1): the
engine-and-golden slice ~35 s, MagicTable ~55 s, the AI slice ~3 min, MtgSimulator 95 s (DEV-8.4's numbers).

While working, an AI change runs only the test classes named after the files it changed (Hidden.cs ->
HiddenTests) and the AI's golden replay; at a commit (--commit), the whole AI and sealed slice. An engine change
runs the AI's golden replay too, and builds MagicConsole. Everything else - the old engine's projects,
CardReplayTests, the rest of the AI slice after an engine change - runs in the full suite at a story's end. A card
batch has its own checks: tools/batch_checks.py.
"""

import argparse
import re
import subprocess
import sys
import time
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
sys.path.insert(0, str(ROOT / "tools"))

# name: (test project, filter or None for the whole project, what it covers)
SLICES = {
    "engine": ("MagicCore.Tests", "FullyQualifiedName!~MagicCore.Tests.Ai&FullyQualifiedName!~MagicCore.Tests.Limited",
               "MagicCore engine, cards and the golden replays (~35 s)"),
    "ai": ("MagicCore.Tests", "FullyQualifiedName~MagicCore.Tests.Ai|FullyQualifiedName~MagicCore.Tests.Limited",
           "MagicCore AI and sealed (~3 min)"),
    # one flat namespace in SQGodotCommon.Tests: the folder's classes, by name (each file is its class)
    "table": ("SQGodotCommon.Tests", "|".join(f"FullyQualifiedName~.{f.stem}."
                                              for f in sorted((ROOT / "SQGodotCommon.Tests/MagicTableTests").glob("*.cs"))),
              "the MagicTable tests (~55 s)"),
    "magic": ("MagicCore.Tests", None, "MagicCore.Tests, all of it - one run beats two slices (~3 min)"),
    "godot": ("SQGodotCommon.Tests", None, "SQGodotCommon.Tests (~54 s)"),
    "mtgcore": ("MtgCore.Tests", None, "MtgCore.Tests (~4 s)"),
    "mtgsim": ("MtgSimulator.Tests", None, "MtgSimulator.Tests (~95 s)"),
    "igo": ("ImmutableGameObjects/ImmutableGameObjects.Tests", None, "ImmutableGameObjects.Tests (~2 s)"),
}
ORDER = ["igo", "mtgcore", "engine", "ai", "magic", "table", "godot", "mtgsim"]  # cheap first: a failure shows early
EVERYTHING = ["igo", "mtgcore", "magic", "godot", "mtgsim"]  # godot has the table in it
AI_DIRS = ("MagicCore/Ai/", "MagicCore/Limited/", "MagicCore.Tests/Ai/", "MagicCore.Tests/Limited/")

# (path regex, slices) - the first match wins. "named" = the AI test classes named after the file (+ the AI's
# golden replay) while working, the "ai" slice and the table at a commit; "class:X" = XTests always; "build:X" builds
# project X. "selftest" = the tool's own --selftest.
RULES = [
    (r"(.*\.md|docs/.*|\.claude/.*|\.gitignore|\.gitattributes)$", []),
    (r"tools/.*\.py$", ["selftest"]),
    (r"tools/", []),
    (r"(MagicConsole|MtgConsole|MtgSimulator\.Console)/", ["build"]),
    (r"SQGodotCommon/(MagicTable|Project)/|SQGodotCommon\.Tests/MagicTableTests/", ["table"]),
    (r"SQGodotCommon(\.Tests)?/", ["godot"]),
    # the kept records, and the classes that replay them
    (r"MagicCore\.Tests/(Records/|Ai/KeptRecords\.cs)", ["named:AiPosition", "named:GameRecord", "named:BlunderLint",
                                                         "named:Hidden"]),
    # helpers and golden files the AI tests share with the engine's
    (r"MagicCore\.Tests/((TestGames|GoldenReplay|Parallel)\.cs|GoldenAiReplay\.txt)$", ["engine", "ai"]),
    (r"(MagicCore|MagicCore\.Tests)/(Ai|Limited)/", ["named"]),
    (r"MagicCore\.Tests/", ["engine"]),
    (r"MagicCore/", ["engine", "class:GreedyAiReplay", "table", "build:MagicConsole"]),
    (r"MtgCore(\.Tests)?/", ["mtgcore", "mtgsim", "godot"]),
    (r"MtgSimulator(\.Tests)?/", ["mtgsim", "godot"]),
    (r"ImmutableGameObjects/", EVERYTHING),
]


def changed_paths():
    git = lambda *a: subprocess.run(["git", *a], cwd=ROOT, capture_output=True, text=True, encoding="utf-8").stdout
    paths = git("diff", "--name-only", "HEAD").splitlines() + git("ls-files", "--others", "--exclude-standard").splitlines()
    return sorted(set(p for p in paths if p.strip()))


def ai_classes(path, tests_root):
    """The AI/sealed test classes named after a file: MagicCore/Ai/Hidden.cs -> HiddenTests, a test file -> its own."""
    stem = Path(path).name.split(".")[0]
    if stem.endswith("Tests"):
        return [stem]
    found = []
    for folder in ("Ai", "Limited"):
        found += sorted(f.stem for f in (tests_root / folder).glob(f"{stem}*Tests.cs"))
    return found


def plan(paths, commit=False, tests_root=ROOT / "MagicCore.Tests"):
    """(slice names in run order, {class names}, [tools to selftest], [projects to build only], [unmapped paths])."""
    slices, classes, tools, builds, unmapped = set(), set(), [], [], []
    for p in paths:
        p = p.replace("\\", "/")  # a path typed or tab-completed on Windows
        rule = next((s for rx, s in RULES if re.match(rx, p)), None)
        if rule is None:
            unmapped.append(p)
            slices.update(EVERYTHING)
            continue
        for s in rule:
            if s == "selftest":
                if Path(ROOT / p).is_file() and "--selftest" in (ROOT / p).read_text(encoding="utf-8", errors="replace"):
                    tools.append(p)
            elif s.startswith("build"):
                builds.append(s[6:] if ":" in s else p.split("/")[0])
            elif s.startswith("class:"):
                classes.add(s[6:] + "Tests")
            elif s.startswith("named"):
                if commit:
                    slices.update(["ai", "table"])
                else:
                    classes.update([s[6:] + "Tests"] if ":" in s else ai_classes(p, tests_root))
                    classes.add("GreedyAiReplayTests")
            else:
                slices.add(s)
    if "ai" in slices:
        classes.clear()  # the whole slice covers them
    if {"engine", "ai"} <= slices:
        slices -= {"engine", "ai"}
        slices.add("magic")
    if "magic" in slices:
        classes.clear()
    if "godot" in slices:
        slices.discard("table")
    return [s for s in ORDER if s in slices], sorted(classes), sorted(set(tools)), sorted(set(builds)), unmapped


def class_filter(classes):
    return "|".join(f"FullyQualifiedName~.{c}." for c in classes)


def commands(slices, classes, tools, builds):
    """[(label, project or None, filter, argv)] in run order."""
    out = [(f"selftest {t}", None, None, [sys.executable, t, "--selftest"]) for t in tools]
    out += [(f"build {b}", None, None, ["dotnet", "build", b, "-v", "q"]) for b in builds]
    if classes and "engine" not in slices:
        out.append((f"AI classes: {', '.join(classes)}", "MagicCore.Tests", class_filter(classes), None))
    for s in slices:
        project, filt, label = SLICES[s]
        if s == "engine" and classes:  # one run, not two: the classes overlap the engine's
            filt, label = f"({filt})|{class_filter(classes)}", f"{label} + {', '.join(classes)}"
        out.append((label, project, filt, None))
    return out


def show(cmd):
    label, project, filt, argv = cmd
    if argv:
        return " ".join(["python" if x == sys.executable else x for x in argv])
    return f'dotnet test {project}' + (f' --filter "{filt}"' if filt else "")


def main(argv):
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--run", action="store_true", help="build and run them, with progress")
    ap.add_argument("--commit", action="store_true", help="what a commit runs: an AI change gets the whole AI slice")
    ap.add_argument("--paths", nargs="+", help="plan for these paths instead of the working tree's changes")
    ap.add_argument("--full", action="store_true", help="the whole solution, then CardReplayTests (a story's end)")
    ap.add_argument("--selftest", action="store_true")
    a = ap.parse_args(argv)
    if a.selftest:
        return selftest()
    if a.full:
        return full()
    paths = a.paths or changed_paths()
    slices, classes, tools, builds, unmapped = plan(paths, a.commit)
    cmds = commands(slices, classes, tools, builds)
    print(f"{len(paths)} changed path(s){' (commit)' if a.commit else ''}")
    for p in unmapped:
        print(f"  unmapped, so everything runs: {p}")
    if not cmds:
        print("no tests needed")
        return 0
    for c in cmds:
        print(f"  {c[0]}\n    {show(c)}")
    if not a.run:
        return 0

    from batch_checks import dotnet_test, run

    t_all, results = time.time(), []
    projects = list(dict.fromkeys(c[1] for c in cmds if c[1]))
    steps = [("build " + ", ".join(projects), None, None, None)] if projects else []
    steps += cmds
    for k, c in enumerate(steps, 1):
        label, project, filt, argv = c
        print(f"[{k}/{len(steps)}] {label} ... ({(time.time() - t_all) / 60:.1f} min in)", flush=True)
        if label.startswith("build ") and argv is None:
            ok, notes = True, []
            for p in projects:
                code, out, secs = run(["dotnet", "build", p, "-v", "q"], 900)
                if code != 0:
                    ok = False
                    notes += sorted({l.strip() for l in out.splitlines() if " error " in l})[:12] or out.splitlines()[-8:]
            text = "built" if ok else "BUILD FAILED"
        elif argv:
            code, out, secs = run(argv, 900)
            ok, text, notes = code == 0, (out.strip().splitlines() or ["(no output)"])[-1], []
            if not ok:
                notes = out.strip().splitlines()[-8:]
        else:
            ok, text, notes, secs = dotnet_test(filt, 900, project=project)
        results.append(f"{'ok  ' if ok else 'FAIL'} {label}: {text}")
        print(f"   {'ok' if ok else 'FAILED'}: {text} ({(time.time() - t_all) / 60:.1f} min in)", flush=True)
        for n in notes[:12]:
            print(f"     {n}", flush=True)
        if not ok and label.startswith("build ") and argv is None:  # the test projects did not build
            break
    print("\n" + "\n".join(results) + f"\n\n{(time.time() - t_all) / 60:.1f} min")
    return 0 if all(r.startswith("ok") for r in results) and len(results) == len(steps) else 1


TEST_PROJECTS = len({p for p, _, _ in SLICES.values()})


def full():
    """The whole solution's tests (projects together: 5 min quiet against 6.7 one after another), streamed so each
    project's result and each failure prints as it lands, then the explicit CardReplayTests. Exit 1 on any failure."""
    from batch_checks import dotnet_test, run

    t0, results, failures = time.time(), [], []
    mins = lambda: f"{(time.time() - t0) / 60:.1f} min"
    print("[full] building the solution ...", flush=True)
    code, out, _ = run(["dotnet", "build", "SQGodotCommon.sln", "-v", "q"], 1800)
    if code != 0:
        errors = sorted({l.strip() for l in out.splitlines() if " error " in l})[:12]
        print("[full] BUILD FAILED" + "".join(f"\n  {e}" for e in errors))
        print("\nFULL: FAILED (the build)")
        return 1
    print(f"[full] built ({mins()}); testing {TEST_PROJECTS} projects together ...", flush=True)
    proc = subprocess.Popen(
        ["dotnet", "test", "SQGodotCommon.sln", "--no-build", "--blame-hang", "--blame-hang-timeout", "10m",
         "--blame-hang-dump-type", "none"],
        cwd=ROOT, stdout=subprocess.PIPE, stderr=subprocess.STDOUT, text=True, encoding="utf-8", errors="replace")
    for line in proc.stdout:
        line = line.rstrip()
        if re.match(r"\s*(Passed|Failed)!", line):
            results.append(line.strip())
            print(f"[full] {len(results)}/{TEST_PROJECTS} projects, {mins()}: {line.strip()}", flush=True)
        elif re.match(r"\s+Failed \S", line) or "test running when the crash occurred" in line or "error CS" in line:
            failures.append(line.strip())
            print(f"[full]   {line.strip()}", flush=True)
    proc.wait()
    print(f"[full] CardReplayTests (every card's golden replay) ... ({mins()})", flush=True)
    ok, summary, notes, _ = dotnet_test("FullyQualifiedName~CardReplayTests", 1200)
    results.append(summary or "CardReplayTests: no result")
    failures += [] if ok else notes
    print(f"[full] CardReplayTests, {mins()}: {summary}", flush=True)
    passed = proc.returncode == 0 and ok and len(results) >= TEST_PROJECTS + 1 and not failures
    print("\n" + "\n".join(results) + "".join(f"\n  {f}" for f in failures[:20]))
    print(f"\nFULL: {'all passed' if passed else 'FAILED'} ({mins()})")
    return 0 if passed else 1


def selftest():
    import tempfile

    with tempfile.TemporaryDirectory() as d:
        t = Path(d)
        for f in ("Ai/HiddenTests.cs", "Ai/GreedyAiTests.cs", "Ai/GreedyAiReplayTests.cs", "Ai/EvaluatorExplainTests.cs",
                  "Limited/SealedBuilderTests.cs", "Limited/SealedBuildMatchTests.cs"):
            (t / f).parent.mkdir(exist_ok=True)
            (t / f).write_text("")

        def check(paths, want_slices, want_classes=(), commit=False):
            slices, classes, _, _, _ = plan(paths, commit, t)
            assert slices == want_slices and classes == sorted(want_classes), f"{paths}: {slices} {classes}"

        check(["docs/stories/DEV-8.md", "MagicCore/CLAUDE.md", ".claude/rules/x.md"], [])
        check(["SQGodotCommon/MagicTable/Board.cs"], ["table"])
        check(["SQGodotCommon/MtgGame/Board.cs"], ["godot"])
        check(["MagicCore/Effects/Damage.cs"], ["engine", "table"], ["GreedyAiReplayTests"])
        check(["MagicCore/Sets/Fra/FraCards.cs"], ["engine", "table"], ["GreedyAiReplayTests"])
        check(["MagicCore\Ai\Hidden.cs"], [], ["HiddenTests", "GreedyAiReplayTests"])
        check(["MagicCore.Tests/Sets/FraTests.cs"], ["engine"])
        check(["MagicCore/Ai/Hidden.cs"], [], ["HiddenTests", "GreedyAiReplayTests"])
        check(["MagicCore/Ai/GreedyAi.cs"], [], ["GreedyAiTests", "GreedyAiReplayTests"])
        check(["MagicCore/Ai/Evaluator.Lifetime.cs"], [], ["EvaluatorExplainTests", "GreedyAiReplayTests"])
        check(["MagicCore/Ai/Lookahead.cs"], [], ["GreedyAiReplayTests"])  # no class named after it
        check(["MagicCore/Limited/SealedBuilder.cs"], [], ["SealedBuilderTests", "GreedyAiReplayTests"])
        records = ["AiPositionTests", "BlunderLintTests", "GameRecordTests", "GreedyAiReplayTests", "HiddenTests"]
        check(["MagicCore.Tests/Records/x.json"], [], records)
        check(["MagicCore.Tests/Ai/KeptRecords.cs"], [], records)
        check(["MagicCore.Tests/TestGames.cs"], ["magic"])
        check(["MagicCore.Tests/GoldenAiReplay.txt"], ["magic"])
        check(["MagicCore/Ai/Hidden.cs"], ["ai", "table"], commit=True)
        check(["MagicCore/Ai/Hidden.cs", "MagicCore/Rules/Stack.cs"], ["magic", "table"], commit=True)
        check(["MtgCore/Cards/X.cs"], ["mtgcore", "godot", "mtgsim"])
        check(["MagicCore/Effects/Damage.cs", "SQGodotCommon/MtgGame/Board.cs"], ["engine", "godot"], ["GreedyAiReplayTests"])
        assert plan(["MagicCore/Effects/Damage.cs"])[3] == ["MagicConsole"]
        check(["ImmutableGameObjects/ImmutableGameObjects/GameState.cs"], EVERYTHING)
        check(["SQGodotCommon.sln"], EVERYTHING)  # unmapped: everything, and said so
        assert plan(["MagicConsole/Program.cs", "MtgConsole/Loop.cs"])[3] == ["MagicConsole", "MtgConsole"]
        assert "FullyQualifiedName~.MagicTableManagerTests." in SLICES["table"][1]
        assert plan(["tools/test_changed.py", "tools/bench_vs_head.sh"])[2] == ["tools/test_changed.py"]
        assert class_filter(["HiddenTests"]) == "FullyQualifiedName~.HiddenTests."
        assert TEST_PROJECTS == 5
    print("selftest ok")
    return 0


if __name__ == "__main__":
    sys.stdout.reconfigure(encoding="utf-8")
    sys.exit(main(sys.argv[1:]))
