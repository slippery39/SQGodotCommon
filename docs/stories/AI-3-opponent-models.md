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
- [ ] Each measured: head-to-head against the others and against `GreedyAi`, AND its cost (AI-6's
      count: engine steps and rollouts per decision) — recorded side by side in `docs/findings/magic-ai.md`
- [ ] Our own side's rollout policy is swappable through the same seam (cheap greedy vs `GreedyAi`)
- [ ] Hidden information in the library has a second mode beside AI-2's "real order": **shuffle only
      what isn't known**. Needs the engine to record which library positions a player knows — surveil
      keeps, "put on the top / bottom" (Loyal Tutor, Paradox Shaper, Identity Echo's bottom), mulligan
      bottoms — cleared when the library is shuffled; and which hand cards the other player knows
      (revealed, or returned from a public zone), for the hand modes above
- [ ] Every combination worth running is measured the same way: strength AND cost per decision
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
- 2026-10-18, Shayne: **the library mode's default is chosen at AI-3.9**, with the hand modes in —
  `RealOrder` (3–6pp stronger, sees the real library) stays the default until then.
- 2026-10-19, Shayne (AI-3.4): **the sampled hand swaps in place** — a card drawn from the
  opponent's library takes the place of the hand card it replaces, the rest of its library keeps its
  order, so the hand mode is measured apart from the library's. **Measured by the suite, then
  `--until-clear`** against `beam+casts` (the hand matters only to a casting opponent).
- 2026-10-19, Shayne (AI-3.5): **the deck model is its colours, by rarity** — cards of the colours
  the opponent has shown, weighted as a booster weights rarity (honest in limited); **lands from
  what's left** of a 40-card, 17-land deck. Defaults, not asked: "shown" is its own cards (by owner)
  on the battlefield and the stack, in its graveyard and exile, and the hand cards and library places
  the searcher knows (tokens and copies aside), a land showing the colours it makes; nothing coloured
  shown is every colour; a guessed land is a basic of a shown colour; a booster is 10 commons, 3
  uncommons, a rare or (1 in 8) a mythic, each slot over that rarity's nonland cards; a guess
  replaces its real card in place, keeping its id (new ids would move every later id away from the
  real game's and lose committed plans), so the library stays real.
- 2026-10-19, Shayne (AI-3.6): **full responses** — the simulated opponent (only; our rollout
  policy stays sorcery speed) responds **while one of our spells or abilities is on the stack and in
  our combat** (after attackers, after blockers); a response **must beat passing**, both settled;
  **no limit** on how many. Default, not asked: responses are spells (instants, flash) — not
  activated abilities, which a free one could repeat without end.
- 2026-10-19, Shayne (AI-3.7): **GreedyAi decides every decision of ours in a rollout** (plays at
  any speed, choices), **with no per-turn play limit**, measured against **`beam`** (the policy is
  its own axis). The casting opponent keeps the cheap policy (2026-10-17's default, below).
- 2026-10-17 (default, not asked): the casting opponent uses OUR cheap policy, not one of its own —
  symmetric, so a measurement isolates the model. "Our policy" (AI-3.7) swaps our side only.

## Tasks

Each new value gets a `--match` name so it can be measured alone (AI-2's rule: every behaviour
change has a switch that turns it off). One setting per axis, added with its second value — never
an enum of one (AI-2.4's review removed two).

- [x] AI-3.1 Opponent casts from hand (its real hand): the opponent-model setting, `OpponentStep`
      running `OurPlay` for the opponent on its turn; measured against `beam` and `greedy`, time beside.
      84–72 head-to-head (1.3 SE, not shown stronger), +41% time; `BoardOnly` stays the default (d95b89f)
- [x] AI-3.2 Engine: who knows what — library positions a player knows, and hand cards the other
      player knows; set by every FRA source, cleared on shuffle. A test per source (an inert
      primitive throws no error — test the consequence) (0a09d27)
- [x] AI-3.3 Library: "shuffle what isn't known" — both libraries, at the decision's root; measured
      with `BoardOnly` and casts-from-hand. Costs 3–6pp against the real order (72–84, 69–87), +6–8%
      time; still 106–50 against GreedyAi. The default is chosen at AI-3.9. Settled by AI-6.5:
      `--until-clear` stopped at 288 games, 130–158, WORSE by 5pp or more
- [x] AI-3.4 Hand: shuffled from its own unseen cards — the hand setting's second value; measured —
      `+unseen`: NO difference of 5pp against the real hand (384 games, 189–195); the default at AI-3.9
- [x] AI-3.5 Hand: guessed from a deck model — the third value; measured — `+guess` (its colours, by
      rarity): NO difference of 5pp against the real hand (384 games, 190–194), 5% cheaper
- [x] AI-3.6 Full responses: the opponent acts at instant speed inside our turn when it scores
      better; measured — `+responds`: NO difference of 5pp against `beam+casts` (288 games,
      145–143), +3.7% cost
- [x] AI-3.7 Our policy: `GreedyAi` as our rollout policy beside the cheap greedy; measured —
      `+greedyrollout`: UNDECIDED at 600 games, 317–283 (52.8% ± 1.4pp, worse ruled out), +61% cost
- [ ] AI-3.8 Combat: attackers judged together (MtgSimulator's `FindLethalAttacks`, or all-in
      against each alone) and a trigger's target by its owner's answer; measured
- [ ] AI-3.9 The matrix: every combination worth running, side by side in findings; pick the default

## Next

**Resumed 2026-10-19 after AI-6 and AI-7.** Measuring is now minutes: the quick check
(`--suite=<ai>`, ~20 s) says what a change alters; the early-stop match (`--match=A,B --until-clear`)
says whether it costs or gains 5pp — AI-3.4's took 9½ min (384 games), a fixed 156-game match 4½ min.
Commands.md, "Measure an AI change — two speeds". The cost count (engine steps, rollouts per
decision) is the time measure, not the clock.

Done so far: casts from hand (AI-3.1, not shown stronger), who knows what (3.2), library shuffle
(3.3: WORSE by 5pp, settled by AI-6.5), hand from unseen cards (3.4: no difference), hand guessed
from its colours (3.5: no difference, 5% cheaper), full responses (3.6: no difference, +3.7%),
GreedyAi as our rollout policy (3.7: +2.8pp, undecided at 600 games, +61% cost).
Defaults stay `BoardOnly`, `RealOrder` and the real hand until AI-3.9 chooses.

**Next task: AI-3.8, combat** — attackers judged together (MtgSimulator's `FindLethalAttacks`, or
all-in against each alone) and a trigger's target by its owner's answer; measured. Interview Shayne
first: which of the two (or both, as separate settings), and against which baseline. The rollout's
combat is `Rollout.Answer` (each attacker and blocker judged alone, settled) and `Lookahead.QuickAnswer`
(the first legal target). AI-7 measured combat judging at 41% of the search — a costlier combat
policy shows in the cost count. Then AI-3.9: the matrix and the defaults.

Notes still true:
- The oracle shares beam's models and its board-only opponent, so it can't rank anything about the
  opponent's hand or casting — those go to the match.
- A sample of hidden cards must come from a COPY of the state's RNG (`TakeRng` advances the seed),
  or searching changes the real game's draws; `Hidden.Sample` seeds its own `Random` from the state.
- The tree's own opponent decisions and the rollouts must see the SAME sampled state: sample once at
  the root, before the tree, not inside `Rollout.Score`.
- The evaluator prices the opponent's hand blind already (`HeldValues.Unknown`); a hand mode changes
  what the opponent DOES with its hand, not how the hand is scored.
- Deferred from AI-3.3's review: `Hidden.ShuffleUnknown` is a third copy of "out and back in" (with
  `Shuffle` and Identity Echo — one `ReorderLibrary` helper would do; `ShuffleUnseenHand` is a
  fourth, over two zones). Known-card gaps (AI-3.2): Theorist's Sanctum's behold reveals nothing;
  Identity Echo's random bottom is not known; Pia's and Fblthp's `Reveal` have no test of their own.
- `MoveKey` groups hand cards by name: a revealed and a hidden copy are one move. Moot for the
  sampled opponent (rollouts never re-sample, and our own hand is never sampled).

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
- 2026-10-18: AI-3.2 — `Card.KnownTo` (`Knowers`: None / Owner / Opponent / Everyone, relative to the
  owner). `MoveCard` sets it on every move: from a public zone into a hand or library → Everyone
  (bounce, regrowth, Paradox Shaper, Clash of Elements, Plan for All Outcomes); hand ↔ library keeps
  what was known, plus the owner into its library (mulligan bottoms, Loyal Tutor's top). `Shuffle`
  clears the library's. `s.MakeKnown` for the rest: surveil's look (owner), `SearchLibrary.Reveal`
  (landcycling, Fblthp, Pia, Loyal Tutor — `RevealChosenAction`), "reveals their hand" (Solve for
  Disappointment, Null Summoner, Stinging Vitriol), Identity Echo's revealed bottom, behold from the
  hand. No measurement: nothing reads it yet (AI-3.3 does). /pre-commit fixes: a search took a
  card's library knowledge into the hand (now only a draw from the top keeps it, and in a hand only
  the opponent's bit counts); Identity Echo marked its random bottom known — a miss would have kept
  the whole library's real order; the behold test called `Pay` directly (now a real Countersculpt
  cast); a shuffle's extra pass and no-op updates gone. Thirteen tests in `FraKnowledgeTests`, ten
  rules each revert-checked. MagicCore.Tests 940 green.
- 2026-10-18: AI-3.3 — `Hidden.Library` (`RealOrder` default, `ShuffleUnknown`) on
  `BeamSearchAi.Settings`; `Hidden.Sample` builds the position the search decides from at its root:
  both libraries reshuffled where the decider can't know a card's place (its own: `KnownTo & Owner`;
  theirs: `& Opponent`), from a seed of the state's, and the sample gets its own RNG seed so a
  rollout's shuffle can't predict the real one. Left alone while a choice offers library cards of
  unknown place (a search lists the first copy per name). `--match` combines models:
  `beam+casts+shuffle`. Seven tests in `HiddenTests` — one plays the same seeded game in both modes
  and requires them to differ — five rules revert-checked. Measured (findings): **72–84 vs beam
  (46.2% ± 2.7pp), 69–87 with the casting opponent (44.2% ± 2.9pp), 106–50 vs GreedyAi (67.9%)**;
  +6–8% time. MagicCore.Tests 947 green.
- 2026-10-18: /pre-commit fixes to AI-3.3 — the choice guard now compares the sample's offered ids
  with the real choice's (it checked library cards only, so a known card mid-library, or a number
  equal to a card's id, slipped past); the "same sample all turn" comment was untrue (marked
  `ponytail:`); `beam-casts` throws if its model is renamed; a test half that could not fail is gone;
  the MagicCore map, its MoveObject rule and DesignNotes' real-order ceiling brought up to date.
- 2026-10-18: paused by Shayne after AI-3.3 — the runs take too long; pivoting to other work.
- 2026-10-19: resumed after AI-6/7. Requirements: "time per decision" → the cost count (AI-6's
  decision). AI-3.3 settled by AI-6.5 (WORSE by 5pp, 288 games).
- 2026-10-19: AI-3.4 — `Hidden.Hand.ShuffleUnseen` (`+unseen`): the opponent's hand cards the
  searcher hasn't seen drawn again from those and its library's unknown-place cards, swapped in place
  (Shayne). Tests: seen cards and known places stay, the rest is drawn from the unseen, nothing of
  ours moves, the rest of the library keeps its order, the search plays a real game differently with
  it (revert-checked: sampling off fails all three). `beam` and `beam+shuffle` unchanged on the suite
  (0 and 24 changed). Measured (findings): NO difference of 5pp, 384 games, 9½ min. 979 tests.
- 2026-10-19: /pre-commit fixes to AI-3.4 — `beam+unseen` without `+casts` is refused (the
  board-only opponent never reads its hand: it would measure only the reseed); MagicCore/CLAUDE.md's
  MoveObject rule names the hand swap (a hypothetical — a real zone change's events must not fire)
  and the source map the hand mode; findings say both AI-3.3 and AI-3.4 also take away the real
  game's random seed (foresight of a random discard or order) — not separated. Kept the RNG use when
  no hand card is unseen, so `+unseen` is the version measured (the suite: still 37 changed).
- 2026-10-19: AI-3.5 — `Hidden.Hand.GuessedFromColors` (`+guess`), interviewed first (Shayne: its
  colours by rarity; lands from what's left). Review found the first cut's guesses took new ids —
  NextId moved, so a plan naming a later object was lost; that match was stopped, and a guess now
  keeps its card's id (`UpdateObject`). Also: the stack counts as shown, cards by owner, rarity slots
  over nonland cards; the 40/17 FRA assumption is a ponytail (DesignNotes). Tests: shown colours only,
  a seen card stays, nothing else touched, no land once 17 are shown, ids and NextId kept, a real
  game played differently — revert-checked four ways. Measured (findings): NO difference of 5pp, 384
  games, 10 min; 5% cheaper. 984 tests.
- 2026-10-19: AI-3.6 — `Rollout.OpponentModel.Responds` (`+responds`), interviewed first (Shayne: our
  spells and combat; must beat passing; no limit; opponent only). Review found the combat window
  opened with no attack declared (fixed: `Combat.Attackers(s).Any()`, as `Stops` guards it) — that
  match was stopped; "a response's target is the first legal one" was checked and is wrong (each
  target combination is its own cast candidate). One `BestSettled` for our policy and the response;
  one model factory in `AiMatch`; the name rules tested (`casts`/`responds` exclusive, a hand mode
  needs one). Tests: it kills our creature in response, CastsFromHand doesn't, it passes when nothing
  beats passing, combat answered only when we attack — revert-checked three ways. `beam+casts`
  unchanged on the suite (35). 995 tests.
  Measured (findings): NO difference of 5pp, 288 games, 8½ min; +3.7% cost.
- 2026-10-19: AI-3.7 — `Rollout.OurPolicy.Greedy` (`+greedyrollout`), interviewed first (Shayne:
  every decision of ours, no play limit, against `beam`). `GreedyAi.Play` is its decision without the
  description (none needed inside a rollout). Review: the model was first named `greedy` — the same
  word as the AI (`--match=greedy`), so a dropped `beam+` would run the wrong thing silently; renamed
  (the match ran under the old name, the same code). One `WithRollout` for every rollout model. Test:
  two creatures in one turn where the cheap policy casts one (revert-checked). 996 tests. Measured
  (findings): UNDECIDED at the 600-game cap, 317–283, +61% cost — run approved past 15 min (18 min).

