# Stories

Every piece of work on this branch has one **story**: why we want it, its requirements, the
decisions taken (so nobody re-asks them), its tasks, and what a fresh session needs to pick it up.
A story replaces the handoff file — when a session ends, the story already says where things stand.

Refer to work by its ID: **`AI-1`** is a story, **`AI-1.2`** a task inside it.

## Sprints

**Current sprint: [2026-11-19](../sprints/2026-11-19.md)** — a sealed answer in 20 minutes and tests out of
the way, then AI quality through notes (land sequencing, AI notes, option pricing); the cube lane goes on with LSVPower.
Start there: it says which task is next.

Sprints run **Thursday to Thursday**, when Shayne's Claude usage resets, so a sprint is one week's
usage budget. **One file per sprint** in `docs/sprints/`, named by its start date (the Thursday
reset; sprint 2 began on a Friday only because sprints were adopted mid-week):
- **Plan:** the goal, then the items in order, each a story task with a size (S/M/L), and which
  items slip first.
- **Review**, written at the close: what got done, what slipped and why, sizes against what each
  item took, anything heavy on usage. After that the file is never edited again.

A sprint file only POINTS at story tasks. Requirements, decisions and where the work stands stay in
the stories, so nothing in a sprint file can go stale. At the boundary: write the review, start the
next file, and move the link above. `python tools/stories.py` checks that the link resolves and that
every task a sprint file names exists.

## Next up

Hand-ordered — reorder freely. Everything here must be an open story (`python tools/stories.py` checks).

**Next session (written 2026-11-19):** DEV-8 done — `python -u tools/test_changed.py --run` runs what a change
touches (~1-1.5 min), `--full` the suite at a story's end (~6 min, in the background). Next AI-30.1, the sealed run
re-profiled (quiet machine). The cube lane goes on with MC-4.4h (black) in its own folder.

1. **AI-30** — sealed runs in 20 minutes: re-profile (the decision cost doubled in sprint 5), the stopped games, early stop once clear, then the levers
2. **AI-29** — AI notes: land sequencing first (AI-29.3), then the shape and the flash pilot; card tags when a note needs one
3. **AI-26** — option pricing, continued from Shayne's table notes
4. **AI-31** — the oracle re-examined: why agreement falls as play improves
5. **AI-27** — the evaluator pricing pass; AI-27.11, the value / tempo / options evaluator separate and swappable
6. **AI-32** — machine learning: research and a proposal, nothing built
7. **TBL-2** — Shayne's table notes of 2026-11-17: blocks shown as chosen, a choice panel you can move or hide, the table live while the AI thinks (TBL-2.20–2.22)
8. **AI-28** — the AI plays in those notes: L'Rell's copy before combat, the Captain's +1/+0 on an attacker, a Clue priced by use, Tricorder's think time
9. **AI-21** — combat on big boards (Tribbles) — the rollout's attack plans are 50% of a sealed decision (AI-22's profile), the biggest speed lever left
10. **AI-20** — the look-ahead stops betting on one guessed future: the land / not-a-land look-ahead is the default; paused 2026-11-16 while Shayne plays games to find big issues (its Next lists what waits)
11. **AI-19** — the scan's value leftovers: Cold-Blooded Crew, Mikaeus, won-line ties
12. **AI-23** — stationing priced: the positions first, Shayne confirms
13. **TBL-4** — the MTGA reference page (TBL-4.4) for Shayne to mark; a smoother, less cluttered table
14. **TBL-10** — the game reviewer: step, rewind, notes, the AI's next decision while paused
15. **DEV-4**, **DEV-5** — the rules split and the end-of-batch script — before any new set (cube lane)
16. **MC-7** — the attacker divides combat damage among its blockers (cube lane, this sprint)
17. **MC-6** — MagicCore's pieces compose: one per family (cube lane)
18. **MC-9** — the cube's cards in the AI's hands: the deferred AI items, watched then fixed, five phases (cube lane, 2026-11-23)
19. **MC-4** — LSVPower (Shayne, 2026-11-12): imported, surveyed, built in batches (cube lane)
20. **AI-18** — tap choices: fewer moves for the same choice (AI-18.1, measure first)
21. **AI-9** — resources by use: AI-9.7 (Quadrant Theory boards), AI-9.8 (the card-value table charged for mana cost)
22. **AI-13** — Shayne's lessons as general rules for the search, each measured
23. **AI-14** — see what the AI decides: AI-14.3 (reading reports) replaced by the reviewer (TBL-10)
24. **LIM-2** — draft against bots: LIM-2.3, Shayne's playtest of the draft screen
25. **LIM-1** — sealed: the deck testing tools (`/sealed-test`)
26. **TBL-3** — the hand on Common's `Card2D`, with drag-to-play — with TBL-4
27. **MC-5** — Star Trek: every task done; its requirements to tick and close
28. **DATA-2** — rate cards without 17lands: DATA-2.7, the format report
29. **DEV-3** — sprint tooling: a `/sprint` skill, the story script's sprint view and drift checks
30. **DATA-1** — bring in 17lands data
31. **AI-11** — learn from Shayne's games: after the reviewer, his notes feed it
32. **AI-4** — strategic plans above single actions
33. **AI-12** — a card's threat over time
34. **AI-24** — the speed levers left from AI-22: shortlist2 / freepasses2 (change moves), the step cap, the state-based audit
35. **MC-8** — every modal spell one cast per choice of modes, its targets chosen after; Mystic Confluence's triple counter (cube lane)
36. **TBL-11** — playable on a phone (backlog, Shayne 2026-11-18)
37. **LIM-6** — a cube builder in the UI (backlog)
38. **LIM-7** — constructed testing (backlog)

## How it works

**One file per story**, named `<ID>-<short-slug>.md`, copied from [`_template.md`](_template.md).
The header carries `id`, `title`, `status`, `area`, `created`; the sections are Why, Requirements,
Decisions, Tasks, Next and Log.

**IDs** are `AREA-N`, numbered per area and never reused — a dropped story keeps its number. Take
the next free number from the bottom of the table below. Tasks are `AREA-N.M`, numbered within the
story. Areas on this branch:

| Area | What |
|---|---|
| `MC` | MagicCore — the rules engine and the cards |
| `TBL` | MagicTable — the Godot front end |
| `AI` | the MagicCore opponent |
| `LIM` | limited: sealed and draft |
| `DATA` | outside data and analytics (17lands) |
| `DEV` | tooling, docs, process |

A new area needs a row here first.

**Status:** `backlog` (wanted, not yet shaped — open questions remain) → `ready` (requirements
agreed, can start) → `in-progress` → `done`, or `dropped` (say why in the Log). A story goes to `done` only
after the full suite passed: `python -u tools/test_changed.py --full` (~6 min), started in the background when the
last task's work is in, its `FULL:` line read before closing (DEV-8). Work goes on while it runs.

**Keep the story current, in the same step as the work** — the root `CLAUDE.md` makes this a rule:
- a requirement added, changed or dropped → edit Requirements, and log the change with its date
- a decision taken → Decisions, dated, with who decided; it is not re-asked
- a task done → tick it (no hash: a commit can't hold its own; its subject starts with the task
  ID, so `git log --grep "AI-8.2"` finds it); a "looks off" report → a new task
- a result measured → a line in Log pointing at `docs/findings/`
- the session ends → Next says what a fresh session needs (files to read, traps, the next task)

**Commits start with the story ID:** `AI-1: value cards in hand`. `git log --grep "AI-1"` is then
the story's whole history.

**What stays elsewhere:** `DesignNotes.md` keeps the deferred engine ceilings and their costed
options — a story links to an entry when it takes one up. Measured numbers go in `docs/findings/`.
Rules go in the `CLAUDE.md` files and `.claude/rules/`. A story points at them; it never copies them.

**Branches:** each project branch owns its own stories (`mtg-real` here; KIN and the core branch
keep theirs). A story about shared code (`Common/`, ImmutableGameObjects) is a **`CMN-`** story,
numbered on `main` only and cherry-picked to the branches it affects, so its number never clashes.

## All stories

<!-- stories:begin -->
<!-- generated by python tools/stories.py — do not edit by hand -->

| ID | Story | Status |
|---|---|---|
| [AI-9](AI-9-resources-by-use.md) | Resources priced by what they provide, not by how many there are | in-progress |
| [AI-13](AI-13-shaynes-lessons.md) | Shayne's lessons as general rules for the search — card-agnostic, switchable, measured | in-progress |
| [AI-14](AI-14-see-the-ai-play.md) | See what the AI decides and why — a decision report now, an in-game inspector next | in-progress |
| [AI-17](AI-17-basic-plays.md) | The AI misses basic plays — two found at the table, then a full review of the AI | in-progress |
| [AI-18](AI-18-tap-choices.md) | Tap choices without ballooning moves | in-progress |
| [AI-19](AI-19-card-by-card-play.md) | Card-by-card play and rulings — after every card plays | in-progress |
| [AI-20](AI-20-lookahead-unknown-cards.md) | The look-ahead stops betting on one guessed future — unknown cards handled honestly | in-progress |
| [AI-26](AI-26-option-pricing-by-target.md) | Option pricing by the target — a held card is worth what it will hit later, less the mana it costs then | in-progress |
| [AI-27](AI-27-scores-in-cards.md) | An evaluator pricing pass — one consistent theory, scored in cards, instead of piecemeal fixes | in-progress |
| [AI-28](AI-28-table-notes-2026-11-17.md) | Shayne's table notes of 2026-11-17 — the AI plays they flagged, each traced and fixed | in-progress |
| [DATA-2](DATA-2-engine-ratings.md) | Rate a set's cards without 17lands — measured by the engine, checked against FRA's real rates | in-progress |
| [DEV-4](DEV-4-split-mechanics-rules.md) | Split magic-mechanics.md by subsystem, so a session loads only the rules it touches | in-progress |
| [DEV-5](DEV-5-batch-check-script.md) | One script for a card batch's end-of-batch checks, one summary out | in-progress |
| [LIM-1](LIM-1-sealed-draft.md) | Sealed for Reality Fracture — authentic Arena packs, an AI build, played on the table | in-progress |
| [LIM-2](LIM-2-draft.md) | Draft Reality Fracture against bots | in-progress |
| [MC-4](MC-4-powered-cube.md) | MTGA's powered cube plays in MagicCore — surveyed, then built in batches | in-progress |
| [MC-7](MC-7-combat-damage-assignment.md) | The attacker assigns combat damage among its blockers | in-progress |
| [MC-9](MC-9-cube-cards-in-the-ais-hands.md) | The cube's cards in the AI's hands - the deferred AI items, watched and fixed | in-progress |
| [TBL-2](TBL-2-playtest-pass.md) | Playtest MagicTable until the games look right | in-progress |
| [TBL-3](TBL-3-common-card2d-hand.md) | MagicTable's cards use Common's Card2D, with drag-to-play | in-progress |
| [TBL-4](TBL-4-clearer-table.md) | A clearer, calmer table — MTGA first, the best of MTGO kept | in-progress |
| [AI-21](AI-21-big-board-combat.md) | Combat on big boards — attack plans that don't grow with every token | ready |
| [AI-30](AI-30-sealed-runs-in-20-minutes.md) | Sealed runs in 20 minutes — a pool's question answered while Shayne waits, not in 3 hours | ready |
| [AI-33](AI-33-attack-tests.md) | Attack tests — the right attack pinned in small positions, before combat is made cheaper | ready |
| [MC-5](MC-5-star-trek.md) | Star Trek (TRK) plays in MagicCore — sealed and draft before Arena's day 1 | ready |
| [MC-6](MC-6-composable-pieces.md) | MagicCore's pieces compose — one piece per family, not one per card phrase | ready |
| [AI-4](AI-4-strategic-plans.md) | Strategic plans — the AI picks a plan above single actions | backlog |
| [AI-11](AI-11-learn-from-shaynes-games.md) | Learn from Shayne's games — settle where he and the oracle differ, keep what he got right | backlog |
| [AI-12](AI-12-threat-over-time.md) | A card's threat over time — what it does if it isn't answered, on an empty board and a busy one | backlog |
| [AI-23](AI-23-station-priced.md) | Stationing priced — what the counters buy against the attack or block given up | backlog |
| [AI-24](AI-24-speed-levers-left.md) | The speed levers left over from AI-22 — tried, costed or proposed, each to decide later | backlog |
| [AI-29](AI-29-ai-notes.md) | AI notes — specific evaluators for troublesome effects, called by the general one | backlog |
| [AI-31](AI-31-the-oracle-reexamined.md) | The oracle re-examined — what the suite's "agrees with the oracle" measures, and whether it still should | backlog |
| [AI-32](AI-32-machine-learning-research.md) | Machine learning — research where it could help the AI play better or faster, before building anything | backlog |
| [AI-34](AI-34-play-around-their-cards.md) | Playing around the opponent's cards — removal, tricks and a look-ahead that bets on one future | backlog |
| [DATA-1](DATA-1-17lands.md) | Bring 17lands data into the engine's tools | backlog |
| [DEV-3](DEV-3-sprint-tooling.md) | Sprint tooling — a /sprint skill and the story script's mechanical checks | backlog |
| [LIM-6](LIM-6-cube-builder.md) | A cube builder — build your own cube in the UI, then draft it | backlog |
| [LIM-7](LIM-7-constructed-testing.md) | Constructed testing — real constructed decks tested by simulation | backlog |
| [MC-8](MC-8-modal-targets-after-cast.md) | Every modal spell is one cast per choice of modes, its targets chosen after | backlog |
| [TBL-10](TBL-10-game-reviewer.md) | A game reviewer — step, rewind and play a game on the table, with notes and the AI's next decision | backlog |
| [TBL-11](TBL-11-playable-on-phone.md) | MagicTable playable on a phone — test decks on the go | backlog |
| [AI-1](AI-1-wasted-removal.md) | The AI plays plausibly — no wasted removal | done |
| [AI-2](AI-2-beam-search.md) | A search AI for MagicCore — port the multi-turn beam search | done |
| [AI-3](AI-3-opponent-models.md) | Swappable simulation models in the search, measured for strength against time | done |
| [AI-5](AI-5-evaluator-threat-abilities.md) | The evaluator sees threats and abilities | done |
| [AI-6](AI-6-fast-measurement.md) | Measure an AI change in minutes, or overnight | done |
| [AI-7](AI-7-faster-search.md) | A faster search — profiled, then optimised without changing a decision | done |
| [AI-8](AI-8-playtest-plays.md) | Plays that looked wrong at the table, rebuilt and fixed | done |
| [AI-10](AI-10-winning-ties.md) | A winning AI still presses — ties among won lines no longer pick the passive move | done |
| [AI-15](AI-15-lookahead-gang-blocks.md) | The look-ahead's defender gang-blocks — a menace attacker isn't unblockable | done |
| [AI-16](AI-16-won-line-self-burn.md) | A won line still counts what it spends — the AI no longer burns its own face | done |
| [AI-22](AI-22-faster-decisions.md) | Faster AI decisions — sealed runs in minutes, not hours | done |
| [AI-25](AI-25-lookahead-combat-tricks.md) | The look-ahead's attacker uses its tricks after blocks — a block isn't free when the attacker can pump | done |
| [DEV-1](DEV-1-story-system.md) | A story system that always says where the work stands | done |
| [DEV-2](DEV-2-push-mtg-real.md) | mtg-real exists somewhere besides this machine | done |
| [DEV-6](DEV-6-two-worktrees.md) | Two lanes at once — a second worktree for the cube, merged back often | done |
| [DEV-7](DEV-7-faster-tests.md) | Faster tests while working — run what a change touches, the full suite at a story's end | done |
| [DEV-8](DEV-8-tests-out-of-the-flow.md) | Tests out of the flow — the slice a change touches while working, the full suite in the background at a story's end | done |
| [LIM-3](LIM-3-greedy-builds.md) | Sealed builds beyond two colours — streamlined, splash or greedy, offered as suggestions | done |
| [LIM-4](LIM-4-deck-builder-search.md) | Deck builder search: find cards by name and keyword | done |
| [LIM-5](LIM-5-arena-deck-builder.md) | An Arena-style deck builder — the deck as cards in columns, sorted and arranged, real mana symbols | done |
| [MC-1](MC-1-fra-cards.md) | A real-rules engine that plays every Reality Fracture card | done |
| [MC-2](MC-2-filter-refactor.md) | One composable filter instead of six closed vocabularies | done |
| [MC-3](MC-3-special-guests.md) | The 10 FRA Special Guests play | done |
| [TBL-1](TBL-1-magictable-v1.md) | MagicTable — play MagicCore games in Godot, the Arena way | done |
| [TBL-5](TBL-5-game-records.md) | Game records — export a played game, replay it, compare its choices with the AI's | done |
| [TBL-6](TBL-6-known-hand-cards.md) | Known cards in the opponent's hand show face up on the table | done |
| [TBL-7](TBL-7-illegal-attack-refused.md) | The table refuses an attack the rules forbid, instead of dropping the creature | done |
| [TBL-8](TBL-8-mana-payment-you-can-see.md) | Mana payment you can trust and see — a smarter auto-tap, and Arena's tap preview | done |
| [TBL-9](TBL-9-playtest-inputs.md) | Table inputs the playtests asked for: an X spinner, reordering a surveil, conceding | done |

Next free id per area: AI-35 · DATA-3 · DEV-9 · LIM-8 · MC-10 · TBL-12
<!-- stories:end -->
