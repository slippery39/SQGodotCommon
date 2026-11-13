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
- 2026-11-13, Shayne: the replay's speed is the table's animation beat (TBL-4.5: 0.8 s since watching it, set in Tools / `--beat=`) —
  one setting, changeable while it plays.

- 2026-11-13, Shayne: notes are the most important part now ("so I can write notes for you to analyze later on";
  the table's animations are good enough to follow along) — on the table, any game (live, AI vs AI, resumed), not
  only in a reviewer. What each note saves, and where, left to Claude ("you will be the one reading it"): a
  markdown file beside the record, `<record>.notes.md` — readable with one read, searchable across games, the
  record and its replay untouched (in the record, Undo would cut notes short and every move rewrites it). Each
  note: the text, the move number with its reopen command, both sides by name (hands, battlefield with stats,
  tapped, counters), the stack, the last 8 log lines, the AI's last search (top 6). Shayne: "From now on I will be
  using the MagicTable to view scenarios."

Open questions:
- "The AI's next decision" for Shayne's seat too (what the AI would do in his place)?
- A position from a live game: F7's save, or a reviewer button on the table?

## Tasks

- [ ] TBL-10.1 Interview the open questions; the screen sketched against the MTGA reference (TBL-4)
- [ ] TBL-10.2 The player: open a record or position, step / back / play / pause / jump, both hands shown
- [x] TBL-10.3 Notes per step, saved beside the record; the export (log + notes) for Claude — BUILT (2026-11-13):
      `GameNotes`, Tools · Write a note or N (the AI pauses while it is open); the notes file IS the export. Tested:
      a note's move reopens that exact position. Captured: the dialog by real clicks (`shots/tbl103-a`)
- [ ] TBL-10.4 The AI's next decision while paused (the inspector on a searched position)

## Next

**Notes (TBL-10.3) are in use**; Shayne's first session (2026-11-13) became AI-9.9, AI-13.13 and AI-25, the game kept as
`MagicCore.Tests/Records/ShayneNotesSeed1.json`. The uncapped AI on the table: `--full-ai` or Tools · Full AI (the
notes' "AI's last search" is the capped one otherwise — at #213 the two differed on a tie). Built 2026-11-13: a pause holds the
queued beats too (P, Tools, Space in AI vs AI, the note box), and a note saves the position ON SCREEN — its move, its
transcript lines (numbered as the reopen counts), the last search decided before it. — "analyze my notes" means read `game_records/*.notes.md`, reopen each position with
the note's command, and weigh the AI's last search there. Then TBL-10.1. Read TBL-5 (records, `--resume`, Undo), AI-14.4 (the F6 inspector) and `MagicTable/CLAUDE.md`.

## Log

- 2026-11-12: created from Shayne's sprint-5 notes.
- 2026-11-13: Shayne's first notes session read with `MagicConsole --explain`: three causes, written up as AI-9.9, AI-13.13, AI-25; the table's AI uncappable (`--full-ai`, Tools).
- 2026-11-13, Shayne: the note box should pause the animations too, and a pause key — P (and Space in AI vs AI) pause the game, beats included; a note saves the position on screen (`GameNotesTests`: a note behind the game lists no later move). Captured: the box held the table still from its frame to Cancel (`shots/pause-a`).
