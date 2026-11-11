"""A variant of an Arena deck export, made by swapping cards (the /sealed-test skill).

    python tools/deck_variant.py BASE.txt OUT.txt --swap "Old Card=New Card" [--swap ...]
    python tools/deck_variant.py --test

Each swap takes ONE copy of Old Card out of the Deck section and puts New Card in its place, at the
same position: the variant's cards keep their list order, so `--builds ... --both-seats` deals both
lists the same shuffles and the paired difference measures the swap alone. New Card comes from the
Sideboard section (with its set and number); a basic land not in the sideboard is given with its set,
"Forest=Swamp (MIR) 340". The card swapped out goes to the sideboard (basics don't: they're free).
Names match case-insensitively, a spaced or unspaced ellipsis alike ("All Good Things . . .").
"""

import re
import sys

BASICS = {"plains", "island", "swamp", "mountain", "forest"}
LINE = re.compile(r"^(\d{1,3})x?\s+(.+?)(\s+\([A-Za-z0-9]+\)(?:\s+\S+)?)?$")


def key(name):
    return re.sub(r"\s*\.\s*\.\s*\.", "...", name).strip().lower()


def parse(text):
    """
    The export as (deck, sideboard): lists of [count, name, suffix]. As MagicCore's ArenaDeck.Parse reads it: the main
    deck starts after a "Deck" header (a Companion or Commander section before it is skipped) and ends at the first
    blank line or header; everything after is the sideboard. A card line that can't be read stops the tool.
    """
    lines = [l.strip() for l in text.replace("\r", "").split("\n")]
    start = next((i + 1 for i, l in enumerate(lines) if l.lower() == "deck"), 0)
    deck, side, past_deck = [], [], False
    for line in lines[start:]:
        if not line or not line[0].isdigit():
            past_deck = past_deck or bool(deck)  # the blank line or header after the main deck
            continue
        m = LINE.match(line)
        if not m:
            raise SystemExit(f"can't read the line {line!r}")
        (side if past_deck else deck).append([int(m.group(1)), m.group(2), m.group(3) or ""])
    return deck, side


def swap(deck, side, old, new):
    i = next((i for i, (_, n, _) in enumerate(deck) if key(n) == key(old)), None)
    if i is None:
        raise SystemExit(f"{old}: not in the deck")
    m = LINE.match("1 " + new)
    new_name, new_suffix = m.group(2), m.group(3) or ""
    j = next((j for j, (_, n, _) in enumerate(side) if key(n) == key(new_name)), None)
    if j is not None:
        new_name, new_suffix = side[j][1], new_suffix or side[j][2]
        side[j][0] -= 1
        if side[j][0] == 0:
            del side[j]
    elif key(new_name) not in BASICS:
        raise SystemExit(f"{new_name}: not in the sideboard (a basic needs its set: 'Swamp (MIR) 340')")
    elif not new_suffix:
        raise SystemExit(f"{new_name}: give its set and number, e.g. '{new_name} (MIR) 340'")
    count, old_name, old_suffix = deck[i]
    # the LAST copy of the line becomes the new card, so every other card keeps its position
    if count == 1:
        deck[i] = [1, new_name, new_suffix]
    else:
        deck[i][0] -= 1
        deck.insert(i + 1, [1, new_name, new_suffix])
    if key(old_name) not in BASICS:
        k = next((k for k, (_, n, _) in enumerate(side) if key(n) == key(old_name)), None)
        if k is None:
            side.append([1, old_name, old_suffix])
        else:
            side[k][0] += 1


def render(deck, side):
    lines = ["Deck"] + [f"{c} {n}{s}" for c, n, s in deck]
    if side:
        lines += ["", "Sideboard"] + [f"{c} {n}{s}" for c, n, s in side]
    return "\n".join(lines) + "\n"


def test():
    base = "Deck\n5 Forest (MIR) 347\n1 Tea, Earl Grey, Hot (TRK) 269\n2 Tricorder (TRK) 270\n\nSideboard\n1 Lore, Positronic Provocateur (TRK) 246\n1 All Good Things . . . (TRK) 226\n"
    deck, side = parse(base)
    swap(deck, side, "Tea, Earl Grey, Hot", "Lore, Positronic Provocateur")
    swap(deck, side, "Forest", "Swamp (MIR) 340")
    swap(deck, side, "Tricorder", "All Good Things...")
    out = render(deck, side)
    assert out.split("\n")[1:7] == [
        "4 Forest (MIR) 347",
        "1 Swamp (MIR) 340",
        "1 Lore, Positronic Provocateur (TRK) 246",
        "1 Tricorder (TRK) 270",
        "1 All Good Things . . . (TRK) 226",
        "",
    ], out
    assert "1 Tea, Earl Grey, Hot (TRK) 269" in out and "1 Tricorder (TRK) 270" in out.split("Sideboard")[1]
    assert sum(c for c, _, _ in deck) == 8
    # no Sideboard header: a blank line ends the deck; "4x" is a count
    deck, side = parse("4x Tricorder (TRK) 270\n1 Tea, Earl Grey, Hot\n\n1 Lore, Positronic Provocateur")
    assert [c for c, _, _ in deck] == [4, 1] and [n for _, n, _ in side] == ["Lore, Positronic Provocateur"], (deck, side)
    print("ok")


if __name__ == "__main__":
    if sys.argv[1:] == ["--test"]:
        test()
        sys.exit(0)
    args = sys.argv[1:]
    if len(args) < 4 or "--swap" not in args:
        sys.exit(__doc__)
    src, dst = args[0], args[1]
    swaps = [args[i + 1] for i, a in enumerate(args) if a == "--swap"]
    deck, side = parse(open(src, encoding="utf-8").read())
    before = sum(c for c, _, _ in deck)
    for s in swaps:
        old, new = s.split("=", 1)
        swap(deck, side, old.strip(), new.strip())
    open(dst, "w", encoding="utf-8").write(render(deck, side))
    print(f"{dst}: {before} cards, {len(swaps)} swap(s): " + "; ".join(swaps))
