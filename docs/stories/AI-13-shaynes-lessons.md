---
id: AI-13
title: Shayne's lessons as general rules for the search — card-agnostic, switchable, measured
status: in-progress
area: AI
created: 2026-10-24
---

## Why

Shayne (2026-10-24): Magic is complex enough that years of play teach lessons a simulated game
state won't find on its own — when to hold Unsummon (AI-4's cases), when the right time to play a
card comes. Rather than only hoping the look-ahead finds the line, try coding the lessons in. "I
think it might be a good thing to test out."

The look-ahead can't learn these by itself: it sees two turns, its simulated opponent plays the
board only (AI-10), and our side casts one spell a turn (AI-8.2). A lesson that comes from
thousands of games is exactly what a two-turn search won't rediscover.

## Requirements

- [x] A **lessons ledger** in this story: each lesson in Shayne's words, the general rule it
      becomes, and its status (proposed, built, kept, dropped — with the measurement)
- [x] Each lesson is a **general rule, never a card's own script** — it reads what a card does
      (an instant, a bounce, a token, an Aura), never its name — so it holds for every card and set
- [x] Each is its **own switch** (as `Evaluator.Terms` and the rollout models are), so it can be
      measured on and off
- [ ] Each is pinned by positions where Shayne agrees the right answer (AI-4.2's for the bounce
      cases), and **kept only if it doesn't lose games** — the AI-6 way: `--suite`, then
      `--match … --until-clear`; a rule that wins its example but loses overall doesn't ship

## Decisions

- 2026-10-24, Shayne: create the story; worth testing.
- 2026-10-24, Claude (proposed, open to change): **general rules, not per-card logic.** Forge, the
  open-source Magic AI, writes "when to play me" per card: it plays decently, but every new card
  needs new code and the rules quietly contradict each other. This repo already refuses a special
  case per card phrase (MtgCore's lesson). A lesson becomes a rule that steers the search — a
  penalty, a bonus, or a move it doesn't consider — and the search still decides.
- 2026-10-24, Claude (proposed): **how this splits with AI-4.** AI-4 COMPUTES the price of holding
  a spell (plans, held spells as options); AI-13 tries the RULES route on the same problems. Both
  are measured on the same positions, and a rule that turns out to be a cheap stand-in for AI-4's
  computed answer is fine until AI-4 lands.

- 2026-10-28, Shayne (AI-13.1): **the first rule is patience with instant-speed cards** (lessons 1,
  4, 5): a card that can be cast later at instant speed (an instant, or flash) isn't cast now unless
  now beats waiting; **it steers as a HOLD VALUE** — such a card is worth more held than a
  sorcery-speed one, so casting now must beat holding by that much, and a big swing (desperation, a
  blowout) still casts. Card-agnostic: it reads instant speed, never a name. Its own switch, measured.
  Positions: Ferocity of the Hunt with Last Gasp unseen (today a TIE, Pass 5.40 = cast 5.40, and the
  tie casts) and #547 (passes since AI-10.4's known-card opponent — kept as a guard).

Open questions:
- How a rule steers: a score adjustment the search adds, a move left out of the search (pruning),
  or an ordering hint (searched first)? Pruning is the cheapest and the most dangerous — a pruned
  move can never be chosen, even in the exception.
- Where rules live: one file of named rules, each a switch, beside the evaluator?
- Which lesson first — Shayne's pick.

## The lessons ledger

| # | Lesson (Shayne) | General rule (proposed) | Status |
|---|---|---|---|
| 1 | Bounce and removal: wait until they give real value, or use them in desperation (AI-4, Decisions 2026-10-24: his value and desperation cases) | **Patience with instants:** don't cast an instant now if casting it later does as much or more — unless the alternative is losing (desperation) | **built, kept** (AI-13.2: a cost on casting an instant or flash card at sorcery timing) |
| 2 | Unsummon's value cases: a token, an Aura or counters, a creature in a double block, your bomb under removal, an attacker at a planeswalker that matters, a big creature for tempo (AI-4) | **What a bounce costs them:** a bounce is worth what is lost when the creature leaves play — a token is gone, Auras and counters are gone, the mana is paid again, a combat is won — computed for any card | proposed |
| 4 | At #547 (AI-4.2's holding position, 2026-10-27): don't bounce what you can trade with in combat; a recast gives them its "enters" again (Fateshaper returning a creature from their graveyard); a creature already stunned or tapped needs no answer now — "at the very latest wait a turn"; at 5 life "I can still take a hit"; hold for their bad attack, when Unsummon plus a trade wins two for one "when they don't expect it" | Part of lesson 1, sharpened: **an instant cast now must beat holding it** — beat what combat already does (a trade), not hand them an "enters" again, and not answer what can't hurt you this turn | proposed |
| 5 | On AI-10.4's hand-built position (2026-10-27): "ferocity of the hunt is best held until the opponent plays their removal spell first, or if you can trade it with a creature. So the AI shouldn't have been playing this out at first possibility anyways" | Lesson 1 for an Aura or pump (Ferocity has flash): **hold a card that makes one creature better until their removal is spent, or until it wins a combat now** (a trade it makes possible) | waits now (AI-13.2); "until their removal is spent" not yet asked |
| 3 | Hard removal: a 1-for-1 — weigh the threat against tempo and against holding for a better target (AI-4) | needs a target's threat over time (AI-12) | waiting on AI-12 |

New lessons go here as Shayne thinks of them — in his words first, the rule after.

## Tasks

- [x] AI-13.1 Shayne picks the first lesson; agree how a rule steers (open questions) — 2026-10-28: patience with instant-speed cards, as a cost on casting early (Decisions)
- [x] AI-13.2 The first rule behind a switch, its positions (AI-4.2), measured; kept or dropped in
      the ledger — **patience, kept** (2026-10-28): `Evaluator.Terms.Patience` — an instant or flash
      cast at sorcery timing costs `Evaluator.PatienceCost` (1.0), charged by the search's tree on
      every such cast in a line (`BeamSearchAi.Impatience`) and by our rollout policy
      (`Rollout.Impatience`), none on a line that ends the game. Tried first as a hold value on the
      card in hand (Shayne's pick) — it did nothing: the best line after "Pass" cast the flash card
      later that turn; he chose the cost on the act. Charged in the tree only, it was a one-turn delay
      (the review) — so the rollout waits too. Positions: `AFlashAura_IsHeld_NotCastAtTheFirstChance`
      (Ferocity, Last Gasp unseen: a tie that cast, now held; control without patience) and
      `Unsummon_IsHeld_AtMove547` (a guard; their turn, so patience doesn't touch it). Suite: 4
      decisions differ from no patience. Match: **53.0% ± 1.0pp, 600 games (318–282)**, undecided at
      5pp, "worse" rejected, cost the same — kept (Shayne's rule: sensible plays count; the agreed
      position fixed). `docs/findings/magic-ai.md`
## Next

The first rule — patience with instant-speed cards — is built and kept (AI-13.2). Lessons 4 and 5
are its cases; their parts it doesn't cover yet: "don't bounce what you can trade with in combat",
"a recast gives them its enters again", and holding an Aura until their removal is SPENT (it waits
now, but never asks whether removal is still coming). Next: Shayne picks the next lesson (2, "what a
bounce costs them", is partly done by AI-9.5 and AI-10.4), or a sharper version of this one (its
cost scaled by what the card does at a later window — AI-4's held spells). `PatienceCost` (1.0)
was chosen, not measured: try 0.5 and 2 with the suite if a hold looks wrong.

## Log

- 2026-10-24: created — Shayne's idea; machine learning for the AI noted as a later option in
  `DesignNotes.md` ("MagicCore AI: machine learning, later").
- 2026-10-27: lesson 4 from Shayne's reasons at #547 (AI-4.2's first agreed holding position).
- 2026-10-27: lesson 5 from AI-10.4's position (the AI casts Ferocity of the Hunt at the first chance).
- 2026-10-28: AI-13.1 interviewed, AI-13.2 built and kept — patience with instant-speed cards
  (53.0% ± 1.0pp over 600 games, worse rejected; the agreed position held). Status → in-progress.
