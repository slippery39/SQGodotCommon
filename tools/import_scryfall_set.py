"""Import a set's rules data from Scryfall into MagicCore/Sets/<CODE>.json.

    python tools/import_scryfall_set.py fra
    python tools/import_scryfall_set.py lsv    # a cube: MagicCore/Sets/LSV.list.txt, by name (MC-4)

Keeps only what the rules engine reads (no prices, images or legality); one entry per card,
booster extras (showcase, borderless) excluded. Sorted by collector number so a re-import diffs cleanly.
A set's Special Guests (GUESTS) come after its own cards: printed in another set, opened in its packs.
"""

import json
import sys
import time
import urllib.error
import urllib.parse
import urllib.request
from pathlib import Path

CARD_FIELDS = ["name", "layout", "mana_cost", "cmc", "type_line", "oracle_text", "power", "toughness",
               "loyalty", "colors", "keywords", "rarity", "collector_number"]
FACE_FIELDS = ["name", "mana_cost", "type_line", "oracle_text", "power", "toughness", "loyalty", "colors"]

# Cards printed elsewhere that a set's boosters hold — they play as the set's own (story MC-3).
GUESTS = {"fra": "set:spg cn>=159 cn<=168", "trk": "set:sds"}

SETS = Path(__file__).resolve().parent.parent / "MagicCore" / "Sets"


def list_names(code):
    """A cube's card list (MC-4): MagicCore/Sets/<CODE>.list.txt, one name a line, # comments. None for a set."""
    path = SETS / f"{code.upper()}.list.txt"
    if not path.exists():
        return None
    lines = path.read_text(encoding="utf-8").splitlines()
    return [l.strip() for l in lines if l.strip() and not l.lstrip().startswith("#")]


def query(code):
    """The set's cards, booster extras excluded, and its Special Guests. fetch_set_art.py reads it too."""
    own = f"set:{code} not:extra"
    return f"({own}) or ({GUESTS[code]})" if code in GUESTS else own


def fetch(url):
    req = urllib.request.Request(url, headers={"User-Agent": "SQGodotCardGame/1.0 (personal project)",
                                               "Accept": "application/json"})
    with urllib.request.urlopen(req) as r:
        return json.load(r)


def post(url, body):
    req = urllib.request.Request(url, data=json.dumps(body).encode(), method="POST",
                                 headers={"User-Agent": "SQGodotCardGame/1.0 (personal project)",
                                          "Accept": "application/json", "Content-Type": "application/json"})
    with urllib.request.urlopen(req) as r:
        return json.load(r)


def search(q):
    url = "https://api.scryfall.com/cards/search?" + urllib.parse.urlencode(
        {"q": q, "unique": "cards", "order": "set"})
    cards = []
    while url:
        page = fetch(url)
        cards += page["data"]
        url = page.get("next_page")
        time.sleep(0.1)  # Scryfall asks for 50-100ms between requests
    return cards


def by_name(names):
    """Each name's card, in the list's order — 75 a request (Scryfall's limit); a name it can't match exactly
    (a front face, an accent) is looked up alone, exact then fuzzy, and named if still missing."""
    found = {}
    for i in range(0, len(names), 75):
        page = post("https://api.scryfall.com/cards/collection",
                    {"identifiers": [{"name": n} for n in names[i:i + 75]]})
        for c in page["data"]:
            found[c["name"]] = c
            for f in c.get("card_faces", []):
                found.setdefault(f["name"], c)
        time.sleep(0.1)
    out = []
    for n in names:
        if n not in found:
            for mode in ("exact", "fuzzy"):
                try:
                    found[n] = fetch("https://api.scryfall.com/cards/named?" + urllib.parse.urlencode({mode: n}))
                    break
                except urllib.error.HTTPError:
                    time.sleep(0.1)
            else:
                raise SystemExit(f"Scryfall has no card named '{n}'")
            time.sleep(0.1)
        out.append(found[n])
    return out


def cards_of(code):
    """Scryfall's cards for a set (its query) or a cube (its list, by name, in the list's order)."""
    names = list_names(code)
    return search(query(code)) if names is None else by_name(names)


def pick(obj, fields):
    return {f: obj[f] for f in fields if f in obj}


def main():
    code = sys.argv[1].lower()
    cards = cards_of(code)
    cube = list_names(code) is not None
    if not cube:  # the set's own cards first, then the guests (whose numbers would interleave)
        cards.sort(key=lambda c: (c["set"] != code, len(c["collector_number"]), c["collector_number"]))
    out = []
    for i, c in enumerate(cards, 1):
        card = pick(c, CARD_FIELDS)
        if cube:  # a cube's number is its place in the list; the rarity is the printing Scryfall returned
            card["collector_number"] = str(i)
        if "card_faces" in c:
            card["card_faces"] = [pick(f, FACE_FIELDS) for f in c["card_faces"]]
        out.append(card)

    path = SETS / f"{code.upper()}.json"
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(out, indent=1, ensure_ascii=False) + "\n", encoding="utf-8", newline="\n")
    print(f"{len(out)} cards -> {path}")


if __name__ == "__main__":
    main()
