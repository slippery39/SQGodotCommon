---
id: AI-3
title: Swappable simulation models in the search, measured for strength against time
status: in-progress
area: AI
created: 2026-10-16
---

## Why

What the search imagines the opponent doing decides what it sees. MtgCore's default, `BoardOnly`,
never casts from hand — so a bigger threat never arrives inside a rollout, and holding removal for
one never pays (AI-1's Rank Rat). A richer model sees more and costs more, and the one that sees
the opponent's real hand cheats. Which is best is a measurement, not a guess.

## Requirements

- [ ] Three opponent models behind AI-2's seam (`Rollout.OpponentStep`), chosen by a setting:
  - `BoardOnly` — attacks and blocks with its board, never casts (ships in AI-2)
  - casts from hand — plays a land and threats in its own turn, by the same cheap policy as ours
    (`Rollout.OurPlay`), so the model is symmetric — MtgSimulator's lesson: a rollout that gives the
    opponent every attack and us one spell measures the asymmetry (`.claude/rules/sim-ai.md`)
  - full responses — also responds at instant speed (counters, tricks, removal) inside our turn
- [ ] **Which hand the opponent casts from is its own setting**, swappable and measured (2026-10-17):
  - real — the cards it really holds (cheap; cheats, like the real library order)
  - shuffled from its own unseen cards — its hand cards we don't know, sampled from the pool of its
    hand plus the unknown part of its library (a determinization, taken once per decision at the
    search root, from the state's seeded RNG); its own deck list is the guess
  - guessed from a deck model — sampled from what its deck COULD hold, never from its real cards
    (honest even in limited, where the opponent's pool is not known)
- [ ] Each measured: head-to-head against the others and against `GreedyAi`, AND time per decision —
      recorded side by side in `docs/findings/magic-ai.md`
- [ ] Our own side's rollout policy is swappable through the same seam (cheap greedy vs `GreedyAi`)
- [ ] Hidden information in the library has a second mode beside AI-2's "real order": **shuffle only
      what isn't known**. Needs the engine to record which library positions a player knows — surveil
      keeps, "put on the top / bottom" (Loyal Tutor, Paradox Shaper, Identity Echo's bottom), mulligan
      bottoms — cleared when the library is shuffled; and which hand cards the other player knows
      (revealed, or returned from a public zone), for the hand modes above
- [ ] Every combination worth running is measured the same way: strength AND time per decision
- [ ] The rollout's combat: each attacker is judged alone and a trigger's target is the first legal
      one (DesignNotes ceilings). On a full board this trades both sides away — it made the card-value
      sandbox measure combat instead of the card (findings, AI-1.6) and it shapes the search's
      scores too. Measure a better combat policy here, like the other models

## Decisions

- 2026-10-16, Shayne: don't commit to one model — be able to try all three and measure play
  ability against simulation time.
- 2026-10-16, Shayne: hidden information is the same kind of question — two methods, swappable and
  measured. A pure library shuffle is wrong, because some positions are known.
- 2026-10-17, Shayne: **"casts from hand" — real or guessed hand? Both, and more: multiple modes,
  swappable, monitored and tested** — real, shuffled from its own unseen cards, guessed from a deck
  model. The hand is a setting of its own, apart from the library's mode, so each is measured alone.
- 2026-10-17, Shayne: **opponent models first, combat last** — combat is a confound, re-measured in
  the final matrix.
- 2026-10-17 (default, not asked): the casting opponent uses OUR cheap policy, not one of its own —
  symmetric, so a measurement isolates the model. "Our policy" (AI-3.7) swaps our side only.

## Tasks

Each new value gets a `--match` name so it can be measured alone (AI-2's rule: every behaviour
change has a switch that turns it off). One setting per axis, added with its second value — never
an enum of one (AI-2.4's review removed two).

- [x] AI-3.1 Opponent casts from hand (its real hand): the opponent-model setting, `OpponentStep`
      running `OurPlay` for the opponent on its turn; measured against `beam` and `greedy`, time beside.
      84–72 head-to-head (1.3 SE, not shown stronger), +41% time; `BoardOnly` stays the default
- [ ] AI-3.2 Engine: who knows what — library positions a player knows, and hand cards the other
      player knows; set by every FRA source, cleared on shuffle. A test per source (an inert
      primitive throws no error — test the consequence)
- [ ] AI-3.3 Library: "shuffle what isn't known" — both libraries, at the decision's root; measured
      with `BoardOnly` and casts-from-hand
- [ ] AI-3.4 Hand: shuffled from its own unseen cards — the hand setting's second value; measured
- [ ] AI-3.5 Hand: guessed from a deck model — the third value; measured
- [ ] AI-3.6 Full responses: the opponent acts at instant speed inside our turn when it scores
      better; measured — expect it to be the dearest in time
- [ ] AI-3.7 Our policy: `GreedyAi` as our rollout policy beside the cheap greedy; measured
- [ ] AI-3.8 Combat: attackers judged together (MtgSimulator's `FindLethalAttacks`, or all-in
      against each alone) and a trigger's target by its owner's answer; measured
- [ ] AI-3.9 The matrix: every combination worth running, side by side in findings; pick the default

## Next

**AI-3.1 done; next is AI-3.2** (the engine records who knows what). Read first:
`MagicCore/Ai/Rollout.cs` (`OpponentStep` is the ONE place the opponent is decided — the beam's tree
goes through it too; `Settings.Opponent` picks the model), `BeamSearchAi.cs` (Settings,
`DefaultRollout`), `AiMatch.Makers` (the `--match` names; `beam-casts` is AI-3.1), and in
`docs/findings/magic-ai.md` the AI-2.8 baseline (111–45, 239 ms) and AI-3.1's runs.

Traps:
- Only `--sequential` timings are evidence; never time a search-vs-search run in parallel.
- A 156-game sequential run (26 per ordered pairing) is ~40 min at the current speed — a casting
  opponent will be slower.
- A sample of hidden cards must come from a COPY of the state's RNG (`TakeRng` advances the seed),
  or searching changes the real game's draws and breaks determinism.
- The tree's own opponent decisions and the rollouts must see the SAME sampled state: sample once at
  the root, before the tree, not inside `Rollout.Score`.
- The evaluator already prices the opponent's hand blind (`HeldValues.Unknown`); a hand mode changes
  what the opponent DOES with its hand, not how the hand is scored.

## Log

- 2026-10-16: split out of AI-2 so the port stays bounded.
- 2026-10-16: widened from opponent models to every simulation model — our rollout policy and hidden information too.
- 2026-10-17: shaped — the open question answered (the hand is its own setting with three modes),
  the order agreed (opponent models first, combat last), tasks AI-3.1…3.9. Status `ready`.
- 2026-10-17: AI-3.1 — `Rollout.OpponentModel` (`BoardOnly` default, `CastsFromHand`); the casting
  opponent plays its land, up to `ActionsPerTurn` spells and its loyalty abilities by `OurPlay`,
  counted from the turn's tally because `OpponentStep` is stateless (the tree calls it). Other
  activated abilities are a `ponytail:` (no tally counts them, a free one would repeat).
  `--match=beam-casts`. Tests: the hand's card arrives in a rollout (against the same position
  without it); its turn casts ONE of two castable spells and uses its planeswalker; `BoardOnly` does
  neither — each revert-checked. /pre-commit fixes: loyalty abilities were left out (Jace's plan
  invisible), the test passed on drawn cards alone, "symmetric" was overstated, a move list was built
  just to find no land. Measured after the fixes (findings): **123–33 vs GreedyAi (78.8% ± 2.8pp,
  276 ms), 84–72 vs beam (53.8% ± 3.0pp), 321 ms against 228** — not shown stronger, 41% dearer;
  not the default. Status `in-progress`.
