"""Applies replace-exactly-once edits from a spec file — all of them, or none.

    python tools/edit.py SPEC            # apply
    python tools/edit.py SPEC --dry-run  # only check every edit would apply

SPEC format (the text between markers is taken literally — no quoting, no escaping):

    @@ MagicCore/Effects/Effects.cs
    <<<< OLD
    old text, exactly once in the file
    ==== NEW
    new text
    >>>> END

Several blocks may follow one `@@ path`; a new `@@ ` line switches file. An empty OLD appends NEW at
the end of the file. Blank lines and `#` comments between blocks are ignored; anything else there is
an error — a mistyped marker must never drop a block silently.

Why it exists: hand-written Python edit scripts failed ~1 in 6 times in the FRA batches — CRLF vs LF,
tabs vs spaces, an anchor CSharpier had reformatted — and a failure halfway left some files edited
and others not. Here:
- every edit is checked before any file is written, and files are swapped in from temp copies, so
  either all change or none does;
- each edit is spliced into the file's raw text, so lines it doesn't touch keep their own line
  endings (Commands.md and DesignNotes.md MIX CRLF and LF; a whole-file rewrite makes the whole
  file a diff), and new text takes the endings of the text it replaces;
- an anchor that differs only in indentation or trailing spaces still matches (and says so);
- a miss prints the nearest lines, so the retry needs no re-read.
"""

import difflib
import os
import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
OLD, NEW, END = "<<<< OLD", "==== NEW", ">>>> END"


def parse(spec_text):
    """[(path, old, new)] in order. The path line is kept whole (revert_check splits off '| filter')."""
    edits, path, mode, old, new = [], None, None, [], []
    for n, line in enumerate(spec_text.replace("\r\n", "\n").split("\n"), 1):
        marker = line.rstrip()
        if mode is None:
            if marker.startswith("@@ "):
                path = marker[3:].strip()
            elif marker == OLD:
                if path is None:
                    raise SystemExit(f"spec line {n}: a block before any '@@ path'")
                mode, old, new = "old", [], []
            elif marker == "" or marker.startswith("#"):
                pass
            else:
                raise SystemExit(f"spec line {n}: expected '@@ path' or '{OLD}', got: {line!r}")
        elif mode == "old" and marker == NEW:
            mode = "new"
        elif mode == "new" and marker == END:
            edits.append((path, "\n".join(old), "\n".join(new)))
            mode = None
        elif mode == "old":
            old.append(line)
        else:
            new.append(line)
    if mode is not None:
        raise SystemExit(f"spec: unterminated block (expected '{NEW}' then '{END}')")
    if not edits:
        raise SystemExit("spec: no edits")
    return edits


def _norm(line):
    return re.sub(r"\s+", " ", line.strip())


def find(text, old):
    """(start, end, how) of the one match of old in text (both LF), or raise with a helpful message."""
    n = text.count(old)
    if n == 1:
        i = text.index(old)
        return i, i + len(old), "exact"
    if n > 1:
        lines = [text[: m.start()].count("\n") + 1 for m in re.finditer(re.escape(old), text)]
        raise LookupError(f"matches {n} times (lines {lines}) — add context to make it unique")
    # whitespace-tolerant: the same lines once leading/trailing whitespace is normalized
    t_lines = text.split("\n")
    o_lines = old.split("\n")
    target = [_norm(l) for l in o_lines]
    hits = [i for i in range(len(t_lines) - len(o_lines) + 1)
            if [_norm(l) for l in t_lines[i:i + len(o_lines)]] == target]
    if len(hits) == 1:
        i = hits[0]
        start = sum(len(l) + 1 for l in t_lines[:i])
        end = start + len("\n".join(t_lines[i:i + len(o_lines)]))
        return start, end, f"whitespace-tolerant (line {i + 1})"
    if len(hits) > 1:
        raise LookupError(f"whitespace-tolerant match {len(hits)} times (lines {[h + 1 for h in hits]})")
    best, best_i = 0.0, 0
    for i in range(max(1, len(t_lines) - len(o_lines) + 1)):
        r = difflib.SequenceMatcher(None, "\n".join(target), "\n".join(_norm(l) for l in t_lines[i:i + len(o_lines)])).ratio()
        if r > best:
            best, best_i = r, i
    near = "\n".join(f"  {best_i + k + 1:5}| {l}" for k, l in enumerate(t_lines[best_i:best_i + len(o_lines) + 1]))
    raise LookupError(f"not found. Nearest ({best:.0%} alike), lines {best_i + 1}+:\n{near}")


def _raw_index(raw):
    """pos[i] = the raw index of LF-text character i (CR before LF dropped), with pos[len] = len(raw)."""
    pos, i = [], 0
    while i < len(raw):
        if raw[i] == "\r" and i + 1 < len(raw) and raw[i + 1] == "\n":
            i += 1
            continue
        pos.append(i)
        i += 1
    pos.append(len(raw))
    return pos


def splice(raw, old, new):
    """raw with old replaced by new — touching only that span; new takes the span's line endings."""
    text = raw.replace("\r\n", "\n")
    if old == "":
        ending = "\r\n" if raw.count("\r\n") * 2 > raw.count("\n") else "\n"
        sep = "" if raw.endswith("\n") or raw == "" else ending
        return raw + sep + new.replace("\n", ending) + ending, "append"
    s, e, how = find(text, old)
    pos = _raw_index(raw)
    a, b = pos[s], pos[e]
    region = raw[a:b]
    if "\n" in region:
        ending = "\r\n" if "\r\n" in region else "\n"
    else:  # one line: the ending of the line it sits on
        line_end = raw.find("\n", b)
        ending = "\r\n" if line_end > 0 and raw[line_end - 1] == "\r" else "\n"
    return raw[:a] + new.replace("\n", ending) + raw[b:], how


def apply(edits, dry_run=False, root=ROOT):
    """Check every edit, then write every file — temp copies first, then swapped in. Returns {path: original raw}."""
    originals, current, errors = {}, {}, []
    for k, (path, old, new) in enumerate(edits, 1):
        p = (root / path) if not Path(path).is_absolute() else Path(path)
        if p not in current:
            if not p.exists():
                errors.append(f"[{k}] {path}: no such file")
                continue
            originals[p] = current[p] = p.read_bytes().decode("utf-8")
        try:
            current[p], how = splice(current[p], old, new)
            print(f"[{k}] {path}: {how}")
        except LookupError as ex:
            errors.append(f"[{k}] {path}: {ex}")
    if errors:
        print("\nNOT APPLIED — nothing was written:\n" + "\n".join(errors))
        sys.exit(1)
    if dry_run:
        return originals
    temps = {}
    try:
        for p, text in current.items():
            t = p.with_name(p.name + ".edit-tmp")
            t.write_bytes(text.encode("utf-8"))
            temps[p] = t
    except OSError as ex:
        for t in temps.values():
            t.unlink(missing_ok=True)
        print(f"\nNOT APPLIED — could not write a temp copy ({ex}); nothing was changed")
        sys.exit(1)
    done = []
    try:
        for p, t in temps.items():
            os.replace(t, p)
            done.append(p)
    except OSError as ex:
        for p in done:  # roll back what was already swapped in
            p.write_bytes(originals[p].encode("utf-8"))
        for t in temps.values():
            Path(t).unlink(missing_ok=True)
        print(f"\nNOT APPLIED — {ex}; the files already changed were put back")
        sys.exit(1)
    return originals


if __name__ == "__main__":
    sys.stdout.reconfigure(encoding="utf-8")
    if len(sys.argv) < 2:
        raise SystemExit(__doc__)
    spec = Path(sys.argv[1]).read_text(encoding="utf-8")
    apply(parse(spec), dry_run="--dry-run" in sys.argv)
    print("dry run: every edit would apply" if "--dry-run" in sys.argv else "applied")
