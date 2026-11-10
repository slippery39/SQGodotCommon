---
id: TBL-9
title: Table inputs the playtests asked for: an X spinner, reordering a surveil, conceding
status: in-progress
area: TBL
created: 2026-11-09
---

## Why

Shayne's playtest, 2026-11-09. Choosing X lists every possible X as a button (terrible past a few); a surveil lets you put cards in the graveyard but not reorder the ones kept on top, though the rules allow it.

## Requirements

- [x] X is chosen with a spin-down (+ / -) that can also be typed into, never one button per X
- [ ] A surveil (and any "any order" choice) lets the player drag the kept cards into an order
- [x] The player can concede a game: right-click the table → a context menu → Concede, confirmed first

## Decisions

- 2026-11-10, Claude (Shayne asked why surveil can't reorder): it is the engine's own shortcut, not a rule —
  `Surveil` (`ZoneEffects.cs`) asks only which cards go to the graveyard and keeps the rest in their order
  ("ponytail: reordering them is a second choice nobody needs at N=1"). Scry is NOT implemented: no FRA or
  TRK card scries. Spock's "bottom in any order" is random (`ZoneEffects.cs`, Shayne 2026-10-31). So
  TBL-9.2 needs an engine choice first; a new choice can make old game records stale (their offers
  fingerprint — TBL-2.16's trap), so check a record still replays.
Open questions (TBL-9.2, written overnight 2026-11-10 — waiting on Shayne; nothing built):
- How the kept cards' order enters the engine. The surveil choice (`ChooseFromLibraryTopChoice`) is the AI's too:
  the search branches on it and `LandUnlock.Surveil` decides it by rule (AI-17.20); 7 cards surveil 2 or more.
  (A) a second choice after the binning, "the rest back in any order", asked only when 2+ are kept, of BOTH
  players (parity) — the AI answering it by rule (its dearest card on top: the held prices / LandUnlock already
  rank them), so the search doesn't branch on orders. A recorded surveil that kept 2+ goes stale at that move.
  (B) the order folded into the one answer (the binned ids, then the kept in order): no new decision, but the
  choice's options change — the offers fingerprint makes every record with a surveil stale (TBL-2.16's trap) —
  and the search's answers multiply unless it keeps today's order. (C) the table alone reorders, the AI never
  does — breaks the AI/human parity rule; not recommended. Claude's pick: (A).
- 2026-11-10, Claude (open to change): a concede is the table's, not an engine move — the game ends as a
  loss for you (a draft run counts it as one, `TableSetup.Finished`); the record is left as it stands.

## Tasks

- [x] TBL-9.1 X picker: a spinner with +/- and direct typing, bounded by the legal X values (`ChoicePanel`/`Targeting`; today every X is listed)
      Built: `Targeting.Plays` no longer splits a play by X (nor `Modes` a mode); `Play` asks X first when a play
      still spans several (`Targeting.Xs`), and the action column draws it as `X = [−] [box] [+]` and the play's
      button naming the X (`AddXPicker`; Godot's SpinBox, its own arrows hidden). Starts at the most X can be
      (Claude, open to change); a typed X that isn't legal goes to the nearest legal one (`WithChosenX`). A spell's
      {X} and a planeswalker's −X. Test `AnXSpell_IsOnePlay_ItsXChosenOnASpinner`; real input: Guiding Hydra with
      three Plains, − twice, cast — "Cast Guiding Hydra (X=1)", one counter. Typing not driven (mouse only)
- [ ] TBL-9.2 Surveil/"any order": drag to reorder the cards kept on top. The engine takes no order today (Decisions): add the order to the choice, check a record replays, then the table
- [x] TBL-9.3 Concede: a right-click context menu on the table with Concede, a confirm, the game ended as a loss
      Built: a right-click anywhere on the table but a card (that zooms it, as before — `CardViews.CardMeta` marks a
      card) opens a menu with Concede; a dialog confirms ("It counts as a loss"); `MagicTableManager.Concede` — the
      game over, the AI the winner (a draft run counts the loss through `TableSetup.Finished`), the record left
      unfinished; Undo takes the concession back first. Test `Conceding_EndsTheGameForTheAi_AndUndoTakesItBack`;
      real input (new `--rtap`, a right-click): menu → Concede → confirm → "The AI wins."; a right-click on a card
      zoomed it, no menu

## Next

TBL-9.1 and TBL-9.3 are built. TBL-9.2 waits on the open question above (Shayne's pick of A/B/C); then count
the records it makes stale before building (a surveil that kept 2+). Read this story, then
`.claude/rules/magic-table.md` (table) or `MagicCore/CLAUDE.md` (engine) and `LandUnlock.cs` (the AI's surveil rule).

## Log

- 2026-11-09: opened from Shayne's playtest notes.
- 2026-11-10: TBL-9.3 (concede) added (Shayne); TBL-9.2's engine side found (Decisions).
- 2026-11-10: TBL-9.1 built (overnight).
- 2026-11-10: TBL-9.3 built (overnight).
- 2026-11-10: TBL-9.2 sized and stopped at its engine decision (Decisions, open question) — not built.
