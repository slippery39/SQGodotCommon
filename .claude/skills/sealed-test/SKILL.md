---
name: sealed-test
description: Test Shayne's sealed (or draft) deck lists in MagicCore by simulation — against the AI's own sealed decks or against a list of his, on the play and on the draw — and read the report as a player would. Use when he pastes an Arena deck export and asks "which build is better", "test these lists", "which card for the last slot", "should I play X or Y", "how does my deck do against aggro", wants per-card win rates (GIH / IWD) for his deck, or wants sample games to watch on the table. Covers making variants that pair cleanly, sizing a run to finish in about an hour, and the three ways a report misleads.
---

# Testing a sealed deck by simulation

Built 2026-11-10 over Shayne's first TRK sealed pools (story LIM-1.12; the runs and what they showed are in
`docs/findings/limited.md`, "Shayne's TRK sealed lists"). The search AI plays BOTH sides, so every number
is what **this AI** does with the 40 — read section 5 before trusting one.

## 1. Save his lists

Each pasted Arena export (Deck + Sideboard sections — Arena exports the unplayed pool as the sideboard,
and cards "from outside the game" need it) to `sim_results/builds/<Name>.txt` (git-ignored, disposable).
Name it by its colours or its idea (`RB`, `Soup-Riker`). Check it parses before any run:

```
dotnet run -c Release --project MagicConsole -- --set=trk --builds=sim_results/builds/A.txt --both-seats --games=2
```

A card the engine can't play stops it with the card's name — a silent substitute would test a different deck.

## 2. Variants: change ONE thing, in place

```
python tools/deck_variant.py sim_results/builds/A.txt sim_results/builds/A-X.txt --swap "Old Card=New Card"
```

The new card takes the old one's position, so both lists get the same shuffles and the games play out
alike until it is drawn — the paired difference is then sharp (±2.3–3 points at 100 opponents). Swaps
come from the sideboard; a basic needs its set ("Forest=Swamp (MIR) 340"). **Several changes at once,
or a change in land COUNTS, shift every card's position** and the pairing is lost (Soup-Red: ±5.2 at 75
opponents, where a one-card swap gave ±2.9 at 50). One variable a run; split a bundle that comes out clear.

## 3. Size the run, then ask

`--games=N` is the number of opponents; each plays every list twice, once on the play, once on the
draw. **~10 games a minute** on 16 cores while nothing else runs; slow decks (many decisions a turn) less.

| Lists | Opponents | Games | Time |
|---|---|---|---|
| 2 | 50 | 200 | ~25–30 min |
| 3 | 50 | 300 | ~35–45 min |
| 3 | 100 | 600 | ~60–90 min |

Shayne wants an answer within about an hour, not overnight: default 50 opponents, 1.5x (75) when a
result is close. State the run's size and time before starting it (any run over ~15 min — memory).
`--game-minutes=6` stops a game past six minutes (a Tribbles board can run for hours — AI-21) and logs
its board; more than ~5% stopped means a deck's long games are missing from its number.

```
dotnet run -c Release --project MagicConsole -- --set=trk --builds=sim_results/builds/A.txt,sim_results/builds/A-X.txt --both-seats --games=50 --game-minutes=6
```

Run it in the background, redirected to a log in `sim_results/builds/`; the progress line has each list's
running win rate. The report goes to `ai_reports/sealed-builds-<time>.md` — give him its link.

**Against one of his lists instead of the AI's decks** (the AI's decks are weaker than his — his RB beat
them 60% and his soup 64%): `--opponent=sim_results/builds/RB.txt`, every game that list with a fresh
shuffle. `--first-opponent=N` starts the seeds at N, so a second run plays NEW games (the same numbers
replay the same games). **Games to watch on the table:**

```
dotnet run -c Release --project MagicConsole -- --set=trk --sample-games=sim_results/builds/A.txt,sim_results/builds/B.txt --games=6 --first-seed=501
./Run-Godot.ps1 MagicTable/MagicTableScene.tscn -GameArgs '--resume=<full path of a game_records/sample-*.json>'
```

(or the deck select's "Resume a game"; Undo steps back; the `.md` beside each is the game to read).

## 4. Reading the report

- **The paired line is the answer** (`A − B: +x ± y, paired game by game`). A win rate's own ± is ~5 at
  50 opponents; the pair's is smaller. Call it a difference at about 2 standard errors, "leans" at 1.
- **Play and draw apart**: an aggro deck lives on the play (his RB: 64–70% on the play, 42–50% on the draw).
- **A one-card swap that moves the whole win rate 5+ points overstates the card** (it was in hand in ~40%
  of games): the AI's imagined library differs too. Take the direction, not the size.
- **IWD (GIH − GNS) swings ±10–20 points a run at ~100 games**: Lore read +28 in one run and −7 in the next.
  Trust a card that is strongly positive in EVERY run (Will Riker: +26, +22, +18); confirm anything else
  with a paired swap. Cheap aggressive cards read low (long games draw more cards and aggro loses them).

## 5. The three ways it misleads — say so in every report

1. **The AI pilots both decks.** A simple deck (aggro) is played nearer its best than a complex one
   (Spacecraft, Sagas, assimilate, beam me up): a swap inside one deck compares fairly, a match ACROSS
   archetypes leans to the simpler deck.
2. **A bottom card may be the AI's misplay, not the card.** Before he cuts one, watch how it is played:
   Tal Shiar Analyst pinged in its own upkeep (AI-17.25); Borg Cube was never cast in four games
   (AI-19.22, `--card-games="Borg Cube"` then `--record=… --card=…` and `--explain=N`). Timing cards
   (instants, flash, free tap abilities) and complex permanents are the suspects.
3. **The AI's decks are a weak, simple field** (built on TRK's stand-in ratings, no curve, cards the AI
   plays well — DATA-2). Beating them says less than beating one of his lists does.

Record what a run decided in LIM-1's task for the pool and the numbers in `docs/findings/limited.md`;
ask him for his real record with each deck — it calibrates the sim (RB: ~60% in the sim, 6-3 on Arena).
