"""Download Scryfall's official mana symbols for the MagicTable front end (LIM-5.1).

    python tools/fetch_mana_symbols.py

Every symbol that can appear in a mana cost, plus {T} and {Q}, as
SQGodotCommon/MagicTable/Assets/Symbols/<name>.svg — named as Scryfall names them ({2/W} is 2W.svg,
{B/P} BP.svg; ManaSymbols.cs maps a symbol to its file the same way). Files already on disk are
skipped. Then import them: godot-mono --path SQGodotCommon --headless --import
"""

import json
from pathlib import Path

from fetch_set_art import get

OUT = Path(__file__).resolve().parent.parent / "SQGodotCommon" / "MagicTable" / "Assets" / "Symbols"


def main():
    OUT.mkdir(parents=True, exist_ok=True)
    symbols = json.loads(get("https://api.scryfall.com/symbology"))["data"]
    wanted = [s for s in symbols if s["appears_in_mana_costs"] or s["symbol"] in ("{T}", "{Q}")]
    fetched = 0
    for s in wanted:
        target = OUT / s["svg_uri"].rsplit("/", 1)[-1]
        if not target.exists():
            target.write_bytes(get(s["svg_uri"]))
            fetched += 1
    print(f"{len(wanted)} symbols, {fetched} fetched, in {OUT}")


if __name__ == "__main__":
    main()
