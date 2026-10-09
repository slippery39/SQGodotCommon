"""Download a set's art from Scryfall for the MagicTable front end.

    python tools/fetch_set_art.py fra

Per card (and per token of the set's token set, t<code>), by name slug:
  SQGodotCommon/MagicTable/Assets/Art/<slug>.jpg    art crop  — battlefield card crops
  SQGodotCommon/MagicTable/Assets/Cards/<slug>.jpg  full card — hand and preview (exact Oracle text)
Files already on disk are skipped, so a re-run only fetches what is missing.
"""

import json
import re
import sys
import time
import urllib.error
import urllib.parse
import urllib.request
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent / "SQGodotCommon" / "MagicTable" / "Assets"
HEADERS = {"User-Agent": "SQGodotCardGame/1.0 (personal project)", "Accept": "*/*"}


def get(url):
    for attempt in range(4):
        try:
            with urllib.request.urlopen(urllib.request.Request(url, headers=HEADERS), timeout=30) as r:
                return r.read()
        except urllib.error.HTTPError as e:
            if (e.code == 429 or e.code >= 500) and attempt < 3:
                time.sleep(2 ** attempt)
                continue
            raise
        except (urllib.error.URLError, TimeoutError):  # stalled or dropped connection
            if attempt == 3:
                raise
            time.sleep(2 ** attempt)


def search(query):
    url = "https://api.scryfall.com/cards/search?" + urllib.parse.urlencode(
        {"q": query, "unique": "cards", "order": "set"})
    cards = []
    while url:
        try:
            page = json.loads(get(url))
        except urllib.error.HTTPError as e:
            if e.code == 404:  # no cards match (e.g. a set without tokens)
                return cards
            raise
        cards += page["data"]
        url = page.get("next_page")
        time.sleep(0.1)  # Scryfall asks for 50-100ms between requests
    return cards


def slug(name):
    return re.sub(r"[^a-z0-9]+", "_", name.lower()).strip("_")


def images(card):
    # Single-image layouts (normal, prepare) carry image_uris at the top; true two-faced cards per face.
    if "image_uris" in card:
        return [(card["name"], card["image_uris"])]
    return [(f["name"], f["image_uris"]) for f in card.get("card_faces", []) if "image_uris" in f]


def main():
    code = sys.argv[1].lower()
    (ROOT / "Art").mkdir(parents=True, exist_ok=True)
    (ROOT / "Cards").mkdir(parents=True, exist_ok=True)
    cards = search(f"set:{code} not:extra") + search(f"set:t{code}")
    fetched = skipped = 0
    for card in cards:
        for name, uris in images(card):
            for folder, kind in (("Art", "art_crop"), ("Cards", "normal")):
                path = ROOT / folder / f"{slug(name)}.jpg"
                if path.exists():
                    skipped += 1
                    continue
                part = path.with_suffix(".part")  # a killed run never leaves a truncated .jpg
                part.write_bytes(get(uris[kind]))
                part.replace(path)
                fetched += 1
                time.sleep(0.1)
    print(f"{len(cards)} cards: fetched {fetched} images, {skipped} already on disk -> {ROOT}")


if __name__ == "__main__":
    main()
