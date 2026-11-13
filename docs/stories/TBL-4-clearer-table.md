---
id: TBL-4
title: A clearer, calmer table — MTGA first, the best of MTGO kept
status: in-progress
area: TBL
created: 2026-10-20
---

## Why

Shayne's first game against the search (TBL-2, 2026-10-20): he passed through his own turn because he
thought it was the opponent's. The table works, but "it feels a little too busy". The aim: a cross
between MTG Arena and MTGO, keeping the best of both — feeling more like Arena is an OK start.

## Requirements

- [ ] Whose turn it is, and who holds priority, is unmistakable at a glance — a pass can't be made
      thinking it is the other player's turn
- [ ] Less busy: what isn't needed for this decision recedes
- [ ] Arena's clarity first; MTGO's strengths kept where they serve (to be listed with Shayne)
- [ ] The look is decided the repo's way: `MtgVisualDesign.md` and the `/match-mockup` skill (mockups,
      a rubric, a locked style, then each screen matched)

## Decisions

- 2026-10-20, Shayne: noted now, done in a later session; an Arena-like feel is an OK start.
- 2026-10-23, Shayne: **quick wins first** (TBL-4.2), each built and shown as before/after
  captures — no mockup round for them; the `/match-mockup` process is for the redesign (TBL-4.1).
- 2026-10-23, Shayne: phone is a **nice-to-have for later** — nothing new may depend on hover alone
  (a touch has none; KIN's cards take a short tap as the hover).
- 2026-10-23: TBL-3 (the hand on Card2D) is decided — hand only, KIN's fan; the redesign builds
  around it.
- 2026-11-12, Shayne: **animations are required, not polish** — "things happen but its not clear what"; MTGO's
  level is enough (a card to the stack and onto the battlefield, the stack visible, arrows, damage, deaths). The
  table is the screen that matters most. Claude gathers MTGA reference screenshots and clips; Shayne marks what to copy.

Open questions:
- Which of MTGO's strengths to keep (stops, the full log, full control, card zoom, …)?
- The turn/priority signal: Arena's phase ribbon and pass button, a coloured frame, a banner?
- How it relates to TBL-3 (the hand on Common's Card2D, drag-to-play) — one overhaul or two steps?

## Tasks

- [x] TBL-4.2 Quick wins (sprint item 5), from a capture of turn 7 (seed 5, 2026-10-23):
      whose turn it is — a banner as the turn changes, the active side's plate and board edge lit,
      the pass button naming what comes next ("To combat", "End turn") as Arena's does; the phase
      bar one row (the active player's), stops in a toggle; a dark overlay calming the leather
      behind the play area; battlefield cards sized to the space (the centre sits empty); the
      developer controls (Full control, Undo, Sandbox, Pause AI, Step, speed) folded into a Tools
      menu; click-and-hold to zoom a card (phone needs it too). And one to find the cause of
      first: the log line "Attack with Samut… 2/2? Attack You" looks like the attack prompt's own
      text in the log. Each shown as before/after captures for Shayne's yes — BUILT, awaiting
      that yes: all of the list; the log line was `Describe.Answer` (the prompt + the pick) for an
      AI's declaration — logged now as yours reads, a "don't" as nothing (the golden replays hash
      `Describe.Answer`, so it stays); captures `shots/tbl42-before`, `-after4`, `-zoom`, `-tools`
- [ ] TBL-4.1 Interview Shayne: what is "busy" today, what from MTGO to keep, which Arena screens to
      match; then mockup prompts (`/match-mockup`)
- [x] TBL-4.3 Show who blocks whom: Shayne, 2026-11-08 — two creatures of the same name, no way to tell which one
      the opponent blocked. He compared MTGO (blockers sit across from the attacker they block) with Arena
      (arrows) and chose MTGO's: "much simpler, the Arena one has always felt weird and hard to read". Built:
      while attackers are declared, each attacker and its blockers share a column in the two creature rows
      (which already face each other across the phase bar), rows left-aligned while it lasts; combatants
      first, the rest after (`MagicTableScene.Fill`, `ColumnWidth`). Captured on two real games, in both
      directions (Garak under-blocked by two Conquerors; my Culber blocked by Pet Sehlat + Khan). Not pinned
      by a test (Godot layout). New debug flag `--resume-at=N` (Commands.md)
- [ ] TBL-4.4 The MTGA reference: current screenshots and clips of the table, gathered into one page for Shayne to
      mark (2018's beta guides show the layout but no motion) — then TBL-4.1's interview against it
- [ ] TBL-4.5 Animations, MTGO's level: a cast to the stack and onto the battlefield, the stack shown while it holds
      anything, attack and target arrows, damage numbers, deaths to the graveyard; each a short beat the eye can follow

## Next

**TBL-4.2 is done** — Shayne: "fine for now" (2026-10-23).
Open edges, should they bite: the pass label is by step alone ("To combat" in main 1 even with no
attackers — `PassLabel`'s ponytail); permanents grow only ~1.1× at 1280x720, since height is the
limit; a click now fires on release; the Tools popup's unticked boxes are faint (Godot's default
icons on a dark popup — godot-frontend.md). Then TBL-4.1 (the redesign) next sprint.
Capture with flags as SEPARATE strings — `-GameArgs '--autoplay=7','--greedy','--seed=5'`; one
string arrives as one argument and the table silently starts at the mulligan.

Read `MtgVisualDesign.md`, `.claude/rules/magic-table.md`, TBL-3, and capture today's table
(`Run-Godot.ps1 -Capture`) to point at during the interview.

## Log

- 2026-10-20: created from Shayne's first playtest session (TBL-2).
- 2026-10-23: sprint planning — TBL-4.2 (quick wins, captures not mockups) added.
- 2026-10-23: TBL-4.2 built — turn banner, the active half and plate edged in gold, the pass button
  naming what comes next, a one-row phase bar with Stops, a dark wash under each half, permanents
  scaled to the room, a Tools menu, hold to zoom (click on release); the AI's attacks logged as
  "Attack: X → You". Checked by capture and real input (a hold zoomed and didn't play; a click played).
- 2026-11-12: TBL-4.4 and TBL-4.5 added (Shayne, sprint 5 planning).
- 2026-11-12: TBL-4.4's page gathered — `docs/mockups/magictable/mtga-reference.md`, each row for Shayne to mark copy / skip / later; his own Arena screenshots asked for.
