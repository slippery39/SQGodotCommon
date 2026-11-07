"""PROVISIONAL TRK play-booster collation, built from Wizards' published slot odds (story MC-5.4).

    python tools/build_trk_boosters.py      # -> MagicCore/Sets/TRK.boosters.json

MTGJSON had no TRK booster data (checked 2026-10-31 and 2026-11-07). Once it has, replace this file's
output with `python tools/import_mtgjson_boosters.py trk` and delete this script.

Source: https://magic.wizards.com/en/news/feature/collecting-star-trek (Play Booster section). Every
number below is Wizards', except the assumptions, each marked ASSUMPTION and listed in MC-5's Decisions:
- a treatment (Klingon, scene, LCARS, classic Enterprise, borderless land) is the same card to the rules,
  so its odds are folded onto the card's name, as import_mtgjson_boosters.py folds FRA's;
- which printing is which treatment is read from Scryfall's collector-number runs (checked against
  Wizards' counts below — the script stops if they differ);
- "less than 1%" shares what the slot's stated odds leave of 100%, evenly;
- within a group, every card is equally likely (Wizards gives only the group's total);
- the Stardates sheet is uniform over its 30 cards;
- Arena opens paper's foil slot as a second non-foil card with the same odds (as FRA, LIM-1).
"""

import json
import time
import urllib.parse
import urllib.request
from pathlib import Path

SOURCE = "https://magic.wizards.com/en/news/feature/collecting-star-trek"

# Treatment groups by collector-number run (Scryfall, 2026-11-07); a land in the scene run is a borderless land,
# a rare land in the LCARS run an LCARS shock land.
RUNS = {"scene": (332, 362), "lcars": (363, 401), "enterprise": (402, 425), "klingon": (426, 435)}
# Wizards' counts of each group's cards in the common and uncommon slots — the runs must reproduce them.
COUNTS = {("klingon", "common"): 1, ("scene", "uncommon"): 10, ("lcars", "uncommon"): 7,
          ("enterprise", "uncommon"): 5, ("klingon", "uncommon"): 6}

LT1 = None  # Wizards' "less than 1%"
# Each slot: (group, rarity) -> percent. "main" is the main set's default printings.
SLOTS = {
    "common": {("main", "common"): 97.1, ("klingon", "common"): 2.9},
    "uncommon": {("main", "uncommon"): 78.1, ("scene", "uncommon"): 7.7, ("lcars", "uncommon"): 4.6,
                 ("enterprise", "uncommon"): 4.6, ("klingon", "uncommon"): 5.0},
    "wildcard": {("main", "common"): 20.8, ("main", "uncommon"): 54.1, ("main", "rare"): 16.7,
                 ("main", "mythic"): 2.6,
                 ("klingon", "common"): LT1, ("klingon", "uncommon"): 1.0, ("klingon", "rare"): LT1,
                 ("scene", "uncommon"): 1.5, ("scene", "rare"): LT1, ("scene", "mythic"): LT1,
                 ("lcars", "uncommon"): 1.0, ("lcars", "rare"): LT1, ("lcars", "mythic"): LT1,
                 ("enterprise", "rare"): LT1, ("enterprise", "mythic"): LT1,
                 ("land", "rare"): LT1, ("land", "mythic"): LT1, ("shock", "rare"): LT1},
    "rareMythic": {("main", "rare"): 80.1, ("main", "mythic"): 12.4, ("klingon", "rare"): LT1,
                   ("scene", "rare"): 1.0, ("scene", "mythic"): LT1, ("lcars", "rare"): 1.8,
                   ("lcars", "mythic"): LT1, ("enterprise", "rare"): 1.5, ("enterprise", "mythic"): LT1,
                   ("land", "rare"): 1.0, ("land", "mythic"): LT1, ("shock", "rare"): 1.4},
    "foil": {("main", "common"): 51.2, ("main", "uncommon"): 34.7, ("main", "rare"): 6.3,
             ("main", "mythic"): 1.0,
             ("klingon", "common"): LT1, ("klingon", "uncommon"): 1.0, ("klingon", "rare"): LT1,
             ("scene", "uncommon"): 1.6, ("scene", "rare"): LT1, ("scene", "mythic"): LT1,
             ("lcars", "uncommon"): 1.1, ("lcars", "rare"): LT1, ("lcars", "mythic"): LT1,
             ("enterprise", "rare"): LT1, ("enterprise", "mythic"): LT1,
             ("land", "rare"): LT1, ("land", "mythic"): LT1, ("shock", "rare"): LT1},
    # 50% a common dual (non-foil 40 + foil 10), 50% a basic (default, holodeck, away team; foil or not)
    "land": {("dual", "common"): 50.0, ("basic", "common"): 50.0},
    "specialGuest": {("stardate", "mythic"): 100.0},
}
# "14 Magic: The Gathering cards": 6–7 commons — "In 1 out of 25 Play Boosters, a Stardates card will
# replace a common" — 3 uncommons, a wildcard, a rare or mythic, a traditional foil, a land.
PACK = {"uncommon": 3, "wildcard": 1, "rareMythic": 1, "foil": 1, "land": 1}
BOOSTERS = [{"weight": 24, "contents": {"common": 7, **PACK}},
            {"weight": 1, "contents": {"common": 6, "specialGuest": 1, **PACK}}]


def fetch_all(q):
    url = "https://api.scryfall.com/cards/search?" + urllib.parse.urlencode({"q": q, "unique": "prints"})
    rows = []
    while url:
        req = urllib.request.Request(url, headers={"User-Agent": "SQGodotCardGame/1.0 (personal project)",
                                                   "Accept": "application/json"})
        with urllib.request.urlopen(req) as r:
            page = json.load(r)
        rows += page["data"]
        url = page.get("next_page")
        time.sleep(0.1)
    return rows


def groups(trk_json, prints):
    """(group, rarity) -> card names. "main" from the set's own file; the treatments from Scryfall's runs."""
    own, guests = trk_json[:311], trk_json[311:]
    g = {}
    for c in own:
        t = c["type_line"]
        key = ("basic" if t.startswith("Basic Land") else "dual" if "Planet" in t
               else "main", c["rarity"])
        g.setdefault(key, set()).add(c["name"])
    g[("stardate", "mythic")] = {c["name"] for c in guests}
    for p in prints:
        n = int("".join(ch for ch in p["collector_number"] if ch.isdigit()) or 0)
        run = next((r for r, (lo, hi) in RUNS.items() if lo <= n <= hi), None)
        if not run:
            continue
        land = "Land" in p["type_line"]
        group = "land" if run == "scene" and land else "shock" if run == "lcars" and land else run
        g.setdefault((group, p["rarity"]), set()).add(p["name"])
    return g


def main():
    root = Path(__file__).resolve().parent.parent
    trk = json.loads((root / "MagicCore" / "Sets" / "TRK.json").read_text(encoding="utf-8"))
    assert all(c["rarity"] == "mythic" for c in trk[311:]) and len(trk) == 341, "TRK.json: 311 + 30 Stardates"
    g = groups(trk, fetch_all("set:trk"))
    for key, n in COUNTS.items():
        assert len(g.get(key, ())) == n, f"{key}: {len(g.get(key, ()))} cards, Wizards says {n}"
    assert len(g[("main", "common")]) == 81, "Wizards: 81 commons"
    assert len(g[("main", "uncommon")]) == 110, "Wizards: 110 uncommons"

    sheets = {}
    for slot, odds in SLOTS.items():
        unstated = [k for k, v in odds.items() if v is None]
        rest = (100 - sum(v for v in odds.values() if v is not None)) / max(len(unstated), 1)
        cards = {}
        for key, pct in odds.items():
            assert key in g, f"{slot}: Wizards lists {key}, the collector-number runs found none"
            names = sorted(g[key])
            for name in names:
                cards[name] = cards.get(name, 0) + (rest if pct is None else pct) / len(names)
        sheets[slot] = {"cards": {n: max(1, round(w * 10000)) for n, w in sorted(cards.items())}}

    out = {
        "source": f"PROVISIONAL — built by tools/build_trk_boosters.py from Wizards' published odds ({SOURCE}), "
                  "MTGJSON having none; replace with import_mtgjson_boosters.py trk once it has",
        "boosters": BOOSTERS,
        "sheets": sheets,
    }
    path = root / "MagicCore" / "Sets" / "TRK.boosters.json"
    path.write_text(json.dumps(out, indent=1, ensure_ascii=False) + "\n", encoding="utf-8", newline="\n")
    print({k: len(v["cards"]) for k, v in sheets.items()}, "->", path)


if __name__ == "__main__":
    main()
