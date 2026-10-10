"""Generates MagicCore's ability catalog and a set's card sheet from the code — never edit those by hand.

    python tools/magic_docs.py            # writes MagicCore/AbilityCatalog.md and MagicCore/FraCardSheet.md
    python tools/magic_docs.py --check    # exit 1 if either is stale (for /pre-commit)

The catalog is for Claude: every effect, trigger, ref, count, condition and component — one line each,
read from its /// <summary>, with the cards that use it. A new session reads this instead of the
engine source. The card sheet is for people: each card's printed text next to its script, the
engine pieces it uses, and the tests that name it — so a mis-scripted card shows on the page.
"""

import json
import re
import sys
from collections import defaultdict
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
CORE = ROOT / "MagicCore"
TESTS = ROOT / "MagicCore.Tests"
SCRIPTS = CORE / "Sets" / "Fra" / "FraCards.cs"
SET_JSON = CORE / "Sets" / "FRA.json"
CATALOG = CORE / "AbilityCatalog.md"
SHEET = CORE / "FraCardSheet.md"

# Which base types make a section, in reading order.
SECTIONS = [
    ("Effect", "Effects — what a card does (`Effects/*.cs`)"),
    ("Condition", "Conditions — \"if …\" (`Effects/Conditions.cs`)"),
    ("GameComponent", "Components — abilities, statics, replacements, markers on a card"),
    ("ChoiceAction", "Choices — what a player is asked"),
    ("GameEvent", "Events — what triggers listen for (`Turns/Triggers.cs`)"),
]
# Every public enum is vocabulary, except these (types and colours a script never spells as an enum value).
ENUM_SKIP = {"CardType", "Supertype", "Step", "SymbolKind", "ManaColor"}
# Bases a record may derive from without being a catalog piece — the engine's own machinery.
NOT_PIECES = {"GameObject", "GameAction", "Effect", "Condition", "GameComponent", "ChoiceAction", "GameEvent"}
sys.path.insert(0, str(Path(__file__).resolve().parent))
import magic_set  # noqa: E402 — THE playable rule and keyword list


def clean_doc(lines):
    """/// lines → one plain sentence-ish string: <summary> text, crefs as names, params dropped."""
    text = " ".join(l.strip()[3:].strip() for l in lines)
    text = re.sub(r"<param name=\"(\w+)\">(.*?)</param>", "", text)
    text = re.sub(r"<see cref=\"([\w.]+)\"\s*/>", lambda m: m.group(1).split(".")[-1], text)
    text = re.sub(r"</?(summary|remarks|c|para)>", " ", text)
    text = re.sub(r"<[^>]+>", "", text)
    return re.sub(r"\s+", " ", text).strip()


def short(doc, limit=170):
    return doc if len(doc) <= limit else doc[: limit - 1].rsplit(" ", 1)[0] + "…"


def doc_before(lines, i):
    """The /// block ending just above line i (attributes allowed between)."""
    j = i - 1
    while j >= 0 and lines[j].strip().startswith("["):
        j -= 1
    block = []
    while j >= 0 and lines[j].strip().startswith("///"):
        block.insert(0, lines[j])
        j -= 1
    return clean_doc(block)


def declaration(lines, i):
    """From `public record Name(` to the first `{` or `;` at paren depth 0 — params and base."""
    text, depth = "", 0
    for line in lines[i:i + 40]:
        for ch in line:
            if ch == "(":
                depth += 1
            elif ch == ")":
                depth -= 1
            elif ch in "{;" and depth == 0:
                return text
            text += ch
        text += " "
    return text


def parse_types():
    """Every record in MagicCore: name, base, params, summary, and its documented init properties."""
    types = {}
    for path in sorted(CORE.rglob("*.cs")):
        if any(p in path.parts for p in ("bin", "obj", "Sets")):
            continue
        lines = path.read_text(encoding="utf-8").splitlines()
        current = None
        for i, line in enumerate(lines):
            m = re.match(r"public (?:sealed |abstract )?record (\w+)", line.strip())
            if m and not line.startswith("\t"):
                decl = re.sub(r"\s+", " ", declaration(lines, i))
                # params: the balanced (...) right after the name; base: the first ": Name" after them
                after_name = decl[decl.find(m.group(1)) + len(m.group(1)):]
                params_text, tail = "", after_name
                if after_name.lstrip().startswith("("):
                    depth, j = 0, after_name.index("(")
                    for j in range(after_name.index("("), len(after_name)):
                        depth += {"(": 1, ")": -1}.get(after_name[j], 0)
                        if depth == 0:
                            break
                    params_text, tail = after_name[after_name.index("(") + 1:j], after_name[j + 1:]
                base = re.search(r":\s*([\w.]+)", tail)
                base = base.group(1).split(".")[-1] if base else ""
                params = re.match(r"(.*)", params_text) if params_text else None
                current = types[m.group(1)] = dict(
                    name=m.group(1), base=base, params=re.sub(r"\s+", " ", params.group(1)).strip() if params else "",
                    doc=doc_before(lines, i), props=[], file=path.relative_to(ROOT).as_posix())
                continue
            p = re.match(r"\tpublic ([\w<>?.,\[\] ]+?) (\w+) \{ get; init; \}", line)
            if current and p:
                d = doc_before(lines, i)
                if d:
                    current["props"].append((p.group(2), p.group(1), d))
            if line.startswith("}"):
                current = None
    # an indirect base (PayToKeepChoice : YesNoChoice) belongs to its root's section
    sections = {b for b, _ in SECTIONS}

    def root(t, seen=()):
        b = types[t]["base"]
        return b if b in sections or b not in types or b in seen else root(b, seen + (t,))
    for t in types.values():
        t["root"] = root(t["name"])
    return types


def parse_enums():
    enums = {}
    for path in sorted(CORE.rglob("*.cs")):
        if any(p in path.parts for p in ("bin", "obj", "Sets")):
            continue
        text = path.read_text(encoding="utf-8")
        for m in re.finditer(r"public enum (\w+)\s*\{(.*?)\n\}", text, re.S):
            if m.group(1) in ENUM_SKIP:
                continue
            members = []
            for line in m.group(2).splitlines():
                mm = re.match(r"\s*(\w+)(?:\s*=\s*[^,]+)?,?\s*(?://\s*(.*))?$", line)
                if mm and mm.group(1):
                    members.append((mm.group(1), (mm.group(2) or "").strip()))
            enums[m.group(1)] = members
    return enums


def parse_static_members(path, type_name):
    """`public static readonly Ref X` / `public static Count X(` — the named shortcuts."""
    out = []
    lines = path.read_text(encoding="utf-8").splitlines()
    for i, line in enumerate(lines):
        m = re.match(rf"\tpublic static (?:readonly )?{type_name} (\w+)", line)
        if m:
            out.append((m.group(1), short(doc_before(lines, i), 110)))
    return out


def parse_scripts():
    """FraCards: each card's script block, and its helper functions with their summaries."""
    text = SCRIPTS.read_text(encoding="utf-8").replace("\r\n", "\n")
    start = text.index("Scripts =")
    blocks = {}
    keys = list(re.finditer(r'^\t\t\t\["(.+?)"\] =', text[start:], re.M))
    end_all = text.index("\n\t\t};", start)
    for k, m in enumerate(keys):
        a = start + m.start()
        b = start + keys[k + 1].start() if k + 1 < len(keys) else end_all
        body = text[a:b]
        body = re.sub(r"\n\t\t\t// =====.*", "", body).rstrip()  # section banners belong to the next card
        blocks[m.group(1)] = body
    lines = text[:start].splitlines()
    helpers = []
    for i, line in enumerate(lines):
        m = re.match(r"\t(?:public )?static (?:readonly )?([\w<>\[\]]+) (\w+)\b", line)
        if m and m.group(2) not in ("Data", "Scripts", "EngineKeywords"):
            d = doc_before(lines, i)
            if d:
                helpers.append((m.group(2), short(d, 140)))
    return blocks, helpers


def card_data():
    d = json.loads(SET_JSON.read_text(encoding="utf-8"))
    return d if isinstance(d, list) else d.get("cards") or d.get("data")



def used_by(types, blocks, helpers):
    """Engine piece -> cards whose script names it."""
    names = set(types) | {h for h, _ in helpers}
    users = defaultdict(list)
    for card, body in blocks.items():
        for n in names:
            if re.search(rf"\b{re.escape(n)}\b", body):
                users[n].append(card)
    return users


def test_mentions():
    """Card name -> test files that name it (a card's test calls it by name)."""
    out = defaultdict(set)
    texts = {p.name: p.read_text(encoding="utf-8") for p in TESTS.rglob("*.cs") if "bin" not in p.parts and "obj" not in p.parts}
    for c in card_data():
        for fname, t in texts.items():
            if f'"{c["name"]}"' in t:
                out[c["name"]].add(fname.removesuffix(".cs"))
    return out


def catalog(types, enums, blocks, helpers, users):
    out = ["# MagicCore ability catalog", "",
           "**Generated by `python tools/magic_docs.py` from the code's `///` summaries — do not edit.**",
           "Read this instead of the engine source to know what exists. Each line: the piece, what it is,",
           "and (in brackets) how many FRA cards use it, with a few. Find one: `grep -rn \"record Name\" MagicCore`.", ""]
    ss = lambda n: f" [{len(users[n])}: {', '.join(users[n][:3])}{'…' if len(users[n]) > 3 else ''}]" if users.get(n) else ""
    for base, title in SECTIONS:
        group = sorted((t for t in types.values() if t["root"] == base), key=lambda t: t["name"])
        if not group:
            continue
        out += [f"## {title}", ""]
        for t in group:
            sig = f"({t['params']})" if t["params"] else ""
            out.append(f"- **`{t['name']}{sig}`** — {short(t['doc'], 140) or '(no summary)'}{ss(t['name'])}")
            for name, typ, d in t["props"]:
                out.append(f"  - `{name}` ({typ}): {short(d, 110)}")
        out.append("")
    out += ["## Building blocks — targets, refs, counts", ""]
    for n in ("TargetSpec", "Ref", "Count"):
        if n in types:
            tt = types[n]
            out.append(f"- **`{n}({tt['params']})`** — {short(tt['doc'], 140)}")
            for name, typ, d in tt["props"]:
                out.append(f"  - `{name}` ({typ}): {short(d, 110)}")
    out.append("")
    out += ["## Vocabulary — enums", ""]
    for e in sorted(enums, key=lambda e: (e not in ("TriggerEvent", "RefKind", "CountOf"), e)):
        items = "; ".join(f"`{n}`" + (f" ({c})" if c else "") for n, c in enums[e])
        out += [f"- **{e}**: {items}", ""]
    out += ["## Named shortcuts", ""]
    for type_name, path in (("Ref", CORE / "Effects" / "Refs.cs"), ("Count", CORE / "Effects" / "Refs.cs")):
        items = parse_static_members(path, type_name)
        out.append(f"- **{type_name}**: " + "; ".join(f"`{type_name}.{n}`" for n, _ in items))
    out += ["", "## FraCards script helpers (`MagicCore/Sets/Fra/FraCards.cs`)", ""]
    for name, d in helpers:
        out.append(f"- `{name}` — {d}{ss(name)}")
    return "\n".join(out) + "\n"


def sheet(types, blocks, users_of_card, tests):
    cards = sorted(card_data(), key=lambda c: (["common", "uncommon", "rare", "mythic"].index(c["rarity"]) if c["rarity"] in ("common", "uncommon", "rare", "mythic") else 9, c["name"]))
    counts = defaultdict(lambda: [0, 0])
    entries = []
    keywords = magic_set.engine_keywords()
    for c in cards:
        name, oracle = c["name"], magic_set.oracle(c)
        status = magic_set.status(c, set(blocks), keywords) or "NOT IMPLEMENTED"
        if status == "keywords only":
            status = "keywords only — needs no script"
        counts[c["rarity"]][1] += 1
        counts[c["rarity"]][0] += status != "NOT IMPLEMENTED"
        e = [f"### {name}", "",
             f"{c.get('mana_cost') or ''} · {c.get('type_line', '')} · {c['rarity']}"
             + (f" · {c['power']}/{c['toughness']}" if c.get("power") else "")
             + (f" · loyalty {c['loyalty']}" if c.get("loyalty") else ""), "",
             "> " + oracle.replace("\n", "  \n> "), "",
             f"**Status:** {status}" + (f" · **tests naming it:** {', '.join(sorted(tests[name]))}" if tests.get(name) else
                                         " · no test names it" if status == "scripted" else "")]
        if name in blocks:
            pieces = sorted(users_of_card[name])
            if pieces:
                e.append("")
                e.append("**Engine pieces:** " + "; ".join(
                    f"`{p}` — {short(types[p]['doc'], 90)}" if p in types else f"`{p}`" for p in pieces))
            e += ["", "```csharp", re.sub(r"^\t\t\t", "", blocks[name], flags=re.M), "```"]
        entries.append("\n".join(e))
    head = ["# Reality Fracture — card sheet", "",
            "**Generated by `python tools/magic_docs.py` — do not edit.** Each card: its printed text, then the",
            "script that plays it and the engine pieces that script uses (meanings from",
            "`MagicCore/AbilityCatalog.md`). Read them side by side: a missing clause or a wrong \"whose\" shows.",
            "How to write a script: `MagicCore/Cookbook.md`.", "",
            "**Playable:** " + " · ".join(f"{r} {counts[r][0]}/{counts[r][1]}" for r in ("common", "uncommon", "rare", "mythic"))
            + f" · **{sum(v[0] for v in counts.values())}/{sum(v[1] for v in counts.values())}**", ""]
    return "\n".join(head) + "\n" + "\n\n".join(entries) + "\n"


def main():
    sys.stdout.reconfigure(encoding="utf-8")
    types = parse_types()
    # A record whose base is unknown would be in no section — say so, never drop it silently.
    for t in types.values():
        if t["base"] and t["base"] not in types and t["base"] not in NOT_PIECES:
            print(f"WARN: {t['name']} ({t['file']}) derives from {t['base']} — not in the catalog")
    enums = parse_enums()
    blocks, helpers = parse_scripts()
    users = used_by(types, blocks, helpers)
    of_card = defaultdict(set)
    for piece, cards in users.items():
        if piece in types:
            for c in cards:
                of_card[c].add(piece)
    outputs = {CATALOG: catalog(types, enums, blocks, helpers, users),
               SHEET: sheet(types, blocks, of_card, test_mentions())}
    stale = [p for p, text in outputs.items() if not p.exists() or p.read_text(encoding="utf-8") != text]
    if "--check" in sys.argv:
        for p in stale:
            print(f"STALE: {p.relative_to(ROOT)} — run python tools/magic_docs.py")
        sys.exit(1 if stale else 0)
    for p, text in outputs.items():
        p.write_text(text, encoding="utf-8", newline="\n")
        print(f"{p.relative_to(ROOT)}: {len(text.splitlines())} lines")


if __name__ == "__main__":
    main()
