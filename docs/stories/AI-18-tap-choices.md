---
id: AI-18
title: Tap choices without ballooning moves
status: in-progress
area: AI
created: 2026-11-05
---

## Why

TRK taps creatures for costs everywhere: Station ("tap another creature you control", one legal action
per creature), Worf's "tap two" (one per pair, in both orders), and since MC-5.3 batch 5b "tap three" and
"tap seven" (Collation Trimatrix, Captain Jean-Luc Picard: one per SET, every combination —
`MagicRules.TapSets`). Each is a branch for the AI's search, and they multiply with targets and modes:
Picard with 9 Officers is 36 moves, with 12 it is 792. Shayne, 2026-11-05: "There is a lot of tapping
creatures in this set (station as well), which has ballooning choice combinations. Lets pick the easiest
to implement to get started, but we might want to have a new story which looks into this and any
potential optimizations." The shortcut is in `DesignNotes.md` (MagicCore known ceilings).

## Requirements

- [ ] Measured first: how many moves tap costs add in real TRK games (self-play, the position suite),
      and what they cost the search per decision.
- [ ] Fewer moves for the same choice, the player still picking every creature on the table.
- [ ] The AI's pick no worse, measured by a match.

## Decisions

- 2026-11-07, Shayne (MC-5.6 batch 3b): **identify and group permanents that are exactly the same when a choice
  offers them** — "Ideally we would have a way to be able to identify and group permanents that are exactly the
  same together when selecting choices." Not only tap costs: Scotty's granted beam me up (a move per spell ×
  creature) too. The evidence: `RandomPlay_EveryScriptedCard(21)` is skipped until this lands — Troubling Tribbles
  doubling each upkeep, every step ~n³ in permanents (41 = 8 s a turn, 73 = 90 s); un-skip it as the check.

Open questions:
- Which way: creatures that don't differ (two untapped vanilla Officer tokens) as one choice; a cost
  picked one creature at a time after activating (a pending choice, as blockers are); or the AI
  offered only the sets a heuristic ranks best (the weakest, the summoning sick) while the table
  offers them all.

## Tasks

- [ ] AI-18.1 Measure the moves tap costs add, and their cost to the search, on TRK self-play
- [x] AI-18.2 The AI's moves group identical permanents (2026-11-09, found as AI-13.9's TRK cast-gains run hung:
      seed 43, Common Goal over 64 Troubling Tribbles = 14 910 casts, GreedyAi settling each): `MagicRules.AiMoves` -
      `DistinctMoves`, and past `TwinsFrom` (64) moves one per KIND of permanent (`Twins`: every field the same once a
      turn stamp is "this turn or not" and summoning sickness sick or not; never one with a status component, metadata
      or something attached). The search, GreedyAi, the rollout and cast-gains call it; `DistinctMoves` is unchanged -
      records index into it (`MoveTokens`), and grouping it first made `EveryKeptRecord_StillReplays` stale. The review:
      `MoveKey` left exactly as it was (a grouped move's key is the key of the move with its ids read as stand-ins,
      `AsTwins`); nothing is grouped while the stack holds anything (a spell's targets single a permanent out), nor a
      creature in combat, one with children, or one a card in exile is linked to; the twins cached per state. Left:
      `--compare` ranks a human's pick of a twin the search didn't keep as unranked (only past 64 moves). Seed 43's
      turn 27: 14 916 moves -> 287, the decision 1.6 s (was > 20 s). Test `AiMoves_GroupsPermanentsThatAreExactlyTheSame`
      (a damaged Bear stands alone). Suite unchanged by it. **Not fixed:** the engine's own list - `LegalActions` still
      enumerates every combination (780 ms at 79 permanents, ~n² per target pair), which `RandomPlay_EveryScriptedCard(21)`
      plays directly (still skipped), and the cast-gains run caps a game at 60 permanents (`CastGains.MaxPermanents`)

## Next

Read `MagicRules.TapSets` and `Taps` (MoveKey), `ActivateAbilityAction.TapIds`, `TapUntappedCost`
(`Effects/AdditionalCosts.cs`) and the table's `Targeting.Narrow`. Measure before choosing (AI-18.1).

## Log

- 2026-11-09: AI-18.2 — the AI's moves group identical permanents (`AiMoves`); the engine's list is still whole.

- 2026-11-05: created — Shayne, answering batch 5b's "how are the N chosen".
- 2026-11-07: Shayne chose grouping identical permanents (MC-5.6 batch 3b's seed 21, skipped until then).
  Lifted meanwhile: `ManaAbilities.Grants`, the "grants a mana ability" sources listed once per state — the
  scan per source had made `ManaPayment.Find` quadratic in permanents.
