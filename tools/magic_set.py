"""Set lookups for MagicCore — card text, and what is playable.

    python tools/magic_set.py text "Gideon the Oathless" "Gardenize"   # printed text of these cards
    python tools/magic_set.py text --missing                         # every card not yet playable
    python tools/magic_set.py coverage                               # playable by rarity
    python tools/magic_set.py ... --set FRA                          # another set's JSON (default FRA)

"Playable" = scripted in ANY set's Cards file (a reprint is scripted once, MC-5.5), keywords only
(the engine plays those unscripted), or a basic land. THE rule — tools/magic_docs.py imports it; the
keyword list is read from CardRegistry's EngineKeywords, so a keyword the engine learns is learned here too.
"""

import json
import re
import sys
from collections import Counter
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
RARITIES = ["common", "uncommon", "rare", "mythic"]


def scripts_file(set_code):
    title = set_code[0] + set_code[1:].lower()
    return ROOT / "MagicCore" / "Sets" / title / f"{title}Cards.cs"


def engine_keywords():
    """CardRegistry's EngineKeywords — the keywords the engine plays with no script."""
    text = (ROOT / "MagicCore" / "Sets" / "CardRegistry.cs").read_text(encoding="utf-8")
    m = re.search(r"EngineKeywords\s*=\s*ImmutableHashSet\.Create\((.*?)\);", text, re.S)
    return {k.lower() for k in re.findall(r'"([^"]+)"', m.group(1))} if m else set()


def script_keys(set_code="FRA"):
    """Card names with a script — keys of the Cards file's Scripts dictionary only."""
    text = scripts_file(set_code).read_text(encoding="utf-8")
    start = text.find("Scripts =")
    names = re.findall(r'^\t\t\t\["(.+?)"\] =', text[start:], re.M) if start >= 0 else []
    return {n.replace('\\"', '"') for n in names}  # C# escapes a quote: Dr. Leonard \"Bones\" McCoy


def all_script_keys():
    """Every set's scripted names — a card plays in every set that prints it."""
    return set().union(*(script_keys(f.parent.name.upper()) for f in (ROOT / "MagicCore" / "Sets").glob("*/*Cards.cs")))


def load(set_code):
    d = json.loads((ROOT / "MagicCore" / "Sets" / f"{set_code}.json").read_text(encoding="utf-8"))
    cards = d if isinstance(d, list) else d.get("cards") or d.get("data")
    return cards, all_script_keys()


def oracle(c):
    if c.get("oracle_text"):
        return c["oracle_text"]
    return "\n// \n".join(f.get("oracle_text") or "" for f in c.get("card_faces") or [])


def keyword_only(text, keywords):
    lines = [l for l in text.split("\n") if l.strip()]
    return all(all(p.strip().lower() in keywords for p in l.split(",")) for l in lines)


def status(c, keys, keywords):
    """'scripted', 'basic land', 'keywords only' or None (not playable)."""
    if c["name"] in keys:
        return "scripted"
    if "Basic Land" in (c.get("type_line") or ""):
        return "basic land"
    return "keywords only" if keyword_only(oracle(c), keywords) else None


def playable(c, keys, keywords=None):
    return status(c, keys, engine_keywords() if keywords is None else keywords) is not None


def show(c):
    stats = f" · {c['power']}/{c['toughness']}" if c.get("power") else ""
    stats += f" · loyalty {c['loyalty']}" if c.get("loyalty") else ""
    print(f"== {c['name']} | {c.get('mana_cost') or '—'} | {c.get('type_line')} | {c['rarity']}{stats}")
    print(oracle(c))
    print()


def main():
    sys.stdout.reconfigure(encoding="utf-8")
    args = sys.argv[1:]
    set_code = args[args.index("--set") + 1].upper() if "--set" in args else "FRA"
    args = [a for i, a in enumerate(args) if a != "--set" and (i == 0 or args[i - 1] != "--set")]
    cards, keys = load(set_code)
    keywords = engine_keywords()
    if not args:
        raise SystemExit(__doc__)
    if args[0] == "coverage":
        total, ok = Counter(), Counter()
        for c in cards:
            total[c["rarity"]] += 1
            ok[c["rarity"]] += playable(c, keys, keywords)
        parts = [f"{r} {ok[r]}/{total[r]}" for r in RARITIES if total[r]]
        print(" · ".join(parts) + f" · **{sum(ok.values())}/{sum(total.values())}**")
        missing = [c["name"] for c in cards if not playable(c, keys, keywords)]
        if missing:
            print(f"not playable ({len(missing)}): " + ", ".join(missing))
    elif args[0] == "text":
        if "--missing" in args:
            chosen = [c for c in cards if not playable(c, keys, keywords)]
        else:
            wanted = [a.lower() for a in args[1:]]
            chosen = [c for c in cards if c["name"].lower() in wanted or c["name"].split(" // ")[0].lower() in wanted]
            for w in wanted:
                if not any(w in (c["name"].lower(), c["name"].split(" // ")[0].lower()) for c in cards):
                    print(f"?? no card named '{w}'")
        for c in chosen:
            show(c)
    else:
        raise SystemExit(__doc__)


if __name__ == "__main__":
    main()
