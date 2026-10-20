---
id: AI-5
title: The evaluator sees threats and abilities
status: done
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

- [x] **Threat:** a symmetric term for how soon each side's board can kill the other, counting the
      defender's possible blockers — a 2/2 at 4 life against no blockers weighs heavily, against four
      blockers little. MtgSimulator's `RacePressure` (20 / turns-to-kill, `.claude/rules/sim-ai.md`)
      is the starting point; it never counted blockers. Monotonic (MagicCore/CLAUDE.md).
- [ ] ~~**Abilities:** seen through PLAY first~~ — deferred (Shayne, 2026-10-20): AI-3 measured richer
      opponent models and GreedyAi rollouts at under 5pp; DesignNotes keeps them. Was: seen through PLAY first — an opponent model that activates abilities (AI-3) and
      a wider budget for our own plays a turn — before any per-ability value; card-agnostic either way
- [ ] ~~**Held spells as options:**~~ — deferred (Shayne, 2026-10-20) to a later story, with AI-4's
      plans. Was: a held card's value moves with what may still come, time left,
      the risk of holding, unused mana and flexibility — replacing or refining AI-1.8's static price;
      measured, including positions where holding for many turns is right and where cashing in is
- [x] **Keywords:** the ones that matter and score nothing today (vigilance, haste, reach, hexproof,
      indestructible, ward) — measured, not guessed
- [x] Every change measured — the suite, then `--until-clear` against `beam`, and its cost (AI-6's
      count) — and recorded in `docs/findings/magic-ai.md`; a regenerated card-value table where the
      sandbox's numbers move. **Kept if better, or if neutral (no difference, not worse) AND it fixes
      a recorded blunder position, with a test of that position** (Shayne, 2026-10-20)

## Decisions

- 2026-10-16, Shayne: create the story; it comes later — after AI-1 (held values) for now.
- 2026-10-20, Shayne (AI-5.1): **this story does threat and keywords**, before Shayne plays against
  the AI (TBL-2 next); held spells as options and abilities through play are deferred. **Threat lives
  in the shared evaluator** (GreedyAi has no look-ahead — the evaluator is its judgment; the search's
  leaves and the card-value sandbox see it too; the table regenerated). **The keep rule:** a change
  is kept if it measures better, or if it measures no difference and fixes a recorded blunder
  position (with a test) — as MtgSimulator kept its race term.

- 2026-10-20, Shayne (AI-5.2, after review): **the card-value sandbox measures WITHOUT the threat
  term** — a card's worth, not a position's clock: the fixture's creature ladder gave almost any card
  a flat +0.5 for the attacker it leaves unblocked. GreedyAi and the search keep it. (Revises the
  AI-5.1 line that the sandbox sees it.)
- 2026-10-20 (default, not asked): the threat term at **half weight** — at a healthy life a forecast
  must not outweigh real damage (a free attack for 2 at 29 life tied with the 1 power it leaves
  unblocked at full weight, and a tie takes the passive move); near death it still dominates.

- 2026-10-20, Shayne (AI-5.3): **the six keywords at the weight the other seven get (0.8), measured
  by a match** and kept by the rule — the sandbox cannot value a keyword (it scores the board with the
  evaluator, so it would echo the weight back). Default, not asked: the sandbox measures without
  them, as without threat, so the card table and the A/B stay clean.

Settled by the above (2026-10-20) — the questions as they were:
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

- [x] AI-5.1 Settle the open questions with Shayne — threat and keywords; threat in the shared
      evaluator; the keep rule
- [x] AI-5.2 Threat: a symmetric race term counting the defender's blockers; a blunder position it fixes —
      kept: 312–288 at the 600 cap (worse ruled out, ~+2pp), +3.7% cost; GreedyAi's swing-back blunder fixed
- [x] AI-5.3 Keywords: vigilance, haste, reach, hexproof, indestructible, ward — measured by match (the
      sandbox can't value a keyword): NO difference at 192 games, 95–97; GreedyAi's haste blunder fixed

## Next

**Done (2026-10-20).** Threat (a clock that counts blockers, half weight) and six more keywords are in
the shared evaluator; the card-value sandbox measures without both. Both kept by Shayne's rule —
threat leaned +2pp (worse ruled out at 600 games), keywords no difference — each fixing a recorded
GreedyAi blunder. `+nothreat`, `+nokeywords` measure the search without them. Deferred: held spells
as options (with AI-4's plans) and abilities through play (DesignNotes). Next: Shayne plays (TBL-2).

## Log

- 2026-10-16: created from Shayne's question about what the evaluator weighs.
- 2026-10-16: Shayne added held spells as options (opportunity cost; the options-trading framing
  from the MtgSimulator work).
- 2026-10-16: Shayne confirmed the option's inputs and added two: our own plan (Bolt the 2-power
  creature now if we'll tap out for two turns; not if next turn's creature blocks it) and the kind of
  game (unused mana matters a lot in a tempo game, hardly at all in a slow one).
- 2026-10-20: AI-5.1 settled with Shayne (Decisions) — threat and keywords now; the rest deferred.
- 2026-10-20: AI-5.2 — the threat term (`Evaluator.Pressure`, half weight, no cap): 20 ÷ turns to kill
  for each side, counting the power that gets past the defender's blockers in the next attack
  (lasting power only, the dying left out, Combat's rules for who attacks and blocks, a tapped
  blocker counted only if it untaps first). The recorded blunder: GreedyAi at 3 life went face with
  its only blocker into a lethal swing-back — the search did not (its rollouts see it); test
  `StaysHome_WhenAttackingLeavesItDeadToTheSwingBack` (fails without the term). Review: pumps until
  end of turn and dying creatures counted, defender and unblockable ignored, a cap that broke the
  monotonic rule, and the sandbox measuring the fixture — all fixed (Decisions); the first match was
  stopped. `+nothreat` is the search without it (`Evaluator.Without(Terms.Threat, …)` since
  AI-5.3, flowing like the step count). The card table regenerates unchanged; GreedyAi's 12
  recorded games regenerated (all moved). Tests: no threat from a pump, none from a defender —
  revert-checked. 1 006 tests.
  Measured (findings): UNDECIDED at the 600 cap, 312–288 (52.0% ± 1.4pp; worse ruled out), +3.7% —
  kept by the rule (no loss, a recorded blunder fixed).
- 2026-10-20: AI-5.3 — vigilance, haste, reach, hexproof, indestructible and ward join a creature's
  valued keywords at 0.8 (`Evaluator.ValuedKeywords`); haste only while the creature is new; a
  printed keyword counts even when a pump grants it again. The evaluator's switchable terms are one
  `[Flags] Evaluator.Terms` (`Threat`, `MoreKeywords`) — `+nothreat`, `+nokeywords`; the sandbox
  measures without both, so the card table stays unchanged. The recorded blunder: of two equal
  2/2s and the mana for one, GreedyAi cast whichever came first — now the one with haste (test,
  fails without the six). Review: haste valued forever, a pump hiding a printed keyword, the switch
  read per creature — fixed; tests that the switch turns the six off and haste counts only while
  new (revert-checked). GreedyAi's recorded games regenerated (5 moved). 1 009 tests.
  Measured (findings): NO difference of 5pp at 192 games (95–97), +0.2% cost — kept by the rule.
  The suite's committed answers moved to the current search (29 of 288). AI-5 done.

