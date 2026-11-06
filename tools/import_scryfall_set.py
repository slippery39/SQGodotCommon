"""Import a set's rules data from Scryfall into MagicCore/Sets/<CODE>.json.

    python tools/import_scryfall_set.py fra

Keeps only what the rules engine reads (no prices, images or legality); one entry per card,
booster extras (showcase, borderless) excluded. Sorted by collector number so a re-import diffs cleanly.
A set's Special Guests (GUESTS) come after its own cards: printed in another set, opened in its packs.
"""

import json
import sys
import time
import urllib.parse
import urllib.request
from pathlib import Path

CARD_FIELDS = ["name", "layout", "mana_cost", "cmc", "type_line", "oracle_text", "power", "toughness",
               "loyalty", "colors", "keywords", "rarity", "collector_number"]
FACE_FIELDS = ["name", "mana_cost", "type_line", "oracle_text", "power", "toughness", "loyalty"]

# Cards printed elsewhere that a set's boosters hold — they play as the set's own (story MC-3).
GUESTS = {"fra": "set:spg cn>=159 cn<=168", "trk": "set:sds"}


def query(code):
    """The set's cards, booster extras excluded, and its Special Guests. fetch_set_art.py reads it too."""
    own = f"set:{code} not:extra"
    return f"({own}) or ({GUESTS[code]})" if code in GUESTS else own


def fetch(url):
    req = urllib.request.Request(url, headers={"User-Agent": "SQGodotCardGame/1.0 (personal project)",
                                               "Accept": "application/json"})
    with urllib.request.urlopen(req) as r:
        return json.load(r)


def pick(obj, fields):
    return {f: obj[f] for f in fields if f in obj}


def main():
    code = sys.argv[1].lower()
    url = "https://api.scryfall.com/cards/search?" + urllib.parse.urlencode(
        {"q": query(code), "unique": "cards", "order": "set"})
    cards = []
    while url:
        page = fetch(url)
        cards += page["data"]
        url = page.get("next_page")
        time.sleep(0.1)  # Scryfall asks for 50-100ms between requests

    # the set's own cards first, then the guests (whose numbers would interleave)
    cards.sort(key=lambda c: (c["set"] != code, len(c["collector_number"]), c["collector_number"]))
    out = []
    for c in cards:
        card = pick(c, CARD_FIELDS)
        if "card_faces" in c:
            card["card_faces"] = [pick(f, FACE_FIELDS) for f in c["card_faces"]]
        out.append(card)

    path = Path(__file__).resolve().parent.parent / "MagicCore" / "Sets" / f"{code.upper()}.json"
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(out, indent=1, ensure_ascii=False) + "\n", encoding="utf-8", newline="\n")
    print(f"{len(out)} cards -> {path}")


if __name__ == "__main__":
    main()
