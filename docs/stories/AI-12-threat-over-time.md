---
id: AI-12
title: A card's threat over time — what it does if it isn't answered, on an empty board and a busy one
status: backlog
area: AI
created: 2026-10-24
---

## Why

Shayne (2026-10-24): the AI needs a value, a threat level, for a card — and a threat isn't one
number. Some cards are most threatening EARLY (Ragavan, Ocelot Pride: nobody holds a Lightning Bolt
for a better target when one lands); some the moment they come into play (Griselbrand, Emrakul).
A 1/1 first striker or a vanilla 2/1 may not be worth a Bolt at all — it depends on our hand and how
much damage we expect to take before a blocker or a lesser removal spell stops it. With two 4/4s
of ours in play, a vanilla 2/1 is not worth a Bolt.

Tempo cuts across it: bolting the 2/1 for one mana may be right because the mana would otherwise go
unspent — and how much an unspent mana costs depends on the format. In a fast one, not spending
mana on turn 1 can lose the game; in a slower one you will likely find a use for it later without
being punished much.

**What exists, and why it doesn't answer this:**
- `CardValues` (AI-1) — a card cast into a FIXED board and measured to the end of that turn, no
  attacks: value that accrues later isn't counted (DATA-2: every planeswalker reads last).
- The threat term (AI-5) — a clock over the board as it is now, not what a card will do over turns.
- Held spells as options (AI-5's Why, AI-4) — the price of waiting needs to know how much a target
  will cost us while it lives. That is this story's number.

## Requirements

- [ ] A card's **value over time**: put into play for its controller and played for X turns —
      attacking, its abilities used — with the damage and other value it provides, turn by turn (a
      curve, not one number). Shayne's two settings:
      - **an empty board**: what happens if I can't remove this for X turns
      - **a busy board**: a few creatures on each side, or a sample state chosen as the one most like
        the current game
- [ ] The AI's removal and bounce decisions read it: a target's threat is its curve in a board like
      this one (AI-4's held-spell price consumes it)
- [ ] Checked on cards whose answer isn't in doubt: Ragavan-like early threats rank high early, a
      vanilla 2/1 against two 4/4s ranks low, and **a 1/1 deathtouch facing their 5/5 is worth what
      it does, not its board score**: the damage it stops for several turns (life it effectively
      gains), or the removal spell it forces them to spend before they can attack

## Decisions

- 2026-10-24, Shayne: capture it as a story; the idea and the examples above are his.
- 2026-10-24, Shayne: **value is what a card makes happen, not the board it leaves** — a 1/1
  deathtouch scores little on a board, but if it keeps a 5/5 home for several turns it has gained
  you that much life, and it may force a removal spell. Grading only the resulting board misses it.
- 2026-10-24, Shayne: **win rates are for deckbuilding; gameplay takes the state into account.** A
  card's win rate measures it in the abstract — fine for building a deck (DATA-2, the sealed
  builder) — but in a game your best card may not be the one to play now. So the AI never decides a
  play by a context-free number: a card's threat is read in a board like this one. (Claude had
  proposed checking the in-game measure against card win rates; withdrawn.)

Open questions:
- How many turns X, and does it depend on the turn of the game or the format?
- What counts as value: damage only, or cards, life, mana and board too — and on the evaluator's
  scale? Damage stopped is worth more under pressure (AI-9.2: life by what it buys) — so perhaps in
  turns bought on their clock rather than points of life.
- The counterfactual is only as good as the simulated opponent: deterrence shows only if its attacks
  see the deathtouch blocker (the look-ahead's blocks do — `Lookahead.Survives`, AI-8.3), and "forces
  a removal spell" only if it casts removal at all — the board-only opponent never does (AI-10).
- The empty board: does the other side do anything (block, cast)? The busy board: a fixed fill, or
  sample states — from where (the position suite's 288 positions, game records)? Matched to the
  current game how?
- **The busy board's trap** — below, under "The trap". A busy board has to be built so the card's
  own contribution survives.
- **Precedents (MtgSimulator):**
  - two sandbox arms, a passive board and one under pressure
    (`CardValueSandbox.Lookup(values, underPressure)`), chosen per position from the race term —
    intended, never built (`.claude/rules/sim-ai.md`, "Card values in ResolveChoice")
  - **`OutputProbe`** (`MtgSimulator/Evolution/OutputProbe.cs`; `docs/findings/evolution.md`, "SOLVED
    by changing the substrate") — close to Shayne's empty board: solitaire against a seat that
    CANNOT LOSE (`CannotLoseComponent`), measuring accumulated OUTPUT over N turns (damage 1,
    permanents 0.5, cards 0.25 — deliberately crude, untuned) instead of a position score. Damage is
    an outcome, so nothing in the evaluator can hide it, and the game never ends, so a longer
    horizon adds up instead of cancelling. It inverted a ranking the right way (Timberwatch Elder,
    last by win rate in random decks, first in its shell). Its warning: a short horizon favours
    immediate damage over a slow engine, so the horizon is a parameter and its effect is recorded.
    Built for MtgCore (no blocking) — not tried on MagicCore's real combat.
- Format speed: whose number is the price of an unspent mana — this story's, AI-4's, or AI-5's
  "kind of game"?
- Beside `CardValues`, or replacing it? DATA-2's deckbuilding rating is a separate question — a
  value over time might help it (planeswalkers read last today), but gameplay never reads a
  context-free rating (Decisions, 2026-10-24).

## The trap — a busy board measured combat, not the card (2026-10-16, AI-1.6)

`docs/findings/magic-ai.md`, "The card-value sandbox: what to measure"; `CardValues.cs`'s header.
AI-1 first ported MtgSimulator's card-value sandbox as it was: cast the card into a fixed board, roll
**four turns** forward, and compare the score with the same board without the card. The board was a
vanilla ladder on EACH side — a 1/1, 2/2, 3/3, 4/4 and 5/5 — so removal had targets of every size.
Probed over all 284 playable FRA cards:

| Measured at | Cards at exactly 0 | Cards negative | Rampart Hunter (3/3 deathtouch) | Artifist Acumen (first strike, a card) |
|---|---|---|---|---|
| rollout, 4 turns | 9 | 2 | **0.0** | −19.1 |
| rollout, 2 / 1 / 0 turns | 10 / 12 / 8 | 2 / 3 / 6 | 0.0 / 0.0 / −1.7 | −14.1 / −14.1 / −11.6 |
| the cast's turn, no attacks (kept) | 13, all honest | **0** | **+10.3** | **+0.3** |

**What went wrong:** the rollout's combat traded the two ladders away — both sides attacked and
blocked until the boards were gone — and the extra card went down with them. The difference the
card made was gone by the time the score was read: a 3/3 deathtouch was worth EXACTLY nothing at
every horizon, and a card that gives first strike and draws a card was worth LESS than no card.

**Why it went that way:**
- **A symmetric board cancels.** Both sides had the same creatures, so most of what happens is a
  wash, and the card is a small difference inside a big exchange.
- **The rollout's combat is crude.** Each attacker is judged alone (an attack that only works
  together is never found), and a trigger's target is the first legal one — so a card's subtle
  edge (deathtouch deterring a block, first strike winning one) is lost in the policy's noise.
- **A position score at the END can't see what happened on the way.** Damage dealt, a creature
  traded for two, cards drawn and spent — gone once the board is empty.
- **MtgCore has no blocking**, so MtgSimulator, whose sandbox this was, never met it.

**The fix then** was to stop rolling: cast the card, play the turn out with NO attacks (so end-step
effects settle), and score the position as the next turn begins. That's why `CardValues` counts only
the turn of the cast — and why AI-12 exists.

**What a busy board for AI-12 must do differently** (ideas, for the interview):
- measure the card's OWN output — damage it deals, blocks it forces or wins, cards and mana it
  makes — not the whole board's end score (OutputProbe's lesson above);
- an asymmetric board, from a real position (Shayne's "sample state most like the current game"),
  so the boards don't simply cancel;
- the same board with and without the card, paired over several samples, and the curve read turn by
  turn rather than at the end — so a creature that dies in the third turn's combat still shows what it
  did in the first two;
- check it on the cards the trap broke: Rampart Hunter must measure above nothing.

## Tasks

- [ ] AI-12.1 Interview the open questions

## Next

Interview first. Read `MagicCore/Ai/CardValues.cs` (its header: the fixture, and why a rollout was
dropped), AI-5's story ("A held spell is an option"), AI-4, DATA-2, and `.claude/rules/sim-ai.md`
("Card values in ResolveChoice": MtgSimulator's two arms).

## Log

- 2026-10-24: created from Shayne's idea, while restructuring AI-11.
- 2026-10-24: the trap written out in full (Shayne asked) — AI-1.6's table, why it happened, what
  a busy board must do differently; MtgSimulator's OutputProbe added as a precedent.
- 2026-10-24: Shayne's deathtouch example — value is what a card makes happen; a requirement's check.
- 2026-10-24: Shayne: win rates for deckbuilding, the state for gameplay (Decisions).
