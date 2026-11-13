"""The first pass of a new set's or cube's survey (MC-4.2; Shayne, 2026-10-29: "a script, checked by hand after").

    python tools/survey.py lsv      # -> MagicCore/LsvSurvey.md

Each card not yet playable is sorted by what it needs, read from Scryfall's keyword list, its layout and a few
rules-text patterns: HAVE (a piece the engine has), OPTION (an option on an existing piece, named) or NEW (a new
piece or rule, named). A card's tier is its worst need: 1 (only HAVE), 2 (an OPTION), 3 (a NEW). It does NOT read
the rest of the text: a tier-1 card can still hide a new phrase — the hand check reads each batch's cards before
it is built (/card-batch step 1). The tables below are the engine as of 2026-11-12 (AbilityCatalog.md, the
.claude/rules/magic-*.md files); a piece added later is a line here.
"""

import json
import re
import sys
from collections import Counter, defaultdict
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
import magic_set  # noqa: E402

ROOT = Path(__file__).resolve().parent.parent
H, O, N = "HAVE", "OPTION", "NEW"

# Scryfall keyword -> (kind, the piece or what is missing)
KEYWORDS = {
    **{k: (H, "engine keyword") for k in ("Flying", "Trample", "Lifelink", "Haste", "Vigilance", "Menace",
                                          "Deathtouch", "Reach", "First strike", "Double strike", "Defender",
                                          "Flash")},
    "Indestructible": (H, "s.IsIndestructible"), "Hexproof": (H, "TargetSpec hexproof"),
    "Enchant": (H, "AuraComponent"), "Equip": (H, "Equipment (Attachments)"),
    "Cycling": (H, "Scripting.Cycling"), "Landcycling": (H, "Cycling(cost, search)"),
    **{f"{t}cycling": (H, "Cycling(cost, search)") for t in ("Plains", "Island", "Swamp", "Mountain", "Forest")},
    "Typecycling": (O, "Cycling(cost, search) — a filter on the search"),
    "Mill": (H, "Mill"), "Surveil": (H, "Surveil"), "Scry": (O, "Surveil — a scry option (no Scry yet)"),
    "Treasure": (H, "Tokens.Treasure"), "Food": (H, "Tokens.Food"), "Investigate": (H, "Tokens.Clue"),
    "Kicker": (H, "KickerComponent"), "Multikicker": (O, "KickerComponent — kicked any number of times"),
    "Flashback": (H, "FlashbackComponent"), "Ward": (H, "ward"), "Explore": (H, "Explore"),
    "Fight": (H, "fight"), "Sunburst": (H, "Sunburst"), "Replicate": (H, "ReplicateComponent"),
    "Exhaust": (H, "ActivatedAbilityComponent.Exhaust"), "Prowess": (H, "FraCards.Prowess"),
    "Proliferate": (H, "proliferate"), "Prepared": (H, "Rules/Prepare.cs"), "Threshold": (H, "If + Count"),
    "Domain": (H, "Count.BasicLandTypesAmongLandsYouControl"), "Landfall": (H, "Trigger on a land entering"),
    "Metalcraft": (H, "If + Count"), "Ferocious": (H, "If + Count"), "Adapt": (H, "PutCounters under If"),
    "Annihilator": (H, "an attack trigger + sacrifice"), "Boast": (H, "Card.AttackedOnTurn + OncePerTurn"),
    "Flurry": (H, "Trigger.SpellNumber"), "Imprint": (H, "Exile { Linked }"), "Ravenous": (H, "X counters + If"),
    "Living weapon": (H, "CreateTokens.AttachTo"), "Mobilize": (H, "tokens entering attacking + sacrifice at end"),
    "Channel": (H, "ActivatedAbilityComponent.From hand"), "Exalted": (H, "'attacks alone' trigger"),
    "Delirium": (O, "Count — card TYPES in your graveyard (Tarmogoyf counts all graveyards)"),
    "Converge": (O, "OnStackComponent.ColorsSpent (Sunburst) read by a Count"),
    "Affinity": (O, "CostChange — less per artifact"), "Improvise": (O, "convoke — artifacts, generic only"),
    "Escalate": (O, "ModeChoice — a cost per extra mode"), "Fading": (O, "Vanishing — fade counters"),
    "Persist": (O, "a dies trigger returning it with a -1/-1 counter"),
    "Monstrosity": (O, "PutCounters + a 'monstrous' stamp"), "Amass": (O, "an Army token + counters"),
    "Backup": (O, "PutCounters on target + granted abilities"), "Hideaway": (O, "ExileTopPlayable — face down, free"),
    "Eternalize": (O, "CreateTokenCopy from the graveyard, changed"), "Squad": (O, "ReplicateComponent-like"),
    "Revolt": (O, "TurnTally — a permanent of yours left the battlefield"),
    "Shadow": (O, "CantBeBlockedBy — and blocks only shadow"), "Evoke": (O, "an alternative cost + sacrifice"),
    "Escape": (O, "MayCastThisFrom(Graveyard) + an exile cost"), "Dash": (O, "an alternative cost + haste + return"),
    "Ascend": (N, "the city's blessing"), "Delve": (N, "a cost paid by exiling graveyard cards"),
    "Buyback": (N, "buyback (returns to hand)"), "Overload": (N, "overload (target → each)"),
    "Splice": (N, "splice"), "Spree": (N, "spree (a cost per mode)"), "Ninjutsu": (N, "ninjutsu"),
    "Crew": (N, "Vehicles: crew"), "Reconfigure": (N, "reconfigure"), "Saddle": (N, "Mounts: saddle"),
    "Companion": (N, "companion"), "Bestow": (N, "bestow"), "Incubate": (N, "Incubator tokens (transform)"),
    "Manifest": (N, "face-down 2/2s"), "Manifest dread": (N, "face-down 2/2s"), "Plot": (N, "plot"),
    "Emerge": (N, "emerge"), "Miracle": (N, "miracle"), "Storm": (N, "storm"), "Cascade": (O, "ExileTopPlayable — Until MV less, Play.Now"),
    "Daybound": (N, "day and night"), "Nightbound": (N, "day and night"), "Level Up": (N, "level up"),
    "Protection": (N, "protection (CR 702.16)"), "Exert": (N, "exert"), "Transform": (N, "transforming DFCs"),
    "Warp": (N, "warp (EOE)"), "Earthbend": (N, "earthbend"), "Airbend": (N, "airbend"), "Waterbend": (N, "waterbend"),
    "Devoid": (O, "a colour characteristic"), "Will of the council": (N, "voting"), "For Mirrodin!": (H, "living weapon-like"),
    "Battle Cry": (H, "an attack trigger pump"), "Choose a background": (N, "backgrounds (commander)"),
    "Teamwork": (N, "teamwork"), "Protect": (N, "protect"), "Avoidance": (H, "a flicker (Flicker)"),
    "Magecraft": (H, "a SpellCast trigger (instant or sorcery) — copies too"),
}
# ability words and flavour keywords (Scryfall lists them) that need nothing on their own
IGNORED = {"Double", "Darkness", "Wasp's Sting", "Battle Cannon", "Venom Blast", "Mind Swap",
           "Terror from the Deep", "Benediction of the Omnissiah"}

LAYOUTS = {"transform": (N, "transforming DFCs"), "modal_dfc": (N, "modal DFCs (play either face)"),
           "adventure": (N, "adventures"), "split": (N, "split cards"), "class": (N, "Classes (levels)"),
           "leveler": (N, "level up"), "saga": (H, "SagaComponent"), "prepare": (H, "Rules/Prepare.cs")}

# rules-text patterns the keyword list misses
TEXT = [
    (r"extra turn", H, "TakeExtraTurn"),
    (r"\bstorm\b", N, "storm"),
    (r"\{E\}|energy counter", N, "energy"),
    (r"the monarch", N, "the monarch"),
    (r"the initiative|venture into", N, "dungeons"),
    (r"poison counter|toxic|infect", N, "poison"),
    (r"protection from", N, "protection (CR 702.16)"),
    (r"face down", N, "face-down cards (morph, manifest)"),
    (r"\bvote\b", N, "voting"),
    (r"split second", H, "split second"),
    (r"can't be countered", H, "CantBeCountered"),
    (r"phases? out", H, "Rules.Phasing"),
    (r"\bRole\b", N, "Role tokens"), (r"\bMap token", N, "Map tokens"), (r"\bBlood token", N, "Blood tokens"),
    (r"\bPowerstone", N, "Powerstone tokens"), (r"\bGold token", N, "Gold tokens"),
    (r"for each opponent|each opponent", H, "Ref opponents (two-player)"),
    (r"you may cast (it|that card|spells?) .*without paying", H, "CastNow free / MayCastFromHandFree"),
    (r"copy (of )?target (instant|sorcery|spell)", H, "CopySpell"),
    (r"lose the game|win the game", H, "WinTheGame / LoseTheGame"),
    (r"\bmutate\b", N, "mutate"),
    (r"your maximum hand size|no maximum hand size", H, "TurnStructure.MaxHandSizeOf"),
]


def needs_of(c, keywords):
    out = []
    for k in c.get("keywords", []):
        if k in IGNORED:
            continue
        out.append(KEYWORDS.get(k, (N, f"? {k} (not in the survey's table)")))
    if c.get("layout") in LAYOUTS:
        out.append(LAYOUTS[c["layout"]])
    text = magic_set.oracle(c)
    for pat, kind, piece in TEXT:
        if re.search(pat, text, re.I):
            out.append((kind, piece))
    return list(dict.fromkeys(out))


def tier(needs):
    kinds = {k for k, _ in needs}
    return 3 if N in kinds else 2 if O in kinds else 1


def main():
    code = sys.argv[1].upper()
    cards = json.loads((ROOT / "MagicCore" / "Sets" / f"{code}.json").read_text(encoding="utf-8"))
    keys, kw = magic_set.all_script_keys(), magic_set.engine_keywords()
    played = [c for c in cards if magic_set.status(c, keys, kw)]
    todo = [(c, needs_of(c, kw)) for c in cards if not magic_set.status(c, keys, kw)]
    tiers = Counter(tier(n) for _, n in todo)
    new = defaultdict(list)
    opt = defaultdict(list)
    for c, n in todo:
        for kind, piece in n:
            if kind == N:
                new[piece].append(c["name"])
            elif kind == O:
                opt[piece].append(c["name"])
    lines = [f"# {code} survey — the first pass (`tools/survey.py {code.lower()}`, MC-4.2)", "",
             "GENERATED — never hand-edit; the hand check and the batch plan are in `docs/stories/MC-4-powered-cube.md`.",
             "A tier from keywords, layout and a few text patterns only — every batch's cards are read before they are",
             "built (`/card-batch` step 1).", "",
             f"**{len(cards)} cards:** {len(played)} play today (scripted in FRA/TRK, or keywords only); "
             f"of the other {len(todo)}, tier 1 (only pieces the engine has) {tiers[1]}, tier 2 (an option on a piece) "
             f"{tiers[2]}, tier 3 (a new piece or rule) {tiers[3]}.", "",
             "## New pieces and rules, by how many cards need them", ""]
    for piece, names in sorted(new.items(), key=lambda kv: (-len(kv[1]), kv[0])):
        lines.append(f"- **{piece}** ({len(names)}): {', '.join(names)}")
    lines += ["", "## Options on existing pieces", ""]
    for piece, names in sorted(opt.items(), key=lambda kv: (-len(kv[1]), kv[0])):
        lines.append(f"- **{piece}** ({len(names)}): {', '.join(names)}")
    lines += ["", "## Every card, by tier", ""]
    for t in (1, 2, 3):
        lines += [f"### Tier {t}", ""]
        for c, n in sorted((x for x in todo if tier(x[1]) == t), key=lambda x: x[0]["name"]):
            what = "; ".join(f"{k.lower()}: {p}" for k, p in n) or "nothing the keywords name"
            lines.append(f"- {c['name']} ({c.get('type_line', '')}) — {what}")
        lines.append("")
    lines += ["## Already playable", "", ", ".join(c["name"] for c in played), ""]
    out = ROOT / "MagicCore" / f"{code.title()}Survey.md"
    out.write_text("\n".join(lines), encoding="utf-8", newline="\n")
    print(f"{out}: {len(played)} playable, tiers {dict(sorted(tiers.items()))}, {len(new)} new pieces")


if __name__ == "__main__":
    main()
