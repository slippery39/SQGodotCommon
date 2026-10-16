---
id: AI-5
title: The evaluator sees threats and abilities
status: backlog
area: AI
created: 2026-10-16
---

## Why

Shayne, 2026-10-16: is the evaluator just a before/after board sum, or does it weigh context — a 2/2
is far more threatening at 4 life than at 20, yet not much of one against four blockers — and does it
see what a creature's abilities do?

Today (`MagicCore/Ai/Evaluator.cs`) it is a static sum per side, ours minus theirs:

| Term | Value |
|---|---|
| a creature | 2 + 1.5 × power + toughness (after damage) + 0.8 per keyword of seven (flying, deathtouch, lifelink, first strike, double strike, trample, menace); pumps until end of turn excluded |
| life | ~1 per point to 20, 0.5 above; below 6 each point costs double; 0 is −50 |
| lands | 1.5 each to 7, then 0.4 |
| a planeswalker | 2 + 0.35 × loyalty |
| any other permanent | a flat 2 |
| hand | 1.2 per non-land, 0.3 per land |

So: **life never meets the board** — an opposing 2/2 costs the same at 4 life as at 20, and nothing
weighs their attackers against our blockers; **only seven keywords count** — vigilance, haste, reach,
hexproof, indestructible and ward are worth nothing; **activated and triggered abilities are worth
nothing in themselves** — a 1/1 with "{T}: deal 1 damage" scores as a vanilla 1/1.

The search's rollouts cover part of this (a lethal attack within two turns scores −10000; blocks are
judged by outcome), but: GreedyAi and the card-value sandbox have no look-ahead, so the evaluator IS
their judgment; the horizon is two turns; and the rollout's opponent (BoardOnly) never activates an
ability while our side makes one play a turn — so a pinger or a mana engine is invisible to the
search too, and the sandbox measures such a creature as about vanilla.

### A held spell is an option (Shayne, 2026-10-16)

From the MtgSimulator work, likened to options trading: **a spell in hand is valued by its
opportunity cost — you cash it in when the value of keeping it is less than the value of playing
it.** A good player holds a spell for many turns, re-weighing every turn the benefit of using it now
against waiting for a better opportunity. That is deeper than "look ahead a few turns and pick the
best target": the look-ahead prices the best use NOW, the option is priced by every use still to come.

What the option is worth depends on, at least:
- **what may still come** — better targets the opponent's deck can still present (the Rank Rat is
  not worth Prophesied End because a flier is coming);
- **time** — fewer turns left, less time value; near the end of a game, cash in;
- **risk of holding** — discard, the game ending first, falling too far behind to use it;
- **mana** — mana left unused this turn is free to spend, so casting at instant speed in their end
  step costs less than tapping out in our main phase;
- **flexibility** — a spell with several uses (modes, any target) is worth more held than one with a
  single use;
- **our own plan** — the same Lightning Bolt on their 2-power creature is RIGHT if we mean to tap out
  for our own spells for the next two turns and can't afford the tempo loss of holding it, and WRONG
  if next turn's creature will block that 2-power creature anyway (Shayne, 2026-10-16). The option's
  price depends on what we intend to do with our mana and board — AI-4's question;
- **the kind of game** — whether wasting mana loses the game: in a tempo game unused mana matters a
  lot, in a slower game hardly at all (Shayne, 2026-10-16). Unused mana has no fixed price.

AI-1.8's held value — max(flat, fraction × the card's measured value) — is a first, STATIC price for
this option: the same at turn 3 and turn 15, blind to what is left in their deck and to unused mana.
It stops premium removal going on a Rat; it cannot decide when a long hold should end. The search's
two-turn horizon can't either — a hold over many turns is exactly what it can't see.

Not written down in MtgSimulator's docs; this paragraph is its first record.

## Requirements

- [ ] **Threat:** a symmetric term for how soon each side's board can kill the other, counting the
      defender's possible blockers — a 2/2 at 4 life against no blockers weighs heavily, against four
      blockers little. MtgSimulator's `RacePressure` (20 / turns-to-kill, `.claude/rules/sim-ai.md`)
      is the starting point; it never counted blockers. Monotonic (MagicCore/CLAUDE.md).
- [ ] **Abilities:** seen through PLAY first — an opponent model that activates abilities (AI-3) and
      a wider budget for our own plays a turn — before any per-ability value; card-agnostic either way
- [ ] **Held spells as options:** a held card's value moves with what may still come, time left,
      the risk of holding, unused mana and flexibility — replacing or refining AI-1.8's static price;
      measured, including positions where holding for many turns is right and where cashing in is
- [ ] **Keywords:** the ones that matter and score nothing today (vigilance, haste, reach, hexproof,
      indestructible, ward) — measured, not guessed
- [ ] Every change measured with `AiMatch` (strength and `--sequential` time) and recorded in
      `docs/findings/magic-ai.md`; a regenerated card-value table where the sandbox's numbers move

## Decisions

- 2026-10-16, Shayne: create the story; it comes later — after AI-1 (held values) for now.

Open questions:
- Threat in the shared evaluator, or only at the search's leaves? MtgSimulator measured static terms
  as neutral once rollouts exist (four of four), and a hand term in the shared evaluator as a loss —
  where a term lives decides what it distorts.
- Which comes first: threat, abilities through play, held spells as options, or keywords?
- The option's inputs: which are measurable now (unused mana, turn number, hand size) and which need
  a model of the opponent's deck (what may still come — the same question as AI-3's "guessed hand")?
- Our plan and the kind of game shape the price (Bolt now vs later; does unused mana matter here?) —
  does this story wait for AI-4's plans, or can the search's own look-ahead stand in for "our plan"
  (it already plays our next turns) with the game's tempo read off the board and the race?

## Tasks

- [ ] AI-5.1 Settle the open questions with Shayne

## Next

Read `MagicCore/Ai/Evaluator.cs`, `.claude/rules/sim-ai.md` ("Race pressure", "Four evaluator changes
measured, four neutral"), and AI-3 — its opponent models are half of the abilities requirement.

## Log

- 2026-10-16: created from Shayne's question about what the evaluator weighs.
- 2026-10-16: Shayne added held spells as options (opportunity cost; the options-trading framing
  from the MtgSimulator work).
- 2026-10-16: Shayne confirmed the option's inputs and added two: our own plan (Bolt the 2-power
  creature now if we'll tap out for two turns; not if next turn's creature blocks it) and the kind of
  game (unused mana matters a lot in a tempo game, hardly at all in a slow one).
