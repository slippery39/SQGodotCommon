# MagicCore AI — measured results

Numbers from runs. The rules that come from them live in `MagicCore/CLAUDE.md`.

## GreedyAi vs random play (2026-10-07, Reality Fracture, 25 seeds per row)

| AI deck | random deck | before | life uncapped | + lands tapered, no floating-mana credit |
|---|---|---|---|---|
| White-Black | Red-Green | 25/25 | 25/25 | 25/25 |
| White-Black | White-Blue Jace | — | 25/25 | 25/25 |
| Red-Green | White-Black | 19/25 | 21/25 | 22/25 |
| Red-Green | White-Blue Jace | 25/25 | 25/25 | 25/25 |
| White-Blue Jace | White-Black | — | 24/25 | 22/25 |
| White-Blue Jace | Red-Green | 25/25 | 25/25 | 24/25 |

**Read it as noise past the first fix.** At 25 games a row moves ±2 by chance; 145/150 → 143/150
overall is not a change. Against RANDOM play these are correctness fixes, each pinned by a unit
test that fails without it — not strength gains. Strength needs an opponent better than random.

### The life cap (fixed)

Red-Green lost to RANDOM White-Black because the evaluator capped life at 20. Screeching
Soulbreakers' drains pushed the opponent to 26–31, so damage scored zero: in seed 8, attacking with
every Red-Green creature scored exactly the same as not attacking (−4.8 vs −4.8 on turn 21) while all
three Soulbreakers were TAPPED from attacking — an open board. Ties go to "don't attack", so the AI
never raced. Tapping was verified correct (`CombatTests.AnAttacker_StaysTapped…`). Pinned by
`GreedyAiTests.AttacksPastTappedBlockers_EvenWhenTheOpponentIsAboveTwenty`.

**Rule from it: an evaluation term must be monotonic.** A cap turns "more of a good thing" into a
tie, and a tie silently picks the passive move.

### The same bug twice more (fixed)

The review of the life fix found two more non-monotonic terms in the same evaluator, both misread
in the first draft of this file as "one-ply habits":
- **Lands capped at 7** — an 8th land scored less in play than in hand, so the AI hoarded lands
  (the "ends games holding 3–4 lands" habit). Pinned by `GreedyAiTests.PlaysASurplusLand`.
- **Floating mana scored +0.1** — but it empties at the end of the step, which the simulation never
  reaches, so "Add {R}" with nothing to cast looked like a gain.

### What is left (not fixed — one-ply ceilings)

The four remaining Red-Green losses (seeds 1, 3, 10, 14) are long attrition games: White-Black's
fliers and Thalia's lifelink against a deck with few answers to fliers. The AI does attack now.
What remains after the fixes, each needing lookahead rather than a weight: it plays a tapped dual and
an untapped basic as equals (no "do I need the mana this turn"), and it cannot plan around fliers it
has no answer to.

`GreedyAiTests.BeatsRandomPlay_InNearlyEveryGame` asserts 90% overall AND at least 6 of 10 in every
pairing, so a collapse in one matchup cannot hide in the average.
