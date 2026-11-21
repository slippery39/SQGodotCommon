---
id: AI-30
title: Sealed runs in 20 minutes — a pool's question answered while Shayne waits, not in 3 hours
status: ready
area: AI
created: 2026-11-18
---

## Why

Shayne, sprint 5 review (2026-11-18): "the simulations too are still just way too long. We really need to try to find
ways to optimize, either from a direct engine level (which would obviously take modifications to ImmutableGameObjects),
or from AI optmizations. But I would say that is a huge bottleneck that will be a hinderance to many things I want to
do. Like it would be nice if I only had to wait 10-20 minutes for a sealed simulation run results, not 3 hours."

Where it stands (2026-11-18): pool 5's three runs (LIM-1.12) took 52, 51 and 75 min — 200 games each, 50 opponents,
both seats, `--game-minutes=6` — and 41, 37 and 84 of their 200 games were stopped at the cap, giving no result. AI-22
made a sealed run ~11% faster on 2026-11-13, but the quality work since (AI-20's two futures for each unseen draw,
AI-26's and AI-27's pricing) **doubled the cost of a decision**: the position suite 10 594 → 20 078 engine steps a
position, 49 s → 121 s. AI-22.1's profile (before the doubling): the rollouts 90% of a decision, the rollout's attack
plans (`WholeAttacks.Best`) 50%, free passes 36%, the engine's per-step checks the third block.

## Requirements

- [ ] Pool 5's run against the AI field (`ai_reports/sealed-builds-2026-11-18_164356.md`'s command) finishes in
      20 minutes or less on a quiet machine
- [ ] Its play held: the position suite's changed decisions read and accepted, a match no worse
- [ ] Stopped games a small share of a run, not a fifth to two fifths

## Decisions

- 2026-11-18, Shayne (sprint 6 planning): speed first this sprint, before the AI-quality items. A sealed run may also
  stop early once its answer is clear, as `--until-clear` does for matches — "a good stopgap for now".

## Tasks

- [x] AI-30.1 Re-profile a sealed run at today's default (AI-22.1's method): where the doubling went
- [ ] AI-30.2 The stopped games: what makes them long (boards, decisions), and their share of a run's machine time
- [ ] AI-30.3 Stop a sealed run early once its paired difference is clear (the stopgap)
- [ ] AI-30.4 The levers the profile points at, each measured (suite changed, cost per decision, a sealed run's wall
      time): AI-21's attack plans (AI-21.1–21.5), AI-24's leftovers, the engine's per-step checks in MagicCore and
      ImmutableGameObjects
      - [x] AI-30.4a LandOrNot's two rollouts of a line share their play before our library first changes
            (`Rollout.ScoreBoth`, `beam+nosharedstart` off) - Shayne, 2026-11-20: "lets test out A"
      - [x] AI-30.4b The second rollout only for the lines still in contention - a switch (`beam+contendersN`), off
      - [x] AI-30.4c With lands in hand for every land drop the look-ahead has left, the not-lands way alone (Shayne,
            2026-11-20: "if I already have 2 lands in hand, I don't need to simulate what happens if i need to draw a
            land next turn"; `beam+nolandscovered` off) - only lands that enter untapped count (Shayne, 2026-11-20:
            "D should only count untapped lands that makes more sense")
      - [x] AI-30.4d In place of B's ranking, the "lands" way only where a drawn land could change something (Shayne,
            2026-11-20: "fixing this obvious bias") - the default (match 48-48, −12.5%), `beam+nolandmatters` off; B's
            code removed
      - [ ] AI-30.4e The attack plans not re-judged against the real blocks when nothing of theirs can block
            (`beam+noblockers`, off) - Shayne, 2026-11-20: "A. sounds good"; it found a bug in the re-judging, fixed
- [ ] AI-30.5 The before/after sealed run, in `docs/findings/magic-ai.md`

## Next

AI-30.1's attribution is done by engine steps: the cost is AI-20.4's second rollout per line. The lever to design
with Shayne: play the second future only where it can change a line's rank. The time profile on a quiet machine
(overnight, 2026-11-19) still says what share the engine's per-step checks and the attack plans now have.
AI-30.3 needs the games played opponent by opponent (today all of build A's, then build B's, so no pair is complete
till the end), and Shayne's margin. Read AI-22 (done) and its findings ("AI-22.1", "AI-22.2" in
`docs/findings/magic-ai.md`), AI-21's Next and AI-24. `dotnet-trace` is in `~/.dotnet/tools`.

## Log

- 2026-11-18: created at the sprint 5 review.
- 2026-11-19: AI-30.1 by engine steps (load-free; the time profile still waits for a quiet machine): the suite with
  each sprint-5 feature off - ALL of the doubling is AI-20.4's second future (`beam+shuffle` 10 393 steps a position,
  19.7 rollouts, against 19 094 and 37.2); AI-26/27's pricing costs nothing measurable. Findings "AI-30.1".
- 2026-11-19: AI-30.2 first read - 27% of the 2026-11-18 sealed games stopped, as many decisions as finished games
  (slow decisions, not long games), cut at turn 14 median, some opponents far more often. Findings "AI-30.2".
- 2026-11-20: the levers for the second rollout, measured first (findings "AI-30.1"): A, share the start (~17%, near
  exact); B, the second rollout only for the lines in contention (~40%, changes decisions); C, skip the less likely
  way when its share is small. Shayne: test A. Built - `Rollout.Score`'s loop is now a `Walk` that can be copied;
  `ScoreBoth` plays the "not lands" way once and the "lands" way from the place before our library first changed.
  `beam+nosharedstart`: 0 changed, 19 094 steps (the rewrite exact); `beam`: **0 of 288 changed, 16 305 steps a
  position (−14.6%)**, rollouts 37.2 both. Test: the two scores equal to each played whole, fewer steps (seeds 2, 3;
  fails with the lands not made at the split).
- 2026-11-20: AI-30.4a committed (039764d6). Shayne: build B, and D too - "if I already have 2 lands in hand, I don't
  need to simulate what happens if i need to draw a land next turn right away, I can focus on what plays I know I can
  already make". D built (`Settings.LandsCovered`, `LandDropsCovered`): suite **13 374 steps a position (−18% on A,
  −30% on 19 094)**, rollouts 29.2, 1 changed (seed 2 #21 - Koth before or after combat, the near-tie that flipped in
  AI-29.4-29.7 too), oracle 74.0%. B built as a switch (`Rollout.ScoreFirst`/`ScoreSecond`, `SecondWays`; the rest
  given the contenders' average lift): contenders4 12 835 (−4.0% on A+D, 2 changed), contenders3 12 309 (−8.0%, 3),
  contenders2 11 628 (−13.1%, 5) - far less than the ~30% estimated: D already skips many second ways, A made the
  rest a third cheaper, and below the root a batch is only a few children.
- 2026-11-20: D's change accepted (Shayne: "its not a big deal if they cast it before or after combat, if nobody is
  attacking anyways"). Overnight, one after the other: `beam,beam+nolandscovered --until-clear` (D), then
  `beam+contenders3,beam --until-clear` (B).
- 2026-11-20: both matches flat, each stopped at 96 games (22 and 20 min on the quiet machine): D 51.0% (49-47), 7 593
  vs 8 312 steps a decision (−8.7%); B (`contenders3`) 50.0% (48-48), 6 806 vs 7 565 (−10.0%). Findings "AI-30.4".
  For Shayne: commit A's follow-ups (D on, B's switch) and decide whether B becomes the default.
- 2026-11-20: Shayne asked which game D won. Red-Green v White-Black seed 7, parting at a tied discard to Rank Rat -
  noise; but D had counted a tapped Konstrari Annex as covering turn 4's drop. Shayne: count only untapped lands. Built
  (`LandDropsCovered`, a test with the Annex): suite 13 582 steps (+1.6%), 1 changed - seed 2 #21 back to A's "Cast
  Koth", accepted (FRA.positions.txt equal to the committed one again). The matches stand: the fix moves few positions.
- 2026-11-20: B's three changes (`contenders3`, 12 455 steps, −8.3% on D) shown to Shayne. Seed 1 #60, Koth kept home
  (13 life, their 1/4 flyer untapped): "Not attacking is fine, the other player probably will block anyways". Seed 2
  #21, Yoshimaru 1/1 attacks and No Admittance at the 1/4 flyer instead of Koth: "playing koth is better". Seed 2 #71,
  Ajani's Anguish X=1 before Jace's −3 draw: "draw first is better" - and in BOTH, the X=1 at the opponent "should not
  be being used at the opponent unless there is additional context". B stays a switch, off.
- 2026-11-20: Shayne: Anguish at X=1 is fine for now (the counter and trample); why do land switches change non-land
  plays? Every move's score blends the two futures. Seed 2 #21, measured: Koth's "not lands" 23.29, "lands" 33.32
  (landfall) - blended 27.64, the best. Under B, Koth ranked below the top 3 by "not lands" alone, so its "lands" way
  was guessed with the batch's average lift - 25.06 - and the Yoshimaru attack + No Admittance line (29.39) won. B's
  own ceiling (`SecondWays`' ponytail), seen: the card a land lifts most is the one it ranks by the average.
- 2026-11-20: committed D + B's switch (b116e3d7). Shayne: build option 1 instead of B's ranking. `LandMatters`: a card
  the line drew (a land the other way), the hand's spells costing more than our mana to come, an X spell, or our
  landfall (the engine's trigger matcher) - else the not-lands way alone. First cut counted CARDS castable
  (`LandUnlock.Playable`) and ignored this turn's draws: 10 578 steps (−22%) but 6 changed, Koth #21 back to Pass and
  Jace −3 played late - fixed. Now: suite **12 326 steps (−9.2% on D's 13 582)**, rollouts 25.6, 5 changed - Koth #21
  and seed 2 #71 kept; seed 1 #70 and seed 2 #60 now draw with Jace −3 first; the rest order changes. Oracle 71.9% (the
  committed 73.6%).
- 2026-11-20: Shayne: run the match. `beam+landmatters` vs `beam`: 50.0% (48-48), no 5pp difference, **6 671 against
  7 620 steps a decision (−12.5%)** - more than D (−8.7%) or B (−10.0%) saved. For Shayne: make it the default.
- 2026-11-20: Shayne: "sure" - the default; its 5 suite changes accepted.
- 2026-11-20: AI-30.1's time profile, quiet machine (Shayne): the rollouts 82% (90% on 2026-11-12) - the attack plans
  37% (50%), the free passes 38% (36%), the opponent's step 36%, the engine's passes 25% (40%); AI-26.4's option price
  9%, invisible to the step counts. Findings "AI-30.1: the time profile". For Shayne: which lever next - the attack
  plans (AI-21), the free passes, or the option price.
- 2026-11-20: Shayne: the big ones first, but research before changing them - attack tests like the flash tests
  (story AI-33, Claude's draft of nine positions for him), A built, and two counts for the free passes. A
  (`beam+noblockers`): suite 11 533 steps (−6.6%), 2 changed - one a Cadet attacking where it had stayed home. Why:
  the real-blocks re-judging (`WholeAttacks.Blocked`) ran on past the combat whenever no block was asked - AutoPass
  and a step test that the next turn's untap also passes - so "no attack" was scored in the opponent's next turn
  and a blocked attack this turn. Fixed (this combat only): suite 12 014 steps (−2.8%), the same 2 changed (the Cadet
  attacks, then Jace −2 makes a new blocker; Koth's Mountain before Jace −1); A on top, the same answers, 11 551
  (−6.5% in all) - decision-neutral once the bug is gone.
- 2026-11-20: the plan count (24 games): 1 168 decisions replayed the plan, 1 922 searched - 84% of those because the
  plan had run out, 14% an unseen card drawn, ~1% a surprise (the score moved, their spell, a new turn). Findings
  "AI-30.4e and the free passes' first count". For Shayne: the fix + A as the default (the Cadet and Mountain boards),
  and which free-pass lever - a longer plan, or the second count first.
- 2026-11-20: committed with AI-33.4's two-attack clock: the blocks fix (its two changes gone again under the new clock
  - the Cadet stays home), A still OFF (`beam+noblockers`; not yet matched; decision-neutral on the suite under the old clock). Review: the
  probe's cap raised to 100 passes (one pass a step since AutoPass went).
