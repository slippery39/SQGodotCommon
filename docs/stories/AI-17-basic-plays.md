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

- 2026-10-29, Shayne (AI-17.3 (b), the attack as one decision): **each creature attacks its own best
  target** — "maybe there is a planeswalker on 4 loyalty. Your 4 power creature might want to attack that
  one, and your 2 and 3 power creature might want to attack their face" (the test); **attacks first,
  blocks later — "but remember the attacking player takes potential blocks into consideration when
  choosing who to attack with"** (each candidate attack is judged against the defender's blocks); **the
  cost is acceptable** — "I am looking for an AI that plays the game reasonably and if it solves certain
  plays it is making then that is good".
- 2026-10-29, Shayne (on the walker test — a "+1: draw a card" walker, the opponent at 20 with no
  creatures, the AI sending all at the face): "it depends on the walker and board … a jace token maybe,
  maybe not depending on the life total of the opponent. But an actual planeswalker with more than just
  draw a card? Very likely" — so the test is a real walker (Chandra, Torch of Defiance at 4 loyalty), and
  the Jace token's case is left open.

- 2026-10-29, Shayne (on AI-17.4's review): **"all of those shortcuts are bad, and we should address
  those. If it causes our AI to be more expensive speed wise, then its ok"** — every listed shortcut is
  to be fixed (AI-17.6), cost accepted.
- 2026-10-29, Shayne (the basics positions' answers): (1) 3/3, 3/3, 2/2 against one 2/2 at 5 — "easy
  attack, almost 0 downside"; (2) a 4/4 menace and a 2/2 against one 3/3 at 4 — attack; (3) a 3/3 flier
  and a 2/2 against two 4/4s at 3 — **only the flier attacks**: "the 2/2 would be blocked and no reason
  to attack (in real life, your opponent might be able to remove the flyer and you wouldn't want your
  2/2 to die)"; (4) a 6/6 trampler against a 2/2 at 4 — attack; (5) a 2/2 and a +2/+2 instant against no
  blockers at 4 — attack and pump; (6) two 3/3s and a kill spell against one 2/2 at 6 — kill it and
  attack; (7) at 5 with a 3/3 against three 2/2s, mana for a 2/2 or "draw two" — play the 2/2; (8) a 2/2
  against an untapped 3/3 at 20 — stay home; (9) a 2/2 "enters, 2 damage to any target" over a vanilla
  3/3, their only creature a 2/2 — yes; (10) a 5/5 or a 1/1 lord — **no answer: "a judgement call and
  depends on other cards in your hand and deck"**, so not a position; (11) Infinite Coursework on their
  only threat, a 5/5 — yes; (12) surveil when flooded — "surveil excess lands that are not needed", and
  look at how MtgSimulator values lands for scry / surveil / discard when there are enough. (13) a library
  search and (14) a planeswalker's best ability — to discuss first.

- 2026-10-29, Shayne (the failures' rules): (3) two attacks that both win this turn — the one that
  leaves us the better board, everyone on equal boards: "Yes I would say so for now". (9) kill now or
  later — option A, a line's value counted partly at this turn's end: "generally in MTG if I am dealing
  with a high value target, I do usually kill right it away, unless the risk reward of waiting is alot
  higher … like playing around counters or protection spells, vs trying to surprise the opponent and
  maybe get a little more potential value. If there isn't any more potential value to get though, then
  i'll kill it right away. It kind of scales with the severity of the threat." (13) **"Tutors should
  fetch the highest potential value card in theory. This is kind of the same problem as surveil and
  scry, but just with more potential cards to choose from"** — MtgSimulator was solving it. (14) **"like
  Chandra for example, even if it has 1 loyalty, you will want to attack it still, because it could draw
  the opponent a card still (with its first +1) or give your opponent mana … Even with like a 4 or 5
  power attacker, the risk of not being able to remove it later on is super high and can be game losing.
  This is not the same risk as not attacking a jace planeswalker token … since the ceiling of jace is
  just drawing a card."**

- 2026-10-29, Shayne (the flier, after `Explain` showed the look-ahead's defender blocking nothing —
  every block an equal −10 000 loss, the tie to "no block"): **"it should block anyways. Like yeah it is
  technically dead, but again we want to make the best play possible even if our search shows that we
  are winning or losing no matter what. This is because players can always make mistakes. So yes it
  should block."** And: "I feel like the early exit due to the scoring is causing issues here" — a
  finished game scored a flat ±10 000, blind to the board. So a finished game keeps a board tie-break
  under its win or loss, for every chooser (extends AI-16.4's "a lost line keeps the best board" to a
  loss inside the turn — DesignNotes' known ceiling, now asked).

- 2026-10-29, Shayne: the `+flatend` match approved (one run). And position 7's cause, life near 0
  priced too cheaply (5 → 1 for 8 points): **"As your life total gets lower you take on more risk of
  your opponent having something that can just kill you, so we do need to account for it"** — agreed:
  below 6 life a lost point costs more the nearer 0, e.g. (6 − life)² ÷ 2 on top (0.5 at 5, 12.5 at 1).

- 2026-10-30, Shayne (the life risk is symmetric — their low life prices our burn at their face higher):
  **"burn to their face is better at low life, but in general we would only do that if either the burn is
  player only, its some sort of repeatable burn effect, or if we have enough damage in hand to completely
  burn them out."** A rule to build as positions (a burn spell that can hit a creature, at their face
  only when one of those holds). The end-board match re-run approved (one run); the commit approved.

- 2026-10-30, Shayne (position 9 re-explained — the Pinger's 2 damage is a one-time "enters" trigger):
  "In that specific case, with a vanilla 2/2 as the opponents card playing the 3/3 is probably correct.
  When you said "pinger" I thought you meant an activated ability on a creature you already own." Not a
  miss; the position is dropped and option A stays reverted. Lesson: name a test card by what it does.

- 2026-10-30, Shayne (the Deathbringer at #262 — the AI, with Settle's fix, attacks with Tinybones and
  then −2 sacrificing TINYBONES): **"the idea is that we can recur the blessed ghoul from the graveyard,
  so sacrificing the ghoul means we can just get the ghoul back next turn and potentially do it again."**
  His answer is the −2 sacrificing Blessed Ghoul; the AI sacrifices Tinybones with or without the fix —
  AI-17.2's test checked only "the −2", so it never checked this.

- 2026-10-30, Shayne (Claude's proposal: a card in our graveyard that can return itself priced like a
  known card in hand, less the mana to bring it back): **"the graveyard rule is basically like paying a
  cost to be able to have access to a card. So that should be priced in."** Item 11 (plays per rollout
  turn) against #547: "back to item 11 a little later". The `+quicksettle` match approved (one run).

- 2026-10-30, Shayne (the Deathbringer's imagined race: the AI at 6 swings its Beast at him on 15, his swing
  back takes it to 1, the next one kills — its look-ahead ends at the start of his turn, one step short):
  "Did we not solve the horizon problem in MtgCore? … why it seems we are repeating the same issues" (no:
  MtgSimulator never fixed it — DesignNotes, "a lethal board at the horizon", deferred AI-8.5) and the
  rule: **"We need to make sure we always account for a counter attack from an opponent whenever we are
  choosing attackers ourselves."**

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
- [x] AI-17.3 Fix 2, lethal: (a) the cheap defender chumps facing lethal, (b) the attack as one decision,
      (c) the early stop on a win this turn only — the position test and control, the suite, a match
      (each part measured alone where it can be) — **in progress (2026-10-29):** (c) built —
      `BeamSearchAi.Settings.WinNowOnly` (`beam+anywin`): 50.0% over 96 games, +5.2% cost a decision. (a)
      built — `Evaluator.Terms.ChumpBlocks` (`beam+nochump`), `Lookahead.Chumps`: first as "the blocker
      asked first chumps": 48.6% ± 1.0 over 288 games (no 5pp difference, but leaning −1.4); then, Shayne:
      **"you always want to chump with your least valuable creatures first"** — the chumps planned for every
      blocker still to be asked, the biggest attacker taken by the least valuable that can block it
      (`Evaluator.ValueOf`); its test `FacingLethal_TheLeastValuableCreatureChumps` (and its control).
      Either fix alone makes the turn-21 attack (`ALethalAttack_IsMade_NotPostponedForALaterWin`, three
      cases); the AI-16 control now switches chumps off too. **Open:** with the least-valuable chump the
      agreed #547 hold flips — Unsummon on Fateshaper Aspirant −51.8 against Pass −53.5 (without chumps
      Pass −42.0); the root rollouts are identical either way and both plans don't block, so the cause is
      deeper in the tree. Shayne then: a 4/4 and a 1/1 against a 5/5 "should actually be blocked by
      both" — a 6/6, the 1/1 alone: built (the least valuable that TOGETHER kill it all block), the test's
      four cases pass. **Found with `--explain` (built for it — Shayne: "I would have to see what happens
      on a deeper search"):** in the bounce line the look-ahead's opponent recasts Fateshaper Aspirant and
      its trigger puts the +1/+1 counter and indestructible on SHAYNE'S Proctor — the first legal target
      (`Rollout.Targets.FirstLegal`, AI-16's side note) — then attacks Gallia alone into two blockers.
      With `Targets.OwnersBest` #547 holds again (the bounce −69.2, Pass −53.5). The chump rule only
      changed which imagined futures the search reached; one held the misplay. **Targets by their owner's best
      made the default** (Shayne, 2026-10-29 — `Rollout.Targets.OwnersBest`; `beam+firsttarget` is the
      old): #547 holds; `OwnersBest_AimsATrigger…` now compares against first-legal explicitly.
      GreedyAi's golden games regenerated (5 of 12 moved — the chump rule alone, checked by forcing it
      off: all 12 pass then). MagicCore.Tests 1 153 pass. Suite with (a), (c) and the targets: 30 of 288
      decisions change, 3 531 steps a position (+14% on AI-17.2's 3 103 — (c)'s deeper search in won
      positions). Matches: chumps on/off **50.0% ± 1.5 over 96 (48–48)** — the lean gone; targets
      owner's-best/first-legal **49.3% ± 1.2 over 288 (142–146)**, no difference. Suite accepted; the
      oracle re-read: **87.2%, score given up 0.59** (85.4%, 1.06). The review: facing lethal the chump
      plan goes first, a menace attacker with one blocker is unblocked, `Explain` uses the search's own
      sample and terms, `Evaluator.WinScore` named, `+targets` gone (`+firsttarget`) — 1 suite decision
      and 1 golden game moved; the oracle 87.2%, 0.81. Committed with `--explain`. **(b) the attack as one decision —
      built (2026-10-29):** `Ai/WholeAttacks.cs` — candidates (everyone, no one, each alone, all but one, one
      creature at another target), each creature at its own best target given the bigger ones
      (`Targets`), the declaration answered whole (`Declare`); in the search's TREE only
      (`BeamSearchAi.Settings.WholeAttacks`, `beam+eachalone` off) — in the look-ahead too
      (`Rollout.Attacks.AsOne`, `+asonelookahead`) cost +27.6% a position against +6.5%, so Shayne: "search
      only for now, but if we ever find a test case that needs it to be in the look ahead then we will have
      to reconsider". Tests: the turn-21 attack by whole attacks alone; the all-in pair (AsOne finds it,
      each-alone doesn't); Shayne's walker case as Chandra, Torch of Defiance at 4 — a KNOWN DEFECT,
      ignored: the attack offers "the 4/4 at Chandra", but the evaluator prices her abilities at nothing
      (AI-9.3), so the 2/2 chips her. Suite: 4 of 288 change, 3 768 steps (+6.5%), the oracle 87.5%.
      The review (before the match — its first run stopped): "don't attack" counted as an answer
      (`WholeAttacks.IsFirst` read only the attacks, so a first creature left home made the next question
      look like the first, and the AI dropped its plan and searched again — revert-checked by
      `AWholeAttack_AnswersEveryLaterQuestion_FromItsPlan`); a whole attack is offered at any question no
      plan decides for (a fresh AI mid-declaration — the suite, `--compare` — plays as the table does),
      and a plan answers only its own creatures in its own turn; "no attack" is never narrowed out (as
      Pass); the class renamed `WholeAttacks` (it shared `Rollout.Attacks`' name). The Gallia control
      switches whole attacks off (they attack there too). Suite: 5 of 288 change, 3 769 steps (+6.6%).
      Match: **49.5% ± 1.7 over 192 games (95–97), no difference**, +1.1% a decision — kept. Suite
      accepted; the oracle re-read 87.2%, score given up 0.84 (0.81 before (b)).
- [x] AI-17.4 The review, code: read the decision path — `BeamSearchAi` (Search, Narrow, Prune, Winner,
      Best, plans and their replay), `Rollout` (Score, OurPlay, OpponentStep, Answer, BestAlone),
      `Lookahead` (Settle, QuickAnswer, Joins), `Evaluator`, `Hidden`, `GreedyAi` — about 2 000 lines;
      every shortcut listed (the `ponytail:` comments, DesignNotes' "MagicCore known ceilings", AI-3's
      models not taken) and each judged: can it make the AI miss an obvious play? Compared against
      MtgSimulator's AI for anything it does that this one doesn't — **done (2026-10-29):** written up in
      `docs/findings/magic-ai.md` ("The review, code"). Six likely to cause a miss: (1) `Settle` still
      declines every may and aims every trigger at its first legal target — and it decides which children
      get a rollout (5 below the root), our rollout's play, the opponent's plays; (2) walker abilities
      unpriced (AI-9.3); (3) the simulated opponent's attackers each alone — walking into a swing that
      kills only together; (4) every priority window with an instant in hand costs a search level, so a
      precombat play pushes the whole attack past depth 3; (5) a creature valued by stats and keywords
      only; (6) a creature that never untaps (Infinite Coursework) counted at full value and as an attacker.
      Five possible (no potential bucket in Narrow; several-pick choices one cheap answer — surveil never
      bins; plans replayed; the table's cap; one play a rollout turn). Not ported from MtgSimulator: its
      potential bucket and rolled-out combinations. Nothing changed in code: AI-17.5's positions test them
- [ ] AI-17.5 The review, behaviour: the basics suite — hand-built positions with Shayne's answers (lethal
      on board: with blockers, menace, flying, trample, a pump; a chump when facing lethal; a "may" for
      value; a search that finds its card; removal on the biggest threat; a planeswalker's best ability;
      no attack into a losing trade; …), the AI run on each; every failure a task here. From the review
      (AI-17.4) too: an "enters, deal damage to any target" creature cast over a plain one; keeping a
      blocker home against a swing that kills only together; removal on the only blocker, then lethal,
      with an instant in hand; removal on an engine over a bigger body; Infinite Coursework against a kill
      spell; a surveil that bins a land when flooded — **first eleven built (2026-10-29):**
      `MagicCore.Tests/Ai/BasicsTests.cs`, Shayne's answers (1)–(9), (11), (12); (10) has no answer, (13)
      and (14) wait on a discussion. **7 pass:** lethal through one blocker, with menace, with trample,
      with a pump, after killing the only blocker (an instant in hand — review item 4 didn't bite here),
      no attack into a losing trade, Coursework on their only threat (item 6 didn't bite: no other play).
      **4 fail, ignored as AI-17.6 tasks** (each cause read with `Explain`, not guessed): (3), (7), (9), (12)
- [ ] AI-17.6 What the review finds, fixed in order of how often it bites — each measured. Shayne: every
      shortcut in AI-17.4's list is to be fixed, cost accepted. From the basics suite:
  - [ ] (7) the blocker over Draw Two at 5 life — **re-diagnosed (2026-10-29):** Claude's position was
        wrong: at 5 with one 3/3 against three 2/2s, the swing puts us at 1, it doesn't kill. With
        `Rollout.Attacks.AsOne` the look-ahead imagines that swing, and Draw Two still wins (−13.30 against
        −17.50): 5 life → 1 costs 8 points, under one dead 2/2 plus their weaker clock. **Life near 0 is
        priced too cheaply** — the rule to agree with Shayne (AI-9.2's taper was dropped, 16 tests moved).
        At 4 life (where it does kill only together) both models cast the blocker — `AsOne` sees Draw Two
        as a loss (−9 529), each-alone wins by less (−17.50 against −19.30). **Built (2026-10-29/30), suite
        and match to run:** at 5 life BOTH fixes are needed, each shown alone not to flip it: the life risk
        (`Evaluator.Terms.LowLifeRisk`, `+flatlowlife`: below 6, (6 − life)² ÷ 2 more — Shayne's rule) with
        the default look-ahead still never imagines the swing (each 2/2 alone dies to our 3/3); **whole
        attacks in the look-ahead, the default now** (`Rollout.Attacks.AsOne`, `+eachalonelookahead` the
        old; +27.6% a position in AI-17.3 — Shayne: "reconsider if a test case needs it", and cost
        accepted) — so review item 3 IS a miss, together with the life price. Test with both controls.
        AI-16's control switches both off; the all-in test's control names `EachAlone`. Golden games:
        1 more moved by the life term (all 12 pass with it forced off); regenerated. Suite (with (12)'s
        branching): 21 of 288 change, 4 470 steps a position (+18.5% on 3 772); each off alone —
        `+eachalonelookahead` 8 change, 3 745 steps; `+flatlowlife` 15, 4 523; `+onepick` 21, 4 470 (it never
        fires in the suite). MagicCore.Tests 1 169 pass. **Matches (2026-10-30), each no 5pp difference —
        kept:** `+eachalonelookahead` 49.7% ± 1.6 over 288 (143–145), +22.7% a decision; `+flatlowlife`
        49.2% ± 1.1 over 384 (189–195), +0.9%; `+onepick` 50.0% over 96, identical (never fires). Suite
        accepted; the oracle re-read 86.5%, score given up 1.03 (87.2%, 0.84 before). The review: the
        end-board tie-break saturated (squashed by its cap, 10, an ended game's position ±60+) — now over
        `EndBoardScale` (100); several picks: fewer options than asked gave no answer, duplicates deduped.
        1 suite decision moved, accepted; `+flatend`'s match ran on the saturated version — re-run on
        b817065: 51.0% ± 1.0pp over 96 (49–47), no difference, kept
  - [ ] (3) two attacks that both win this turn: the tie-break counts their life under 0, so everyone
        attacking (10 002) beats the flier alone (10 000) — **built (2026-10-29), match to run:** `Explain`
        showed the deeper cause: the look-ahead's defender, dead to the flier whatever it does, blocked
        nothing (every block −10 000, the tie to "no block"), so the 2/2's attack cost nothing. Shayne: "it
        should block anyways … players can always make mistakes", and "the early exit due to the scoring is
        causing issues". So **a finished game keeps its board** (`Evaluator.Terms.EndBoard`, `+flatend`):
        ±WinScore plus the position squashed under `EndBoardCap` (10), for every chooser; `Winner`'s
        "a win this turn" threshold allows for it, and two lines still never cross a turn (448 against a
        475 gap). It alone fixes the flier (a "won-this-turn by our board" tie-break was built first and
        removed: not needed). Test with its control (off: the 2/2 attacks). AI-16's control switches it
        off too. Golden games: 6 of 12 moved — all 12 pass with it forced off; regenerated. MagicCore.Tests
        all pass. Suite: 3 of 288 change, 3 772 steps a position (+0.1%). **Match: 49.5% ± 0.9pp over 192
        games (95–97), no difference, 1 537 steps a decision against 1 545 — kept** (done but for the commit)
  - [x] (9) NOT A MISS — Shayne (2026-10-30), once the position was spelled out (the Pinger's damage is a
        one-time "enters" trigger, not an ability of a creature already in play): "with a vanilla 2/2 as the
        opponents card playing the 3/3 is probably correct". Claude's "Pinger" read as an activated ability;
        the position is dropped from BasicsTests. What follows is the history. The Pinger loses to a vanilla 3/3 (23.08 against 24.24): the look-ahead's opponent never
        improves its board (it plays no unknown card), so their 2/2 killed on turn 3 ends the same as killed
        now and the 3/3 first deals one more damage — NOT review item 1; the rule to agree with Shayne —
        **option A tried and reverted (2026-10-30):** a share of each line read as our turn ends
        (`Rollout.Settings.NowShare`): the Pinger is cast from a share of 0.2 (0.15 not), but AI-13.4's
        agreed hold breaks at every share tried down to 0.1 (`Unsummon_IsHeld_AgainstAPlainAttacker`: the
        bounce's tempo shows at once, their recast only by the horizon). A uniform "now" can't tell "waiting
        buys nothing" (the Pinger) from "waiting has value" (Unsummon) — back to Shayne
  - [ ] (12) surveil bins nothing: a choice of several picks gets one cheap answer, the minimum (review
        item 8); branch it. Shayne also: price lands by what they unlock, as MtgSimulator's
        `CardValueTable` did (DesignNotes, "values resources by count") — AI-9's half — **built (2026-10-30), match to
        run:** `BeamSearchAi.Settings.BranchPicks` (`+onepick`): a choice of several picks is branched on every
        combination, each rolled out (up to `MaxPicks`, 32), as MtgSimulator's ResolveChoice; the tree only.
        The position bins both lands; its control (off) keeps them
  - [x] (13) a tutor fetches the highest-value card (Shayne) — **already so for a one-card search
        (2026-10-30):** `Landcycling_FetchesTheColourTheHandNeeds` (Claude's reading of his rule: two Plains
        in play, a {1}{B} creature stuck in hand, Apex Witchstalker landcycled — the Swamp). The tree tries
        each card, the rollout sees the creature cast. The CONTROL: one ply ties the three basics (4.50 each)
        and takes the first by name. "Up to N" searches (Fblthp) take the first N — only basics in FRA
  - [ ] (14) a walker attacked even at 1 loyalty (Shayne's Chandra rule) — **position built, fails, ignored
        under AI-9.3 (2026-10-30):** `AWalkerAtOneLoyalty_IsAttacked_OverTheirFace`: Chandra priced 2.35
        (body and loyalty, her abilities unpriced; in the look-ahead her +1 only deals us 2), so 5 at their
        face (23.60) beats killing her (21.63)
  - [ ] Review item 1, `Lookahead.Settle` — **built, waiting on Shayne (2026-10-30):** position
        `AnEntersTrigger_BehindALandDrop_DestroysTheirThreat` (a 3-mana "enters, destroy target creature"
        behind a land drop, six 1-mana 1/1s, their 5/5): the land's line scored as a pass — Settle had the
        destroyer kill ITSELF (ours are listed first), so our rollout never cast it — and fell out of the
        beam. Fix: `Evaluator.Terms.SettleChoices` (`+quicksettle`): Settle answers one target or a single
        "you may" by its chooser's best, one level down (two levels tried — not needed). The position also
        needs review item 11 (more than one play a rollout turn, from 3) — and FROM 2 PLAYS the agreed
        #547 hold casts Unsummon (its imagined next turn plays Hortimancer AND Proctor from the graveyard
        in the bounce line), so `ActionsPerTurn` stays 1 and the default AI still misses the position.
        SettleChoices alone: card values — Lich's Relic 2 → 16.5 ("you may pay {2}" was declined),
        Yoshimaru 4.5 → 16.5 (its "up to one" fight never fought); regenerated. Golden games 8 moved (all
        pass with it forced off), regenerated. Suite 9 of 288 change, 4 866 steps a position (+8.8%). The
        Deathbringer test (#262) fails: at #262 the AI goes to combat instead — it attacks with Tinybones first, THEN −2
        sacrificing it — the same 4/4 and 2 more damage (played on: Beast made, Shayne 15 → 13) — asked
        Shayne before changing his test. **Match: 49.5% ± 1.7 over 192 (95–97), no difference, +4.4% a
        decision — kept.** It moved MagicTable's seeded test game (GreedyAi plays it): four
        `MagicTableManagerTests` lost their "a creature able to attack on turn 9" — the seed is now one
        constant, 15 (seeds 1–14 each missed one). Shayne then: the right sacrifice is Blessed Ghoul (it returns) — the test now
        asserts that, ignored: **graveyard access** (`Evaluator.Terms.GraveyardAccess`, `+deadgraveyard`,
        Shayne's rule) is built and fires (`ACardThatReturnsItself_KeepsAValueInTheGraveyard`), but the
        sacrifice is still Tinybones — fresh, −14.29 against −16.74, the gap from the imagined next turn's
        combat (kept, the Ghoul trades with Heartstring Puller and the Beast attacks for 4); and at the table
        the sacrifice came from #257's plan (review item 9). Suite unchanged by it. Back to Shayne
  - [x] The counter-attack always seen (Shayne's rule, 2026-10-30) — **done: 50.0% ± 1.4 over 384 (192–192),
        +16.6% a decision, kept; suite accepted, the oracle 88.5%, 1.00 given up.** the cause of
        the Deathbringer's Tinybones sacrifice was the HORIZON, not the blocks: from our own turn the
        look-ahead stopped at the start of his, so the AI at 6 swinging its Beast at him on 15 never met his
        swing back (6 → 1, the next one killing). With one more turn it sacrifices the Ghoul (probed: 3
        lookahead turns, with or without a race-aware clock). `Rollout.Settings.CounterAttack`
        (`+nocounter`): when the turn at the horizon is the opponent's, the rollout plays it too. A race-aware
        clock (a side's clock 0 if its all-out attack dies to the swing back) was built first and reverted:
        it didn't flip the position (both imagined lines pinned the Beast) and pulled the oracle the wrong
        way. Position: `TheDeathbringersBeast_IsMadeFromBlessedGhoul_WhichComesBack` (control: off, Tinybones).
        Two tests moved: the chump test now asserts the chump buys a turn (their next swing kills); the
        AI-9.5 token-bounce test's old look-ahead stops after our turn too (the default still bounces the
        token). Suite: 18 of 288 change (9 without), 5 790 steps a position (+19%); the oracle (read before)
        agrees 88.5% (86.1%), score given up 1.02 (1.12), walks into a loss 1 (3). fcba060 had not
        re-accepted the suite: accepted with this one. The review: `TurnEndCap` counts the extra turn
  - [ ] Blocks judged with a turn's foresight in the look-ahead (Shayne, 2026-10-30: "usually when you
        attack you are thinking of what will happen if they attack back … If so we should try to fix
        this") — the imagined defender scores a block by the board right after combat, so killing their
        3/1 trampler or their 2/2 looks alike, though the trampler alive pins our Beast at home next turn.
        Position: `TheDeathbringersBeast_IsMadeFromBlessedGhoul_WhichComesBack` (ignored)
  - [x] **Done (2026-10-30): 50.0% over 96 (48–48), +1.7% a decision, kept:** `Evaluator.Terms.SettleDeep` (`+shallowsettle`): Settle's
        chooser's best two choices deep, and for every single pick. Tests (with controls):
        `TheCheapLookAhead_PaysForLichsRelic_AndDestroysTheirThreat` (one level: their 5/5 lives) and
        `TheCheapLookAhead_AnEdict_TakesTheirLeastValuable` (first listed: their 5/5 went). Golden games 2
        moved (all pass with it forced off), regenerated; card values unchanged. Suite: 2 of 288 change,
        5 881 steps a position (+1.6%) — both look better: no +1/+1 counter from Yoshimaru on THEIR Screeching
        Soulbreaker (#181, now passes), No Admittance on a Repurposed Enforcer over a Yoshimaru with nothing
        to fight (#18); the oracle (read with the old look-ahead) 87.8%, re-read with the new 88.5%, 1.00. The
        review: a one-option pick takes it without a look-ahead; forced picks capped at 8 options. The task this answers:
  - [ ] From the review of review item 1's fix (before its commit): Settle's chooser's-best is one level —
        a "you may pay … when you do, target" reads as paying for nothing inside Settle; a required single
        pick (a sacrifice) still takes the first listed; Settle prices hand cards flat, so graveyard
        access is 0 there; and a second "chooser's best" beside `GreedyAi.Answer` (DesignNotes)
  - [ ] Review item 6, Infinite Coursework counted whole — **a position, passes (2026-10-30):**
        `Removal_GoesOnTheLiveThreat_NotOneCourseworkHolds` (their 5/5 under our Coursework and their 3/3,
        a 1-mana kill: it kills the 3/3 — the look-ahead never sees the locked 5/5 attack). Kept as a guard;
        the evaluator's price of a locked creature is unchanged. Shayne (2026-10-30), asked: "infinite
        courseworking a 3/3 on an empty board is a good play" — about Coursework itself; the guard's answer
        (the kill on the 3/3) is still Claude's. Then, asked again: "Killing the 3/3 could be relevant, its
        situational so lets just leave it for now" — the guard stays as it is, its answer situational
  - [x] Review item 10, the table's 1 s cap — **doesn't bite on the basics (2026-10-30):** every
        `BasicsTests` position played through the table's settings (`TimeLimit` 1 s) passes as it does
        uncapped (`docs/findings/magic-ai.md`). Re-check when a table game shows a miss the suite doesn't
  - [x] Review item 9, plans replayed — **kept (2026-10-30): plans off (`+noplans`) 49.3% ± 1.6 over 288
        (142–146), no difference, at ×1.98 the cost a decision.** Measured first: three self-play games
        (beam both seats; seeds 1–3), at every decision answered from a committed plan a fresh search run
        on the same position: 23 of 83, 40 of 195, 20 of 58 replays with a real choice DIFFER (25–35%).
        Many are timing (the plan passes, plays the land or spell later); some are not — an attack made or
        held (Samut, Rampart Hunter at Jace), Jace −3 against −1, Divining Duelist's mode. A difference is
        not a worse play: the plan's line is coherent across the turn. The question is a match: a fresh
        search at every decision (`CommitPlans` off, `+noplans`), or plans kept only inside one priority
        window. The suite cannot see this (it decides every position fresh)
  - [ ] DEFERRED (Shayne, 2026-10-30: "hold off on the burn rule now, but note it for later") — burn at
        their face (Shayne, 2026-10-30): only when the burn is player-only, repeatable, or the
        damage in hand burns them out — otherwise a creature. Positions first (the symmetric life risk
        prices their low life higher, so check it doesn't send burn at the face against his rule)
- [ ] AI-17.7 Blunder counters (Shayne, 2026-10-29): over N AI games, each decision checked for a named
      mistake, counted whatever the game's result — **missed lethal** first (an attack that kills through
      the defender's best blocks, not made; AI-17.3 should drive it to 0), then walked into lethal,
      self-harm (damage at its own face or creatures for nothing — AI-16's self-burn), a card thrown away
      (cast for no effect — AI-16.4's X = 0 Anguish); each checker's exact rule agreed with Shayne first.
      **Scope and priority (Shayne, 2026-11-08, after the playtest):** a lint, not a weight - it never changes a
      decision, so it cannot make the AI worse; it runs over a VERY large sample of games (hundreds) and reports each
      class's count and its positions, so mistakes are fixed by class, ranked by how often, not by anecdote. Only
      OBVIOUS mistakes: ones no reasonable player makes. Subtle ones (the believable kind) stay one at a time, from
      his playtests. **First checkers to agree with him** (each with its exact rule): (1) a land to play, and passing
      (AI-13.7 fixed one; a discard to hand size with a land in hand); (2) an attack into a bigger untapped blocker
      with no trick and no follow-up - a creature thrown away; (3) a removal or fight with no target worth it, or
      a free kill left unused (Amok Time, AI-17.14); (4) a "may" taken that does nothing (exile instead of surveil,
      AI-17.11); then missed lethal / walked into lethal, self-harm, a card cast for no effect, as above. Pre-fix
      builds can be the control: each fix since 2026-11-08 has a switch, so the lint on `beam+nolanddrop` etc. must
      show the class the fix removed. **Moved to the FRONT of the next session** (above the F6 inspector)
- [ ] AI-17.8 The long view (Shayne: win rate "slowly increasing over time against a more basic
      algorithm"): a fixed anchor and a long match each sprint, sized to see 1–2pp, the trend kept in
      `docs/findings/magic-ai.md` — **agreed (Shayne, 2026-10-29): the anchor is sprint 3's starting AI**,
      rebuilt from the switches (`beam+nogang+lifeprogress+nomays`, and each later fix's switch added —
      so those switches stay; freeze a build if that gets unwieldy); **~1 500 games (±0.45pp, ~1–1.5 h)
      near each sprint's end**, asked before it runs. Not GreedyAi: the search already beats it ~75%, past
      the 70% a top player reaches, and it moves with every evaluator and look-ahead fix
- [ ] AI-17.9 Bug (found in MC-5.3 batch 4a's review, 2026-11-01; Shayne: note it, fix later): the clock's
      next-attack estimate (`Evaluator.Through`) asks `Combat.CanBlock` and `s.Has` on the board as it is
      NOW, so a grant that ends this turn is read as lasting into next turn — La'An's "target creature
      can't block this turn", an attacker's flying or "can't be blocked" until end of turn. For the rest of
      that turn the AI expects more damage through next turn than there will be. `Through` already strips
      this turn's toughness pumps (`Pumps`); the fix strips every until-end-of-turn grant the same way (all
      three at once), measured the AI-6 way — a position with La'An's trigger resolved, the suite, a match
- [ ] AI-17.10 Block that loses to the trick (Shayne, 2026-11-08): turn 3, the AI blocked his Astute
      Terraformer with its own; he pumped his and killed theirs for free. Record
      `game_records/2026-11-08_024830_seed30768236.json` #92. Rebuild as a position: does the search see his
      open mana and the pump? Measure as AI-6 does.
      **Cause (rollout model):** `Rollout.TheirResponse` opened only in OUR turn, and for spells only — so in
      HIS combat, after our block, the simulated opponent had no priority and the block read as a clean trade.
      Fix (`Evaluator.Terms.TheirCombatTricks`, `+notheircombat`): its own DeclareBlockers window, and a
      mana-costed ability (`IsManaPump`) beside the spells. The position (`TerraformerBlock`, kept) flips to
      "don't block" (-12.31 blocking, -4.03 not); test `ABlock_ThatLosesToTheirOpenManaPump_IsNotMade`, control
      included. Suite: 1 of 288 changed (Jace deck, move 153, Island → Pass), oracle 88.2% → 87.8%. **Match run: 96 games, 51.0% ± 1.0pp, no 5pp difference, +4% cost: kept as the default**
- [ ] AI-17.11 Dominion Assault Force exiles a card instead of surveilling (Shayne, 2026-11-08): it exiled its OWN
      Infinite Diversity (`2026-11-08_191858_seed10869` #127–128) and Shayne's Tal Shiar Operative
      (`2026-11-08_192946_seed1126905837` #294–295), where "surveil 1 is 100% the correct play"; the same AI chose
      surveil at #445. Played on the pre-fix build (Godot's MagicCore.dll was 16:01). Rebuild, `--explain`, name the cause.
      **Cause (a tie, not a judgement):** both modes scored identically in the search - the rollout
      keeps its one sampled top card, so surveil gained nothing, exiling a card with no graveyard value cost nothing,
      and the first listed mode won. **Fix:** `Evaluator.Terms.SurveilPrice` (`+nosurveilprice`): a TRIGGERED surveil
      waiting on the stack counts a third of a card (`SurveilShare`, Shayne's own price) as a search-node bonus
      (`Evaluator.PendingSurveil`). Kept `DominionExilesOwnCard` (#127) and `DominionExilesTheirCard` (#294): both
      surveil now, controls included. Loyalty abilities excluded - a first version counted the Jace token's
      surveil twice and made it surveil rather than draw (14 suite decisions). Suite: unchanged from the AI-19.21
      baseline (17 changed), cost the same; 1523 tests green. Not yet committed
- [ ] AI-17.12 An attack that just loses the creature (Shayne, 2026-11-08): his 1/1 officer attacked into Shayne's 4/3
      Saurian Explorer with no trick, died, then a 3/3 was cast. Record not found yet - search the four 2026-11-08
      19:18–19:40 records for the attack.
      **Found and already fixed (2026-11-08):** `2026-11-08_191858_seed10869` #264, T13, the Officer 1/1 token attacked
      into the untapped Saurian Explorer 4/3. The old look-ahead defender never blocked it; with AI-19.21's
      `TrueDefender` the search stays home (no attack -0.30, attack -3.71). Switching only that term off brings the
      attack back; `TheirCombatTricks` and `SurveilPrice` make no difference. Kept `OfficerAttacksIntoExplorer`,
      test `AOneOneToken_DoesNotAttackIntoAnUntappedFourThree`, control included. Not yet committed
- [ ] AI-17.13 Guidance Failure main phase, then no attack (Shayne, 2026-11-08; `2026-11-08_192946_seed1126905837`
      #177): Starfleet Spanner Jockey and a Dahar Master out, Raffi put on top of the library, no attack. Play it
      main phase to attack, or hold it - "the worst play possible" is both a main-phase cast and a pass.
      **Not reproduced (2026-11-08):** the cast (#177) scores 22.56 against 21.84 for passing, Raffi and the Fugitive
      Listener tied; at #187-188 every search we can build attacks with both - the AI from BEFORE this session
      (commit 79ccbb43, checked out on its own), today's, plans kept or not, caps from 10 ms to 1 s. The live game's
      `Don't attack` stays unexplained (the record header says BeamSearchAi). Left: an undo (the table keeps one AI
      object, with its plan, across an undone move) or a state the record does not hold. Pinned as it plays
      (`AfterGuidanceFailure_TheAiAttacks…`, kept `GuidanceNoAttack`). **Ask Shayne: did he undo in that game?**
- [ ] AI-17.14 Amok Time with nobody to kill (Shayne, 2026-11-08; same record #376–380): a 4/4 out against 3/1s, a 2/2
      and a 3/2, and the second target chosen as none; it could have killed one for free.
      **Cause: a defect in AI-19.21's `TrueDefender`, found here.** The simulated human's attack included Tal Shiar
      Operative, whose trigger ("choose one") leaves a pending choice after the attack is declared; the probe
      (`WholeAttacks.Blocked`) only went on when none was pending, so it silently judged the attack against the
      WEAK defender again (the all-in read "AI to 7 life"), the human looked to win a bloodbath, and a stall after a
      kill scored -0.87 against +9.73 for killing nothing. **Fix:** `Blocked` drives the combat to its blocks,
      answering such choices - but never a pending attacker choice (a creature the plan left out): answering that
      re-picked the attack, broke `FacingLethal_WeChumpBlock` and the Duelist, and was found by logging the choice
      type. Kept `AmokTimeNoSecondTarget` (#380): it now fights the Operative (-0.87 against -24.05); control
      included. Suite unchanged (17, 8 879.4 steps), 1524 tests green. Not yet committed
- [ ] AI-17.15 Exiling an opponent's graveyard card is priced at nothing, surveil at nothing: check what each is worth
      to the search before AI-17.11 is fixed (the second half of the same report)

## Next

**Next session (decided 2026-11-08, Shayne):** (1) commit AI-13.7 (land drop) after `/pre-commit` - tests and records
are in the tree; (2) the table's UI: TBL-2.17 (columns scramble when a creature leaves mid-combat) and TBL-2.18 (the
zones bounce and cut off the bottom) - captures over a turn, not one frame; (3) **AI-17.7, the blunder lint** - bring
Shayne the first four or five checkers' exact rules to approve BEFORE building any, then run it over a few hundred
games (`--decisions` records, or a new mode) and read the counts; (4) then AI-14.4 (F6 inspector), AI-13.8 (Saru, a
flash creature's protective enters) and AI-13.6. This session's lesson: the search finds exploits of its own model
(baiting the simulated opponent, a graveyard trick inside the horizon, ties that look free) - see
`docs/findings/magic-ai.md`, AI-17.10-14 and AI-13.7.


**AI-17.6, what is left.** Done this session: the quick check (review item 1, two levels, every pick), the
counter-attack always seen, graveyard access; item 9 (plans) kept after a match, item 10 (the table's cap)
and item 6 (Coursework) checked. Open: **item 11** (more plays a rollout turn) against the agreed #547 hold —
Shayne: "a little later"; **AI-9.3** walker pricing (the Chandra position, ignored) — a design talk first;
item 5 (creatures by stats — Shayne: a judgement call); the burn rule (deferred). Then AI-17.7's blunder
counters, each checker's rule agreed with Shayne first. **But first in the sprint: MC-5 (Star Trek) in a fresh
session.** `BeamSearchAi.Explain()` / `--explain` show what the search imagined: read it before naming a
cause (this session's guesses were wrong more often than right).

## Log

- 2026-11-08: AI-17.10 — cause found (the simulated opponent had no tricks in its own combat), fix behind
  `+notheircombat`, suite and tests green; the match is pending. `--explain` now ends each line with its
  evaluator terms ("Ends at: …"). Details in `docs/findings/magic-ai.md`.
- 2026-11-08: AI-19.21 (Chang's sacrifices) diagnosed in the same session — a second rollout-model fix,
  `+nodefender` (`Terms.TrueDefender`); its match is pending beside AI-17.10's. Findings: `magic-ai.md`.
- 2026-10-29: created from Shayne's two table games (LIM-2.3's playtest); both diagnosed on kept records.
- 2026-10-29: the plan agreed — the attack as one decision (Shayne); to the top of sprint 3.
- 2026-10-29: AI-17.2 — "you may" taken in the look-ahead; 51.0% over 192 games, kept.
- 2026-10-29: AI-17.3 (a), (c) and targets by their owner — kept; the oracle 87.2%, score given up halved;
  `--explain` built to find #547's cause.
- 2026-10-29: AI-17.3 (b) — the attack as one decision, the search's tree only (Shayne); 49.5% over 192
  games, +1.1% a decision; Chandra's walker case a known defect (AI-9.3). AI-17.3 done.
- 2026-10-29: AI-17.4 — the code read: six shortcuts likely to cause a miss, five possible
  (`docs/findings/magic-ai.md`, "The review, code"); the basics suite's positions extended from it.
- 2026-10-29: AI-17.5 — Shayne's answers; 11 positions built, 7 pass, 4 fail (ignored, AI-17.6 tasks).
- 2026-10-30: AI-17.6 — a finished game keeps its board (`EndBoard`): 49.5% over 192 games, kept. Life
  risk below 6, whole attacks in the look-ahead, every combination of a several-pick choice: each no
  worse over 96–384 games, kept (+22.7% a decision, the look-ahead's whole attacks). Option A for the Pinger
  tried and reverted (it breaks AI-13.4's hold); a tutor already fetches well; Chandra at 1 is AI-9.3's.
- 2026-10-30: AI-17.6 — the cheap look-ahead answers by its chooser's best (49.5% over 192, kept); graveyard
  access priced (Shayne); the counter-attack always seen (Shayne's rule; 50.0% over 384, +16.6%, kept) — the
  Deathbringer now sacrifices the Ghoul, the oracle 88.5%.
- 2026-10-30: AI-17.6 — plans kept (a fresh search at every decision 49.3% over 288, ×1.98 the cost); the
  table's 1 s cap and Infinite Coursework checked on positions.
- 2026-11-01: AI-17.9 noted — `Evaluator.Through` reads this turn's grants as next turn's (MC-5.3 batch 4a's
  review); to fix later (Shayne).
