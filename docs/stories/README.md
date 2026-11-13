# Stories

Every piece of work on this branch has one **story**: why we want it, its requirements, the
decisions taken (so nobody re-asks them), its tasks, and what a fresh session needs to pick it up.
A story replaces the handoff file — when a session ends, the story already says where things stand.

Refer to work by its ID: **`AI-1`** is a story, **`AI-1.2`** a task inside it.

## Sprints

**Current sprint: [2026-11-12](../sprints/2026-11-12.md)** — faster tests and AI decisions, a fairer
look-ahead (AI-20), a table and a reviewer where the play is easy to follow; the cube lane starts LSVPower.
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

**Next session (written 2026-11-12, evening — the cube lane running its first session):**
1. **For Shayne to read:** the AI-20 research note (`docs/findings/ai-research.md`, its questions are AI-20.1's) and the
   MTGA reference to mark (`docs/mockups/magictable/mtga-reference.md`). The DamagePotential match: no difference, kept.
2. Faster tests are done (the full suite ~3 min in parallel; while working, `Commands.md`'s slices — cards and engine 7 s).
   **AI-22.1 next** — it measures wall-clock time: only on a quiet machine, not while the cube lane builds.
3. **The cube lane** (`D:\GameDev\SQGodotCommon-cube`) is on C1–C5; this lane runs nothing heavy until it ends.

1. **AI-22** — faster AI decisions for sealed runs: profiled, then the levers AI-20 won't rewrite
2. **AI-21** — combat on big boards (Tribbles), with AI-22 where its profile points
3. **AI-20** — the look-ahead stops betting on one guessed future: research (Forge, hidden-information search) first, then Shayne's pick
4. **AI-19** — the scan's value leftovers: Cold-Blooded Crew, Mikaeus, won-line ties
5. **AI-23** — stationing priced: the positions first, Shayne confirms
6. **TBL-4** — the MTGA reference page (TBL-4.4), then animations at MTGO's level (TBL-4.5)
7. **TBL-10** — the game reviewer: step, rewind, notes, the AI's next decision while paused
8. **DEV-6** — the cube lane's first session (DEV-6.5), tonight
9. **DEV-4**, **DEV-5** — the rules split and the end-of-batch script — before any new set (cube lane)
10. **MC-7** — the attacker divides combat damage among its blockers (cube lane, this sprint)
11. **MC-6** — MagicCore's pieces compose: one per family (cube lane)
12. **MC-4** — LSVPower (Shayne, 2026-11-12): imported, surveyed, built in batches (cube lane)
13. **AI-18** — tap choices: AI-18.1's measurement folded into AI-22's profile
14. **AI-9** — resources by use: AI-9.7 (Quadrant Theory boards), AI-9.8 (the card-value table charged for mana cost)
15. **AI-13** — Shayne's lessons as general rules for the search, each measured
16. **AI-14** — see what the AI decides: AI-14.3 (reading reports) replaced by the reviewer (TBL-10)
17. **LIM-2** — draft against bots: LIM-2.3, Shayne's playtest of the draft screen
18. **LIM-1** — sealed: the deck testing tools (`/sealed-test`)
19. **TBL-3** — the hand on Common's `Card2D`, with drag-to-play — with TBL-4
20. **TBL-2** — play games on MagicTable; every "looks off" becomes a task
21. **MC-5** — Star Trek: every task done; its requirements to tick and close
22. **DATA-2** — rate cards without 17lands: DATA-2.7, the format report
23. **DEV-3** — sprint tooling: a `/sprint` skill, the story script's sprint view and drift checks
24. **DATA-1** — bring in 17lands data
25. **AI-11** — learn from Shayne's games: after the reviewer, his notes feed it
26. **AI-4** — strategic plans above single actions
27. **AI-12** — a card's threat over time

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
agreed, can start) → `in-progress` → `done`, or `dropped` (say why in the Log).

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
| [DATA-2](DATA-2-engine-ratings.md) | Rate a set's cards without 17lands — measured by the engine, checked against FRA's real rates | in-progress |
| [DEV-6](DEV-6-two-worktrees.md) | Two lanes at once — a second worktree for the cube, merged back often | in-progress |
| [LIM-1](LIM-1-sealed-draft.md) | Sealed for Reality Fracture — authentic Arena packs, an AI build, played on the table | in-progress |
| [LIM-2](LIM-2-draft.md) | Draft Reality Fracture against bots | in-progress |
| [TBL-2](TBL-2-playtest-pass.md) | Playtest MagicTable until the games look right | in-progress |
| [TBL-3](TBL-3-common-card2d-hand.md) | MagicTable's cards use Common's Card2D, with drag-to-play | in-progress |
| [TBL-4](TBL-4-clearer-table.md) | A clearer, calmer table — MTGA first, the best of MTGO kept | in-progress |
| [AI-21](AI-21-big-board-combat.md) | Combat on big boards — attack plans that don't grow with every token | ready |
| [AI-22](AI-22-faster-decisions.md) | Faster AI decisions — sealed runs in minutes, not hours | ready |
| [MC-5](MC-5-star-trek.md) | Star Trek (TRK) plays in MagicCore — sealed and draft before Arena's day 1 | ready |
| [MC-6](MC-6-composable-pieces.md) | MagicCore's pieces compose — one piece per family, not one per card phrase | ready |
| [AI-4](AI-4-strategic-plans.md) | Strategic plans — the AI picks a plan above single actions | backlog |
| [AI-11](AI-11-learn-from-shaynes-games.md) | Learn from Shayne's games — settle where he and the oracle differ, keep what he got right | backlog |
| [AI-12](AI-12-threat-over-time.md) | A card's threat over time — what it does if it isn't answered, on an empty board and a busy one | backlog |
| [AI-20](AI-20-lookahead-unknown-cards.md) | The look-ahead stops betting on one guessed future — unknown cards handled honestly | backlog |
| [AI-23](AI-23-station-priced.md) | Stationing priced — what the counters buy against the attack or block given up | backlog |
| [DATA-1](DATA-1-17lands.md) | Bring 17lands data into the engine's tools | backlog |
| [DEV-3](DEV-3-sprint-tooling.md) | Sprint tooling — a /sprint skill and the story script's mechanical checks | backlog |
| [DEV-4](DEV-4-split-mechanics-rules.md) | Split magic-mechanics.md by subsystem, so a session loads only the rules it touches | backlog |
| [DEV-5](DEV-5-batch-check-script.md) | One script for a card batch's end-of-batch checks, one summary out | backlog |
| [MC-4](MC-4-powered-cube.md) | MTGA's powered cube plays in MagicCore — surveyed, then built in batches | backlog |
| [MC-7](MC-7-combat-damage-assignment.md) | The attacker assigns combat damage among its blockers | backlog |
| [TBL-10](TBL-10-game-reviewer.md) | A game reviewer — step, rewind and play a game on the table, with notes and the AI's next decision | backlog |
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
| [DEV-1](DEV-1-story-system.md) | A story system that always says where the work stands | done |
| [DEV-2](DEV-2-push-mtg-real.md) | mtg-real exists somewhere besides this machine | done |
| [DEV-7](DEV-7-faster-tests.md) | Faster tests while working — run what a change touches, the full suite at a story's end | done |
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

Next free id per area: AI-24 · DATA-3 · DEV-8 · LIM-6 · MC-8 · TBL-11
<!-- stories:end -->
