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
- 2026-11-13, Shayne (TBL-4.5): the animations are for CLARITY — what is going on, and where every card goes. What
  one move does together animates together, as ONE beat (three creatures dying in combat damage go at once, never
  one after another). A beat is **0.2 s** (five moves a second), configurable while watching — the Tools menu and
  `--beat=` — and the replay (TBL-10) uses the same setting. First rows: A1–A3, A5, A6.
- 2026-11-13 (Claude, Shayne agreed): **what happened is read from a diff of the two states**, not MagicCore's
  staged events — they are cleared within the same move (`GivePriority`), exist only where a trigger needs one (no
  "went to the stack", "resolved", bounce or exile) and a life gain carries no amount; a card keeps its id across
  zones, so the diff sees every move. MagicCore is untouched; the manager keeps each move's state only when the
  scene asks (`Animate`), so tests and simulations cost nothing. A creature that dies shows no damage number (its
  damage goes with it) — dimmed, then to the graveyard.
- 2026-11-13, Shayne, after watching a game: "even very slow can seem way too fast" — the old Very slow (0.8 s) is
  Normal now: Fast 0.4, Normal 0.8, Slow 1.2, Very slow 1.6. Still hard to follow: attackers (asks for a combat row:
  attackers and blockers MOVE into it, apart from the rest of the board — TBL-4.6), and the stack (when something
  went on it, and what it targets — TBL-4.5's arrows). Permanents may be shorter — the stats moved up off the unused
  bottom margin — to make room (TBL-4.7).
- 2026-11-13, Shayne: watching a game he wants their hand FACE UP — "to see what they have in hand and determine
  whether or not they are making reasonable plays. That was the point of it"; backs do nothing for him. Built: Tools
  · Show their hand (and `--show-hand`) — their hand in their plate, face up and readable (118x164, wrapping three
  across; hover / hold zooms), and their cards fly face up. A viewer's switch, off by default (a live game hides it).
- 2026-11-13, Shayne: the combat row sits in the MIDDLE, between the two halves (where the phase bar is), and only
  while there is combat (TBL-4.6). A target arrow shows only while its spell or ability is ON THE STACK — not while
  picking targets — "for now and see how it looks" (TBL-4.5).

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
- [x] TBL-4.6 A combat row (Shayne, 2026-11-13: "attackers was hard to follow"): when attackers are declared they
      move into a row of their own, apart from the rest of the board, and blockers join them — so who attacks and
      who blocks is clear. Builds on TBL-4.3's columns; the move is a beat (a card whose place changed flies there).
      BUILT (2026-11-13), awaiting Shayne's look: a red-edged band under the phase bar while there is combat, a
      column per attacker, the opponent's side on top; TBL-4.3's columns in the creature rows are gone
      (`ColumnWidth` deleted). The cost: six rows share the board while it is open, so every card shrinks ~35%
      for the combat (1.38x → 0.86x at 1280x720) and grows back after. Captured: AI vs AI turn 6 and turn 14 (two
      attackers, a blocker flying under the second), and your block by real clicks — your creature, then the
      attacker in the band, then Block (`shots/tbl46-a` … `-d`). Shayne, watching it: no flight into or out of the
      band — it showed the full card, then went back to the crop; "I would rather it just be instant". Now they
      appear there at once; the declaration still holds a beat
- [x] TBL-4.7 Shorter permanents: the stats and loyalty moved up off the crop's unused bottom margin, the rows less
      tall — room for what the table lacks (the opponent's hand, the combat row). BUILT (2026-11-13): 140 tall, not
      168; stats, count and status on the art (`CardViews.ArtShare`), the name below as many lines as fit. Alone it
      frees nothing — a row's height comes from the screen, so the cards just scale up to fill it (captured:
      `shots/tbl45-b` frame 219 vs `shots/tbl47-after`); the room appears when TBL-4.6's row takes its band
- [ ] TBL-4.4 The MTGA reference: current screenshots and clips of the table, gathered into one page for Shayne to
      mark (2018's beta guides show the layout but no motion) — then TBL-4.1's interview against it
- [ ] TBL-4.5 Animations, MTGO's level: a cast to the stack and onto the battlefield, the stack shown while it holds
      anything, attack and target arrows, damage numbers, deaths to the graveyard; each a short beat the eye can follow.
      First: A1–A3 (cast, resolve, settle), A5 (damage, deaths), A6 (life counting); then A4 (tap), A7 (trigger pulse),
      and the stack made followable (Shayne: unclear WHEN something went on it and WHAT it targets): target arrows.
      STACK BUILT (2026-11-13): `TargetArrows` — an orange arrow from each stack row to each target on screen (a
      card, a player's plate) while the spell or ability is on the stack; an ability going on the stack is a beat of
      its own, its row and its source pulsing (A7; `TableChanges.Triggered`). Captured: Extended Absence's arrow to
      Greenhouse Propagator from its cast to its resolution (`shots/tbl45-arrow` frames 187–193). A target off screen
      (a graveyard's card) gets no arrow
      FIRST ROWS BUILT (2026-11-13), awaiting Shayne's look: `TableChanges` (the diff, tested on a whole game),
      `MagicTableManager.Animate`/`TakeSteps`, `MagicTableScene.Motion.cs` (the beats); Tools · Animation and `--beat=`.
      Captured at `--beat=0.8` (10 fps, 8 frames a beat), each frame-diffed: an AI's land drop and cast from its hand
      backs to the stack, a resolve onto the battlefield and settle, your cast by a REAL click (hand → stack →
      battlefield, its trigger, the AI's discard → graveyard, each waiting its turn), turn 6's block — Rank Rat and
      Yoshimaru dying in ONE beat — and 23 → 18 counting with "-5" floating (`shots/tbl45-a` … `-e`)

## Next

**TBL-4.5's first rows are built — Shayne to watch a real game** (the captures ran at 10 fps; 0.2 s at full speed
is unseen by eye). Open edges, should they bite: a tapped card flies upright (the flier is the full card); the
graveyard's count and the log change at the start of the beat, before the card arrives; a land joining a land stack
shows the new count at once; a dying creature shows no damage number (decided); a token that dies dims where it
was and goes (it leaves the game, so no graveyard to fly to); `--beat=` off the five presets ticks none in Tools. Then A4 (tapping turns over a beat)
and A7 (a trigger's source pulses) — both from the same diff (`Moved`'s siblings in `TableChanges`).

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
- 2026-11-13: TBL-4.5 started — Shayne: one beat per move (all it changed at once), 0.2 s, configurable in play
  and in the replay; the diff of two states chosen over the staged events; A1–A3, A5, A6 built and captured.
