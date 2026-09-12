#!/usr/bin/env python
"""Guard the instruction files against the ways they fail SILENTLY.

Wired as a PostToolUse hook on Write|Edit in .claude/settings.json.

Three checks, each for a failure that produces NO error and no visible symptom —
the file looks perfect, every test passes, and the rule simply never loads:

  1. Line budget. A bloated instruction file gets ignored rather than obeyed.
     Over budget means content is in the WRONG FILE, not that the budget is
     wrong: move the detail down and leave the rule plus a pointer behind.

  2. CRLF in the YAML frontmatter. MEASURED on Claude Code 2.1.116: a rule
     file whose frontmatter had CRLF line endings did not load at all. The
     `paths:` block never parsed and the rule was inert.

  3. `**` in a paths glob. MEASURED on the same version: `MtgCore/Sets/**/*.cs`
     matched nothing, while `MtgCore/Sets/*/*.cs` matched. Write the levels out
     explicitly. A glob that matches zero files is also reported.

Reads the hook payload on stdin, prints a JSON systemMessage, always exits 0 —
a documentation nag must never block a write.
"""
import glob
import json
import os
import re
import sys

CLAUDE_MD_BUDGET = 200
RULES_BUDGET = 1500  # rule files are the destination for detail; a runaway backstop

MOVE_TO = {
    "claude": ".claude/rules/ (subsystem rules) or docs/findings/ (measured results)",
    "rules": "a second, more specific rule file - or docs/findings/ if it is run output",
}


def hook_path(payload):
    for getter in (
        lambda: payload["tool_response"]["filePath"],
        lambda: payload["tool_input"]["file_path"],
    ):
        try:
            value = getter() or ""
            if value:
                return value
        except (KeyError, TypeError):
            continue
    return ""


def check_rule_frontmatter(path, norm, repo_root):
    """Return warnings for a .claude/rules/*.md file that would load incorrectly."""
    problems = []
    try:
        raw = open(path, "rb").read()
    except OSError:
        return problems

    head = raw[:4096]
    if b"\r\n" in head:
        problems.append(
            "its YAML frontmatter uses CRLF line endings, which stops the rule "
            "loading at all (measured on 2.1.116) - convert the file to LF"
        )

    text = raw.decode("utf-8", "replace")
    match = re.match(r"^---\n(.*?)\n---\n", text, re.S)
    if not match:
        problems.append(
            "it has no parseable `---` frontmatter, so it has no `paths:` and will "
            "load in EVERY session instead of only on matching files"
        )
        return problems

    globs = re.findall(r'^\s*-\s*"(.+)"\s*$', match.group(1), re.M)
    if not globs:
        problems.append("its frontmatter declares no `paths:`, so it loads in every session")
        return problems

    starstar = [g for g in globs if "**" in g]
    if starstar:
        problems.append(
            "these globs use `**`, which matches nothing on 2.1.116 - write the "
            "levels out (`a/*/b`, `a/*/*/b`): " + ", ".join(starstar)
        )

    dead = []
    for pattern in globs:
        if "**" in pattern:
            continue  # already reported; would mislead the dead-glob count
        hits = glob.glob(os.path.join(repo_root, pattern))
        if not any(os.path.isfile(h) for h in hits):
            dead.append(pattern)
    if dead:
        problems.append("these globs match no files, so they can never fire: " + ", ".join(dead))

    return problems


def main():
    try:
        payload = json.load(sys.stdin)
    except Exception:
        return

    path = hook_path(payload)
    if not path:
        return
    if not os.path.isfile(path):
        return

    norm = path.replace("\\", "/")
    repo_root = os.environ.get("CLAUDE_PROJECT_DIR") or os.getcwd()

    if norm.endswith("/CLAUDE.md") or norm == "CLAUDE.md":
        budget, kind, is_rule = CLAUDE_MD_BUDGET, "claude", False
    elif "/.claude/rules/" in norm and norm.endswith(".md"):
        budget, kind, is_rule = RULES_BUDGET, "rules", True
    else:
        return

    messages = []

    try:
        with open(path, "rb") as handle:
            lines = sum(1 for _ in handle)
    except OSError:
        lines = 0
    if lines > budget:
        messages.append(
            f"{norm} is {lines} lines, over its {budget}-line budget. "
            f"Move detail to {MOVE_TO[kind]} and leave the rule plus a one-line pointer behind."
        )

    if is_rule:
        for problem in check_rule_frontmatter(path, norm, repo_root):
            messages.append(f"{norm}: {problem}.")

    if messages:
        print(json.dumps({"systemMessage": " ".join(messages)}))


if __name__ == "__main__":
    main()
