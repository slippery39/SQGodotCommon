---
id: AI-21
title: Combat on big boards — attack plans that don't grow with every token
status: ready
area: AI
created: 2026-11-10
---

## Why

DATA-2's TRK self-play stalled twice on 2026-11-10. Three search games ran for 90+ minutes each, and a sample of the
process showed every stuck thread in `WholeAttacks.Best` inside the rollout. The look-ahead's attack plans grow with
the board: everyone, no one, each creature alone, everyone but one — about 2n + 2 plans, each settled against the
cheap defender, in every look-ahead turn of every line. **Troubling Tribbles** (a 0/1 that can't block and copies
itself each upkeep — 15, 32, 64 …) makes most of those plans about creatures that can deal no damage at all. With
`GameBudget` at 5 minutes (~3x a typical game), 21 games were stopped in round 1 of DATA-2.4, 5 of them on boards of
15–32 Tribble tokens; the rest were long ordinary games (turn 17–28, 4–8 creatures a side). The stopped boards are in
`sim_results/trk_rounds.log` (one line each, `SimulatedRatings.Board`, with the game's seed to replay).

Shayne (2026-11-10): "We do need a way of simplifying combat for more expensive boards" — checks first, "like 'alpha
strike', then 'unblockable creatures only', then like 'most powerful creatures attack'"; "for the tribbles, they have 0
power, so unless there is some sort of boosting effect, checking to see if they should attack and how many is kind of a
waste. Although in MTG there is always edge cases to watch out for (like ninjutsu …)"; and "how much time is being wasted
on essentially calculating the same attacking plan over and over again, even if not much has changed on board".

What AI-18.2 already does, and doesn't: it groups identical permanents in the AI's MOVE list (`MagicRules.AiMoves`,
`Twins`) — not in combat planning, and the rules engine can't (each token is a real object with its own trigger). A
board of hundreds of tokens stays slow in the rules checks themselves (~40% of the stuck trace walked the battlefield).

## Requirements

- [ ] The attack plans the look-ahead weighs do not grow with copies of the same creature: N identical Tribbles cost
      what one does.
- [ ] A creature that would deal no combat damage is not weighed as an attacker unless attacking does something for it
      — read from the rules (an attack trigger of its own or another permanent's, a pump or anthem that gives it power),
      never a list of names. Ninjutsu-like cases are the guard's test.
- [ ] On a board of many DIFFERENT attackers the plans are a fixed menu (Shayne's checks: alpha strike, evasive only,
      the biggest attack, …) — small boards keep today's full set.
- [ ] Every step its own switch (a `beam+…` name), measured the AI-6 way: the position suite unchanged or better, a
      match no worse, and the time a decision costs — before it is kept.
- [ ] The stopped DATA-2 boards replay within the budget with the steps on (their seeds are in the log).

## Decisions

- 2026-11-10, Shayne: written now, **not begun** — the steps start after DATA-2.6's match, which rebuilds the code
  before it plays (an edit in progress would land in the match).
- 2026-11-10, Shayne + Claude: the order below — grouping first (it reuses AI-18.2's tested `Twins` rather than a new
  piece), then the zero-damage filter, then the fixed menu, then a cache only if measured to hit.

Open questions:
- The board size above which the fixed menu replaces the full set (Claude's guess: ~8 attackers) — measure the cost
  curve first.
- The fixed menu's exact plans — Shayne's three, plus Claude's "creatures that survive any single block" and "everyone
  but those that die for nothing" — to agree before step 3.

## Tasks

- [ ] AI-21.1 Measure: replay a few stopped boards (seeds from `trk_rounds.log`) and a normal game under a profiler —
      the share of a decision in `WholeAttacks` (Candidates, Assigned, Best), in `Lookahead.Settle`, and in the rules'
      battlefield walks. Write it to `docs/findings/magic-ai.md`.
- [ ] AI-21.2 Group identical attackers: `WholeAttacks.Candidates` builds "alone" and "all but one" per KIND
      (`MagicRules`' `Twins`), not per creature; `Assigned` per kind too. Switch, suite, match, cost.
- [ ] AI-21.3 Leave out attackers that deal no combat damage (`Combat.Assigns` 0) unless attacking does something —
      the guard read from triggers and statics. Test: a 0/1 stays home; a 0/1 under an anthem, or with "whenever this
      attacks", is weighed. Switch, suite, match, cost.
- [ ] AI-21.4 The fixed menu past the agreed board size (open questions first). Switch, suite, match, cost.
- [ ] AI-21.5 The repeated-combat cache, measured before built: a summary of what combat depends on (each creature's
      power, toughness, keywords, tapped; life; walkers; untapped mana and hand size for tricks) counted for how often
      it would hit inside one decision. Built only if it hits often; a wrong hit is the risk, so its test is a position
      whose combat differs only in something the summary must see.

## Next

Not begun (Shayne). Start at AI-21.1 once DATA-2.6's match is running. Read `MagicCore/Ai/WholeAttacks.cs`
(`Candidates`, `Assigned`, `Best`, `Shortlist`), `Lookahead.Settle`/`QuickAnswer`, and AI-18.2's `MagicRules.AiMoves` /
`Twins` (what makes two permanents the same). The stopped boards: `sim_results/trk_rounds.log`, lines "game N
abandoned". `dotnet-trace` is installed (`~/.dotnet/tools`); a speedscope trace read with a short script found the
stuck frames on 2026-11-10.

## Log

- 2026-11-10: created from DATA-2.4's stalls and Shayne's ideas (Why). Status ready; not begun.
