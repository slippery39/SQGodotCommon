# MagicCore AI — measured results

Numbers from runs. The rules that come from them live in `MagicCore/CLAUDE.md`.

## GreedyAi vs random play (2026-10-07, Reality Fracture, 25 seeds per row)

| AI deck | random deck | AI wins |
|---|---|---|
| White-Black | Red-Green | 25/25 |
| Red-Green | White-Black | 19/25 (21/25 with all-basic lands) |
| White-Blue Jace | Red-Green | 25/25 |
| Red-Green | White-Blue Jace | 25/25 |

**Why Red-Green loses to RANDOM play**, read from seed 8's log, not reasoned: random White-Black
landed three Screeching Soulbreakers (1/4 flying, "whenever this attacks, 1 damage to each opponent
and you gain 1 life"). Red-Green had no flier or reach in play, and the AI would not attack its
2/2s into the untapped 1/4s — every one-ply simulation scored that attack as a loss of a creature.
It sat behind its ground army and was drained out over ~10 turns.

That is the one-ply ceiling, not a scoring bug: no single move fixes a race lost slowly, so a
better weight would not either. It needs lookahead — the search AI. The dual lands cost about two
games of the six (tapped lands slow the race), within noise at 25.

`GreedyAiTests.BeatsRandomPlay_InNearlyEveryGame` asserts 85% over every pairing for this reason.
