---
id: TBL-10
title: A game reviewer — step, rewind and play a game on the table, with notes and the AI's next decision
status: backlog
area: TBL
created: 2026-11-12
---

## Why

Shayne (2026-11-12): "I want to be able to review games, but reading logs is too cumbersome and time consuming and not
always clear, actually seeing the board and stepping through or just replaying the game like normal would be so much
better." Today a record can be resumed onto the table (`--resume=FILE`, Undo steps back — TBL-5) and F6 shows the AI's
last search (AI-14.4); there is no player, no notes, and no way to ask the AI about a paused position. It replaces
reading decision reports (AI-14.3) and feeds AI-11 (learning from his games).

## Requirements

- [ ] Any game record, or any saved position (F7, a game in progress), opens in the reviewer.
- [ ] Step forward and back, play at a speed, pause; jump to a move or a turn.
- [ ] All known information visible: both hands, face up (the reviewer sees everything a record holds).
- [ ] Notes at a step: written while paused, kept beside the record, exported with the game log as one file Claude
      can read (each note with its move number and the position).
- [ ] Paused only: the AI's next decision at this position — its ranked moves and the chosen line (the F6 inspector's
      view), for either seat.
- [ ] Played with the table's animations (TBL-4), so a replay reads like a game.

## Decisions

- 2026-11-12, Shayne: animations are part of following the play, on the table generally, not reviewer-only
  (TBL-4) — "things happen but its not clear what"; MTGO's level of motion is enough.

Open questions:
- Notes: plain text per step in a file beside the record (Claude's proposal)?
- "The AI's next decision" for Shayne's seat too (what the AI would do in his place)?
- A position from a live game: F7's save, or a reviewer button on the table?

## Tasks

- [ ] TBL-10.1 Interview the open questions; the screen sketched against the MTGA reference (TBL-4)
- [ ] TBL-10.2 The player: open a record or position, step / back / play / pause / jump, both hands shown
- [ ] TBL-10.3 Notes per step, saved beside the record; the export (log + notes) for Claude
- [ ] TBL-10.4 The AI's next decision while paused (the inspector on a searched position)

## Next

TBL-10.1. Read TBL-5 (records, `--resume`, Undo), AI-14.4 (the F6 inspector) and `MagicTable/CLAUDE.md`.

## Log

- 2026-11-12: created from Shayne's sprint-5 notes.
