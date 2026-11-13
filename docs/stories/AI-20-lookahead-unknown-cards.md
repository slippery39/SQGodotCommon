---
id: AI-20
title: The look-ahead stops betting on one guessed future — unknown cards handled honestly
status: backlog
area: AI
created: 2026-11-10
---

## Why

Shayne, 2026-11-10, after AI-17.24 (the landcycle skipped again): "I'm wondering if our lookahead should not count
unknown cards? This seems to be the main culprit for all these weird issues that are intermittently coming up. I.e.
Tasha, Land Cyclers, weird times to draw cards vs not." He asked for it to be the most urgent AI story: how many other
issues go away if the root cause is fixed?

**How the search works today.** Before each decision the search takes ONE guess at everything it can't see
(`Hidden.Sample`, AI-3): the order of both libraries (`ShuffleUnknown`, the default since AI-3.9) and the
opponent's unseen hand. It then plays the turn out and a rollout two turns past it (`Rollout.Horizon`) in that one
guessed world, draws included, and scores the end. Every move is compared inside the same guess. A different guess
can give a different decision.

**The symptom family — the same root each time, a separate patch each time:**

| Where | What the one guess did | The patch |
|---|---|---|
| AI-17.24, `Records/RogueLandcycleSkipped` #157 | the Pass line's guess drew an Island on turn 10, so keeping the 6-drop looked free; it passed with a landcycler on 3 lands, then cycled in Shayne's upkeep. Over 30 guesses: passes in 7 | none yet |
| AI-17.21, `ALandcycler_OnTwoLands…` | a Borg on 2 lands kept over a land | `Terms.ManaShort` (a discount), 18 → 7 of 30 passes |
| AI-17.21, `TheTashaAttack_DoesNotHangOnTheSampledTopCard` | one sampled land at the horizon swung a hand of 4-drops by 11 points | "one short is free" in `ManaShort` |
| AI-17.20, AI-17.11 | a surveil never binned: the rollout keeps the one guessed top card, so binning is never seen to gain | `Terms.SurveilLands`, `SurveilSpells`, `SurveilPrice` (rules beside the search) |
| AI-17.16 | "exile the top card, you may play it" judged by the guessed card ("it shouldn't matter what the exact top card is") | `Terms.ImpulseValue` |
| AI-13.10, AI-17.23 | the Jace −3 against Pass decided by the guessed draw (a Plains) | moved to a calm position |
| AI-19.23's scan, `Records/LintTrkSeed1` #48 | a basic landcycling already paid, its search answered "none" — fetching a land reorders the guessed library, so every imagined draw after it changes. Over 30 guesses (`--record=FILE --guesses=30 --at=48`): Plains 14, none 11, Island 5 | none yet |

Several tests already have to count a decision over 30 guesses, because one guess decides by luck
(`AjanisAnguish_WithNothingToKill_IsHeld`, the Tasha test, the Jace test, `ASurveil_KeepsAGoodSpellOnTop`).

**What has been measured (`docs/findings/magic-ai.md`, "AI-3's matrix"):** seeing the REAL library order is worth
about 5pp (`beam+realorder`), but it is cheating and Shayne chose the honest default (AI-3.9). The opponent's real
hand, its casts and its answers were worth nothing measurable. So what the look-ahead knows about hidden cards
matters, and how it guesses them is the open question.

## The approaches and their trade-offs

**A. Known information only (Shayne's first idea).** The look-ahead plays only with what is on the board and in
known hands. A card drawn in the look-ahead is not a real card from the library: it is an "unknown card", worth the
average card (`HeldValues.Unknown`, as the opponent's unseen hand is priced already), and can't be cast inside the
look-ahead.
- **Gains:** no luck. The same position gives the same decision, every time. The whole family above goes, and with it
  probably most of the patches (surveil, impulse, the Tasha "one short is free" rule). It is cheaper (no sampling).
  The AI's plays become explainable from what is on the table.
- **Costs:** the future has no draws in it. The AI never expects to draw a land, so it would undervalue a hand that
  is one land short (the Borg at 4 lands, which Shayne said is fine to hold), and overvalue fetching lands. A
  drawn card can't be cast, so "draw a card" is worth only an average card, never the play it enables. The
  opponent never draws a threat, so their future is understated. Surveil, scry and "look at the top" need rules
  of their own (some exist). And the 5pp the real order gave says future cards do matter.
- **Variant A+ (an expected draw):** a drawn card is "an unknown card", but with its expected make-up: a land with
  the chance of the lands left in the library, counted toward mana (for `ManaShort`, `LandUnlock`). This answers the
  land-draw cost above without guessing which card. It does not let the AI cast what it draws.

**B. A fixed library (Shayne's second idea).** The look-ahead uses one fixed order. If it is the REAL order, the AI
sees its own draws: that is `beam+realorder`, stronger by 5pp but cheating, and set aside at AI-3.9. If it is one
guessed order fixed for the whole game, the AI is at least consistent from one decision to the next (no "pass on
turn 8, cycle in the upkeep" flip-flop).
- **Gains:** consistency. Simple.
- **Costs:** a fixed guess is still one guess. In AI-17.24 the bad guess would just stay bad for the whole game.
  The real order is the cheat Shayne rejected. This does not fix the root cause.

**C. Several guesses, averaged.** Decide over N guessed worlds (say 4 to 8) and play the move that is best on
average. This is the textbook answer in card-game AI (Perfect Information Monte Carlo).
- **Gains:** keeps the rich future (real-looking draws, the land curve, casting what it draws) while averaging out
  the luck. In AI-17.24, 23 of 30 guesses cycle, so the average would cycle.
- **Costs:** N times the work for each decision, or a narrower beam to pay for it. The table's ~1 s cap gets
  tight. A known flaw: in each world the AI "knows" its next draw, so draw timing and surveil can still be judged as
  if it knew. It reduces the noise but doesn't remove it. It averages the luck away instead of not betting on it.

**D. Mixed: known cards in our turn, average draws after.** Our own turn is played with what we know (A). Draws
after it are unknown cards with an expected make-up (A+), maybe with a few guesses only for what the opponent holds.
- **Gains:** most of A's honesty, with a future that still has lands in it.
- **Costs:** the most design. Two kinds of "unknown" to keep consistent.

**Claude's lean (2026-11-10, for Shayne to decide):** A+ first. It is Shayne's idea with the one gap that matters
most fixed (expected lands), and it is the one that removes the root cause rather than averaging it. Then measure:
if the match says the AI got weaker (the 5pp warns that future cards matter), C is the fallback, and it can be
built on top.

## Requirements

- [ ] One measure of the problem before any change: on a fixed set of positions (the suite's 288 plus the kept
      records above), how often the decision changes between guesses (the "flip rate", over N guesses each).
      Every approach is judged against it.
- [ ] The chosen approach is a switch (a `beam+…` name), so it can be matched against today's search.
- [ ] The kept positions above decide right with it, each with its control.
- [ ] Patches made redundant are tried off one at a time and dropped only when measured redundant.

## Decisions

- 2026-11-10, Shayne: this is its own story and the most urgent AI story. It starts with the discussion and
  trade-offs above.
- 2026-11-10, Shayne: the principle - **known information is used in the look-ahead, unknown information stays
  unknown**: "any known information should be allowed to be used in the lookahead, any unknown information is
  unknown. Thats the way I would try it." So a card already seen on top (a surveil's keep, a "look at the top") is
  drawn and may be played as itself; a card nobody has seen is an unknown card. This is approach A's family.
- 2026-11-10, Shayne: **land odds, in principle (A+)** - "Land odds is fine, gives us a % chance of hitting lands for
  certain turns, then the AI can make decisions based on that." The details are to be discussed when the story
  starts: "I think we should discuss that more when we start the story."
- 2026-11-11, Shayne (Claude's proposal): **AI-20 goes right after the misplay scan (AI-19), before the scan's value
  fixes** — the scan decides each suspect under 30 guesses (AI-20.2's flip rate, built there once) and the NOISE ones
  become this story's positions; the value fixes are then measured once, on the look-ahead kept. Shayne's note that
  started it: an AI opponent skipped its land drop and landcycled in his upkeep, "for no real reason" — one guess
  drew the card it needed, another didn't.
- 2026-11-12, Shayne: **AI-22 (speed) goes first**, limited to what AI-20 won't rewrite; AI-20 then picks the
  search's shape (several guesses multiply its cost). Research first (sprint 5): Forge's AI and the published work on
  hidden information in card-game search (determinization ensembles, information-set MCTS) — a findings note.

Open questions (for Shayne, before `ready`):
- Land odds, in detail (Shayne: discuss at the start): how the % chance of hitting lands by a given turn reaches
  the AI's decisions - which prices read it (`ManaShort`, `LandUnlock`, casting a card it would draw into).
- Does the opponent draw unknown cards too, priced the same way, or nothing?
- What counts as done: the flip rate down, the kept positions right, and a match not worse?

## Tasks

- [ ] AI-20.1 Shayne picks the approach and answers the open questions - the principle decided (known used, unknown
      unknown) and land odds in principle; the land-odds details and the opponent's draws open, to discuss at the start
- [ ] AI-20.2 The flip rate: a console mode or a suite option that decides each position under N guesses and
      counts the changes, run on today's search (the baseline). Uses `RunProgress`
- [ ] AI-20.3 Build the chosen approach behind a switch
- [ ] AI-20.4 Measure: flip rate, suite, the kept positions (AI-17.24's needs its test: `RogueLandcycleSkipped`
      #157, kept but uncommitted on 2026-11-10), then a match
- [ ] AI-20.5 Retire the patches it makes redundant, each measured

## Next

Start at AI-20.1: the approach is decided in principle (known information only, with land odds - Decisions); begin by
discussing the land odds' details and the opponent's draws with Shayne. Read the table of symptoms above and "AI-3's matrix" in
`docs/findings/magic-ai.md`. The guess is made in `MagicCore/Ai/Hidden.cs` (`Sample`) and used by
`BeamSearchAi.Decide`; the look-ahead is `Rollout` (horizon: two turns past this one). The record
`MagicCore.Tests/Records/RogueLandcycleSkipped.json` exists, uncommitted, with no test yet.

## Log

- 2026-11-10: created from AI-17.24 (Shayne): the landcycle, Tasha, surveil, impulse and Jace issues share one root,
  the single guessed future.
- 2026-11-12: sprint 5 — after AI-22; the research note first.
- 2026-11-12: the research note — Cowling et al. on MTG (several guessed futures, one budget split), ISMCTS, Forge — in `docs/findings/ai-research.md`; its questions are AI-20.1's.
