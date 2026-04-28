# Preconstructed Deck Simulation — Feature Spec

## Overview

Add a second simulation mode alongside the existing random card pool mode. In preconstructed mode, named decks play each other in a round-robin tournament. Each unique matchup pair plays `2N` games — `N` with Deck A on the play, `N` with Deck B on the play — so play/draw advantage is always balanced per matchup.

**User inputs N** at startup. Total games = `C(decks, 2) × 2N`.

Example: 2 decks, N=5 → 10 games. 3 decks, N=5 → 30 games.

---

## Mode Selection

`Program.cs` gains a mode prompt at startup:
```
Select mode:
  1 - Random Card Pool
  2 - Preconstructed Decks
```

Each mode then prompts for its own parameters (game count for random; N for precon).

---

## Round-Robin Scheduling

For all unique pairs `(A, B)` where `A < B` (by index):
- Run N games: Player 1 = A (on play), Player 2 = B (on draw)
- Run N games: Player 1 = B (on play), Player 2 = A (on draw)

"On play" = Player 1 (goes first). This mirrors the existing random mode convention.

---

## Card Win Rate: Game In Hand Win Rate (GIH WR)

For each card, **per game**:
- Did the player draw ≥ 1 copy this game? → **GIH game** (binary, not per-copy)
- Did the player win that game? → **GIH win**

`GIH WR = GIH wins / GIH games`

Also track **avg copies drawn per GIH game** (total copies drawn across GIH games / GIH game count). This surfaces "flooding on 4-ofs" vs. "always draw exactly 1."

Drawing 2 copies of Lightning Bolt in the same game does **not** double-count the win — it remains 1 GIH game.

---

## Stats to Collect

### Overall Per-Deck
| Column | Description |
|--------|-------------|
| Deck | Deck name |
| Games | Total games played |
| Wins | Total wins |
| Win% | Overall win rate |
| OnPlay Games | Games where deck was on the play |
| OnPlay Win% | Win rate on the play |
| OnDraw Games | Games where deck was on the draw |
| OnDraw Win% | Win rate on the draw |

### Per-Matchup
| Column | Description |
|--------|-------------|
| Deck | Deck name |
| Opponent | Opponent deck name |
| Games | Total games in this matchup |
| Wins | Wins in this matchup |
| Win% | Matchup win rate |
| OnPlay Win% | Win rate when on the play vs this opponent |
| OnDraw Win% | Win rate when on the draw vs this opponent |

### Card Win Rates — Overall (per deck)
| Column | Description |
|--------|-------------|
| Deck | Deck name |
| Card | Card name |
| GIH Games | Games where ≥1 copy was drawn |
| GIH Wins | Wins in those games |
| GIH WR% | GIH win rate |
| Avg Copies | Avg copies drawn per GIH game |

### Card Win Rates — Per Matchup (per deck × opponent)
Same columns as above, plus an **Opponent** column. Allows spotting cards that over/underperform in specific matchups.

---

## Output

**Console**: Summary table of overall per-deck stats and per-matchup win rates. Mirrors the current `PrintAggregateReport()` style.

**CSV** (written to `sim_results/` directory, filename `precon_<timestamp>.csv`):
- One CSV file with multiple labelled sections (blank-line separated headers), covering all four stat tables above.

---

## New Files

| File | Responsibility |
|------|----------------|
| `DeckRegistry.cs` | Maps deck name → builder `Func<int, List<Card>>`. Owns the list of all registered decks. |
| `PreconstructedGameResult.cs` | Wraps `GameResult` + `Player1DeckName`, `Player2DeckName`, `Player1IsOnPlay`. |
| `PreconstructedStats.cs` | Accepts `PreconstructedGameResult` instances, aggregates all four stat tables. |
| `PreconstructedSimulatorRunner.cs` | Generates round-robin schedule, runs games via `GameRunner`, feeds results to `PreconstructedStats`, prints console summary, triggers CSV export. |
| `PreconstructedCsvExporter.cs` | Writes the four stat tables to a single CSV file in `sim_results/`. |

## Modified Files

| File | Change |
|------|--------|
| `Program.cs` | Add mode selection prompt; branch to `SimulatorRunner` or `PreconstructedSimulatorRunner`. |

---

## Task List (one commit per task)

1. **`PreconstructedGameResult` record** — minimal wrapper around `GameResult` with deck name and on-play metadata.
2. **`DeckRegistry`** — static registry; registers Zoo and Goblins; exposes `All` (list of deck infos) and `Build(name, ownerId)`.
3. **`PreconstructedStats`** — aggregation class; accepts game results and exposes all four stat tables as queryable data.
4. **`PreconstructedSimulatorRunner`** — round-robin schedule generator + game execution loop + console summary output.
5. **`PreconstructedCsvExporter`** — writes all four stat tables to `sim_results/precon_<timestamp>.csv`.
6. **`Program.cs` mode selection** — startup prompt wires mode 1 → existing runner, mode 2 → new runner.

---

## Out of Scope

- Deck balance / realistic mana simulation (auto-mana system used as-is)
- Different AI strategies per deck
- Head-to-head replays or saved game states (existing `FlaggedGameSaver` unchanged)
- Lands or mana base modeling
