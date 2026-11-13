---
id: MC-7
title: The attacker assigns combat damage among its blockers
status: in-progress
area: MC
created: 2026-11-09
---

## Why

Shayne, 2026-11-09: when the opponent double-blocks, we don't get to decide how damage is dealt. CR 510.1c: the attacking player orders/divides damage among the blockers. It is a known simplification today (blocker damage splits automatically - DesignNotes "MagicCore known ceilings").

## Requirements

- [ ] With two or more blockers the attacker's controller divides the damage (CR 510.1c — as amended by Foundations, 2024: "divided as its controller chooses among them", NO order and no lethal-first; trample still needs lethal on every blocker before the player, CR 702.19c), on the table and for the AI
- [ ] The AI chooses sensibly (kill the most valuable it can); a record replays
- [x] **For now (Shayne, 2026-11-12): nobody is asked** — the damage is divided by how much it is worth to the
      attacker's board (which creatures it kills); asking the player is revisited later (MC-7.3)

## Decisions

- 2026-11-12, Claude (cube lane, overnight) — **the rule, checked**: the requirement's "lethal to the first before
  the next" is the OLD rule. Magic Foundations (November 2024) removed damage assignment order: the attacker divides
  its damage among its blockers however it likes, regardless of lethal (Wizards' Foundations update bulletin: "as
  they choose among all the creatures blocking it"); only trample still needs lethal on every blocker before any
  reaches the player. Built to the current rule; the requirement is reworded.
- 2026-11-12, Claude — MC-7.1, **Claude's default, for review**: **a choice per attack**, not a standing order. The
  costs:
  - *A choice per attack* (chosen): in the combat damage step (and the first-strike step), for each attacker with two or
    more blockers, its controller answers "how much to [blocker]?" for each blocker but the last, which takes the rest
    — `AssignCombatDamageChoice`, options 0 to what is left (id = amount + 1, like `ChooseNumberChoice`). It is the
    current rule exactly (any split, any order), the table asks it like every other numbered choice (the generic
    `ChoicePanel`; MC-7.3 makes it nicer), and it is bounded: K−1 questions of at most power+1 options — never one
    option per split (compositions grow fast: power 10 over 3 blockers is 66). Cost: a question in every multi-block,
    so records and golden replays move (handled below), and the AI must answer it.
  - *A standing order* (declared once, lethal in that order): one question at the block, cheap for the AI — but it is
    the pre-2024 rule, can't express "1 to each" or "spare the first", and the table would ask something Arena no
    longer asks. Rejected.
- 2026-11-12, Claude: **the AI** — the look-ahead's quick answer (`Lookahead.QuickAnswer`, so every rollout and
  `Settle`) is `Combat.DefaultSplit`, the split the engine made before it was a choice (each blocker lethal in
  declaration order, the excess onto the first, or with trample to the player); `GreedyAi` and the beam pick by its
  one-ply evaluation over the amounts (the beam asks GreedyAi, as for Nyota's number — no rollout per amount). MC-7.4
  checks that against a position.
- 2026-11-12, Claude: **old records replay** — a record made before MC-7 never answered the question; asked and not
  answered by the record, it gets `DefaultSplit`'s answer (`GameRecord.Unasked`, which already did this for TBL-9.2's
  surveil order — on the table too). One difference from before: the excess onto the first blocker is ONE damage
  event now, not lethal and then the rest as two (simultaneous damage, CR 510.2 — more correct; a "whenever dealt
  damage" trigger on a double-blocked creature fires once).
- 2026-11-12, Shayne (reviewing MC-7.1): **simplified for now, revisited later** — "the damage should be dealt based
  on how much value it provides the player (i.e. which creatures the damage would kill that would increase its board
  state the most)". So the engine keeps the rules-correct question (`AssignCombatDamageChoice`), and NOBODY is asked:
  the table (`MagicTableManager.Settle`, a "rule" move) and the console answer it for the human with GreedyAi's one-ply
  pick — each amount settled through the end of combat and the board evaluated (`Evaluator`) — the same pick the AI
  makes. Revisiting is taking those two lines out and building MC-7.3. The look-ahead's rollouts still use the fixed
  split (`DefaultSplit`) — DesignNotes.
- 2026-11-12, Claude: **ponytail** — with trample the LAST blocker takes no more than lethal (the rest goes to the
  player); overkilling it instead is never wanted. Offered if a card ever makes it matter.
- 2026-11-12, Claude: **the position suite is NOT regenerated here** — regenerating it in this lane moved every game
  from move ~6 (the committed suite was already behind the AI lane's beam), which would move the AI lane's baseline.
  Instead the file says whether its games answer the split (`splits|recorded`, written by every new `--suite=generate`);
  the committed one doesn't, so its replay answers each split with `DefaultSplit`. Measured (`--suite=beam`, 288
  positions): 1 decision changed and 79.9% oracle agreement — the SAME as HEAD without MC-7 (checked in a worktree);
  cost 10,735 engine steps a position against 10,594 (+1.3%, the new question's steps).
- 2026-11-12, Claude: the code review's leftovers, judged: a stale post-MC-7 record answered by default at a split is
  still caught at the next move's offers check; `Default` reads the pending choice's options (every caller has one);
  GreedyAi's one-ply pick settles up to power+1 lines per question (measured +1.3% above; a rule if a big board
  shows the cost).

## Tasks

- [x] MC-7.1 Read the ceiling in `DesignNotes.md` and `Combat.Assigns`; cost the options (a choice per attack vs a standing order) and ask Shayne — costed (Decisions); a choice per attack, Claude's default for review
- [x] MC-7.2 The choice in the engine, behind records' offer fingerprints (a new choice makes old records stale - check one replays)
      — `Combat.BeginDamage` → `AssignCombatDamageChoice` per blocker but the last → `DealCombatDamageAction`; first strike
      too; 6 tests in `CombatTests` (who is asked, 0 to all offered, no lethal-first, trample over two both ways, a lone
      blocker asks nothing, the quick answer is the old split). Every kept record replays (`GameRecord.Unasked`); the
      look-ahead's `Settle` answers it by the old split (it had judged it by the chooser's best — that moved an AI
      control test, ChangSac458). The table asks it in the generic panel (`Describe.Question` names both creatures).
- [ ] MC-7.3 The table: pick how much each blocker gets — DEFERRED (Shayne, 2026-11-12: by value for now, revisit later)
- [x] MC-7.4 The AI's choice, a position test (the double block that killed the wrong creature) — the value pick kills
      the 2/2 flier with lifelink over the vanilla 2/2 declared first (`CombatTests.TheValuePick_…`, revert-checked:
      the old fixed split fails it); the table and console answer by it for the human

## Next

Done for now (Shayne's simplification). Revisit later: MC-7.3, the player divides the damage on the table (the generic
question is still there — take out the value answer in `MagicTableManager.Settle` and `MagicConsole`), and the rollouts
judging a split rather than assuming the fixed one (DesignNotes).

## Log

- 2026-11-09: opened from Shayne's playtest notes.
- 2026-11-12, Shayne: this sprint, in the cube lane (an MC- story; an engine fix both lanes need — merged back at once).
- 2026-11-12 (night, cube lane): MC-7.1 costed (a choice per attack, the current CR 510.1c), MC-7.2 built — records and the suite replay, every test green.
- 2026-11-12 (cube lane): Shayne simplified it — divided by value, nobody asked; built on the table and console, tested.
