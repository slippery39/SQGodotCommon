---
id: TBL-4
title: A clearer, calmer table — MTGA first, the best of MTGO kept
status: backlog
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

Open questions:
- Which of MTGO's strengths to keep (stops, the full log, full control, card zoom, …)?
- The turn/priority signal: Arena's phase ribbon and pass button, a coloured frame, a banner?
- How it relates to TBL-3 (the hand on Common's Card2D, drag-to-play) — one overhaul or two steps?

## Tasks

- [ ] TBL-4.2 Quick wins (sprint item 5), from a capture of turn 7 (seed 5, 2026-10-23):
      whose turn it is — a banner as the turn changes, the active side's plate and board edge lit,
      the pass button naming what comes next ("To combat", "End turn") as Arena's does; the phase
      bar one row (the active player's), stops in a toggle; a dark overlay calming the leather
      behind the play area; battlefield cards sized to the space (the centre sits empty); the
      developer controls (Full control, Undo, Sandbox, Pause AI, Step, speed) folded into a Tools
      menu; click-and-hold to zoom a card (phone needs it too). And one to find the cause of
      first: the log line "Attack with Samut… 2/2? Attack You" looks like the attack prompt's own
      text in the log. Each shown as before/after captures for Shayne's yes
- [ ] TBL-4.1 Interview Shayne: what is "busy" today, what from MTGO to keep, which Arena screens to
      match; then mockup prompts (`/match-mockup`)

## Next

**TBL-4.2 this sprint** (item 5, `docs/stories/README.md`); the redesign (TBL-4.1) next sprint.
Capture with flags as SEPARATE strings — `-GameArgs '--autoplay=7','--greedy','--seed=5'`; one
string arrives as one argument and the table silently starts at the mulligan.

Read `MtgVisualDesign.md`, `.claude/rules/magic-table.md`, TBL-3, and capture today's table
(`Run-Godot.ps1 -Capture`) to point at during the interview.

## Log

- 2026-10-20: created from Shayne's first playtest session (TBL-2).
- 2026-10-23: sprint planning — TBL-4.2 (quick wins, captures not mockups) added.
