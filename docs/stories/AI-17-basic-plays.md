---
id: AI-17
title: The AI misses basic plays — two found at the table, then a full review of the AI
status: in-progress
area: AI
created: 2026-10-29
---

## Why

Shayne's draft games on the table (LIM-2.3, 2026-10-29) showed two blatant misses in one afternoon:

1. **Way of the Deathbringer** (game seed 535739, turn 11, move #262): the AI made a Jace and drew a card
   (−3) where sacrificing Blessed Ghoul for a 4/4 trample Beast (−2) was its best play — "a 4/4 would
   (theoretically have stopped all my creatures)".
2. **A lethal attack not made** (game seed 636312, turn 21, move #494): Shayne at 3 life, one blocker; the
   AI attacked with Chandra's Emberling alone and left Tether Technician home — both attacking was lethal.

Shayne: "This is actually a pretty big deal. I'm wondering what other things are not being properly
accounted for … in MTGCore I don't believe we had such blatant misses … I think we actually need to do a
full review of the AI code to make sure this doesn't happen." Neither miss shows in the 288-position
suite or in any match: a match measures win rate, and both games were won or lost anyway.

## Diagnosis (2026-10-29, Claude, on the kept positions)

**1. Every "you may" is declined inside the look-ahead.** `Lookahead.QuickAnswer` answers a choice with
its minimum — `choice.Options.Take(choice.MinChoices)` — and every optional choice has `MinChoices = 0`:
`YesNoChoice` ("you may …"), "you may sacrifice" (`ChoosePermanentChoice`), "you may discard", a library
search (`ChooseFromLibraryChoice`: finds nothing), `ChooseAmongChoice`. At #262 the −2's sacrifice,
declined, scored −48.5; sacrificing the Ghoul −20.1, the best option. The real decision searches every
answer — but the move was never searched: at #257 the AI cast Way of the Deathbringer and COMMITTED A PLAN
for the turn, judged by that blind look-ahead (the −2 the worst option, so −3), and replayed it at #262
("searched 0"). Fresh at #262 the search takes −2 (−20.5 against −3's −26.8), capped at 1 s or not.

**2. The search stops at the first won line, however slow.** `BeamSearchAi.Winner` returns any node at
or above `WinThreshold` (2 500) — and a win two turns away (10 000 × 0.95² ≈ 9 097) is one. At #494 both
root lines ("Technician attacks", "doesn't") won by turn 23, so the search stopped after its root batch
(2 rollouts) and the tie took the first option, "don't attack". One level deeper, the Emberling's
declaration with both attacking wins THIS turn (10 004) — searched from there, the AI finds it. Why the
root lines tied: the look-ahead judges each attacker ALONE against a defender that never chump-blocks
(`Lookahead.Settle` / `QuickAnswer`, AI-3.8's known ceiling), so the Technician alone looked lethal and the
Emberling's attack added nothing; then the rollout's real defender chumped the Technician.

**What MtgCore's AI had that this one doesn't:** MtgSimulator's search stops at its first win too
(`FindWinner`), but a **lethal pass runs before the search** (`MultiTurnBeamSearchAiStrategy.
FindLethalAttacks`: total power ≥ their life → attack with everything, verified by simulation). It was
simple there — MtgCore has no blocking — and was not ported with the search (AI-2).

## Requirements

- [ ] The two positions pinned as tests, each with its control (`Records/DeathbringerDrawsNotABeast.json`
      #262 — decided from #257, as the table plays; `Records/TechnicianSkipsLethal.json` #494)
- [ ] A "basics" suite: hand-built positions for plays any player makes, each with Shayne's answer —
      the review's second half, so a miss is a failing test, not a table surprise
- [ ] Every fix card-agnostic, behind a switch, measured the AI-6 way (suite, then `--until-clear`)
- [ ] The review written up: every shortcut in the decision path, whether it can cause a miss, and what
      was done about it (`docs/findings/magic-ai.md`)

## Decisions

- 2026-10-29, Shayne: plan it, don't fix it yet; and review the whole AI.
- 2026-10-29, Shayne (on the plan): yes — and **"we need to view an attack as one concrete thing … we
  aren't choosing to attack with creatures individually in isolation, but rather we are launching an
  attack as one decision"**. Asked why the AI doesn't chump when facing lethal ("is it a look ahead
  issue?"). Claude's answer: yes — the look-ahead has two defenders. The cheap one
  (`Lookahead.QuickAnswer`, inside every `Settle`) blocks only what it survives or joins a killing block
  (AI-15) and never asks whether it dies unblocked; the rollout's (`Rollout.Answer` → `BestAlone`)
  scores each blocker's options and does chump. An attacker is judged against the cheap one, so a lone
  attacker looks lethal and a second adds nothing — then the rollout's defender chumps. AI-3.8 measured
  the same mismatch as "all-in attacks WORSE" (`docs/findings/magic-ai.md`); never fixed.

- 2026-10-29, Shayne: **the two fixes first, then the review**; and **AI-17 to the top of sprint 3** —
  MC-4 and AI-13.6 slip.

- 2026-10-29, Shayne (how a change is judged): **win rate is not everything — the AI should make good
  decisions**, in games it wins and games it loses. "We may be dealing with a scenario where the AI is
  making a bunch of different types of misplays, each when fixed may only technically change the outcome
  of 1% of games" (missing lethal still won, a turn later). But **"we should see our win rate slowly
  increasing over time against a more basic algorithm as we include more and more of these fixes"** — and
  since even top players top out near 70%, "a percentage point or two increase actually is a pretty big
  deal (accounting for noise)"; +5pp changes can't go on forever. Agreed: **blunder counters** (Claude's
  proposal) — over many AI games, each decision checked for a named mistake, counted whatever the result.

The plan (proposed by Claude, agreed by Shayne 2026-10-29):
- **Fix 1 (may):** inside the look-ahead, a single optional choice (min 0, max 1) is answered with the
  chooser's best — each option settled and evaluated, as `GreedyAi.Answer` already answers a target
  (`Rollout.Answer`, which both sides' policies use). Larger optional choices (surveil's "any number")
  stay as they are until the review says otherwise.
- **Fix 2 (lethal), revised after Shayne's "an attack as one decision":**
  (a) **the cheap defender chumps when facing lethal** — if the damage left unblocked would kill it, it
      blocks with its least valuable creature (`QuickAnswer`; every simulation gains it);
  (b) **the attack is chosen as a whole** at the first declaration — a few candidate attacks (none, all,
      each alone, all but one …, about n + 3, not every subset: 6 creatures would be 64 rollouts a
      decision), each played against that defender — then each per-creature question the engine asks is
      answered from it, as `MagicTableManager.DeclareCombat` answers them for a human. "All attack" is
      MtgSimulator's lethal pass, found at the first step;
  (c) `Winner` stops early only on a win inside the search's own turn; a later win keeps the search going
      to its depth, and the fastest is taken (wins of several steps — two burn spells — need it too).

## Tasks

- [x] AI-17.1 Shayne agrees the plan (Decisions, 2026-10-29): fixes first, the attack as one decision
- [x] AI-17.2 Fix 1, "you may" in the look-ahead: the position test and control, the suite, a match —
      **built (2026-10-29):** `Rollout.Settings.Mays` (`beam+nomays` is off): `Rollout.Answer` answers a
      choice of none or one (`MinChoices` 0, `MaxChoices` 1) with `GreedyAi.Answer` — none and each option
      settled, the chooser's best — for both sides' policies; `Lookahead.QuickAnswer` (inside `Settle`)
      still declines. Position: `AYouMay_IsTakenInTheLookAhead_TheDeathbringersBeast` — one AI from #257
      as the table plays it: −2 (the Beast); its control draws. Suite: 7 of 288 decisions change, cost
      3 103 steps a position (3 097, +0.2%); oracle 81.9% against 82.6% (read with the old look-ahead).
      MagicCore.Tests 1 145 pass. Match: **51.0% ± 1.3pp over 192 games (98–94), no difference, worse
      rejected**, +0.2% cost, 524 s — kept. Suite accepted; the oracle re-read, 85.4%
      (`docs/findings/magic-ai.md`). The review: a pay-to-keep (a `YesNoChoice` too) keeps the cheap
      answer — it pays; an optional target takes its chooser's best too; the test checks #257 casts as
      recorded, so its plan follows the record (suite unchanged after: the match stands). **Left for the
      review (AI-17.4):** `Lookahead.Settle` — which judges every play the rollout policies weigh (our
      `OurPlay`, their `TheirLoyalty`, `BestAlone`) — still declines a "you may", so the simulated
      opponent's own Deathbringer −2 still reads as "−2 for nothing"; and a library search now settles
      every card it offers
- [ ] AI-17.3 Fix 2, lethal: (a) the cheap defender chumps facing lethal, (b) the attack as one decision,
      (c) the early stop on a win this turn only — the position test and control, the suite, a match
      (each part measured alone where it can be)
- [ ] AI-17.4 The review, code: read the decision path — `BeamSearchAi` (Search, Narrow, Prune, Winner,
      Best, plans and their replay), `Rollout` (Score, OurPlay, OpponentStep, Answer, BestAlone),
      `Lookahead` (Settle, QuickAnswer, Joins), `Evaluator`, `Hidden`, `GreedyAi` — about 2 000 lines;
      every shortcut listed (the `ponytail:` comments, DesignNotes' "MagicCore known ceilings", AI-3's
      models not taken) and each judged: can it make the AI miss an obvious play? Compared against
      MtgSimulator's AI for anything it does that this one doesn't
- [ ] AI-17.5 The review, behaviour: the basics suite — hand-built positions with Shayne's answers (lethal
      on board: with blockers, menace, flying, trample, a pump; a chump when facing lethal; a "may" for
      value; a search that finds its card; removal on the biggest threat; a planeswalker's best ability;
      no attack into a losing trade; …), the AI run on each; every failure a task here
- [ ] AI-17.6 What the review finds, fixed in order of how often it bites — each measured
- [ ] AI-17.7 Blunder counters (Shayne, 2026-10-29): over N AI games, each decision checked for a named
      mistake, counted whatever the game's result — **missed lethal** first (an attack that kills through
      the defender's best blocks, not made; AI-17.3 should drive it to 0), then walked into lethal,
      self-harm (damage at its own face or creatures for nothing — AI-16's self-burn), a card thrown away
      (cast for no effect — AI-16.4's X = 0 Anguish); each checker's exact rule agreed with Shayne first
- [ ] AI-17.8 The long view (Shayne: win rate "slowly increasing over time against a more basic
      algorithm"): a fixed anchor and a long match each sprint, sized to see 1–2pp, the trend kept in
      `docs/findings/magic-ai.md` — **agreed (Shayne, 2026-10-29): the anchor is sprint 3's starting AI**,
      rebuilt from the switches (`beam+nogang+lifeprogress+nomays`, and each later fix's switch added —
      so those switches stay; freeze a build if that gets unwieldy); **~1 500 games (±0.45pp, ~1–1.5 h)
      near each sprint's end**, asked before it runs. Not GreedyAi: the search already beats it ~75%, past
      the 70% a top player reaches, and it moves with every evaluator and look-ahead fix

## Next

**AI-17.3** (lethal: the cheap defender chumps facing lethal, the attack as one decision, the early stop
on a win this turn) — its position `Records/TechnicianSkipsLethal.json` #494 (kept, no test yet). Read this story's Diagnosis; the two kept records; then
`BeamSearchAi.Search` / `Winner`, `Rollout.Answer`, `Lookahead.QuickAnswer`, and MtgSimulator's
`MultiTurnBeamSearchAiStrategy.FindLethalAttacks`.

## Log

- 2026-10-29: created from Shayne's two table games (LIM-2.3's playtest); both diagnosed on kept records.
- 2026-10-29: the plan agreed — the attack as one decision (Shayne); to the top of sprint 3.
- 2026-10-29: AI-17.2 — "you may" taken in the look-ahead; 51.0% over 192 games, kept.
