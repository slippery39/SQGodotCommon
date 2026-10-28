# Stories

Every piece of work on this branch has one **story**: why we want it, its requirements, the
decisions taken (so nobody re-asks them), its tasks, and what a fresh session needs to pick it up.
A story replaces the handoff file — when a session ends, the story already says where things stand.

Refer to work by its ID: **`AI-1`** is a story, **`AI-1.2`** a task inside it.

## Sprints

**Current sprint: [2026-10-23](../sprints/2026-10-23.md)** — the full FRA sealed experience for
Arena Direct practice, then a better feel at the table. Start there: it says which task is next.

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

1. **LIM-1** — sealed: authentic Arena FRA packs, a deck builder and an AI build, played on the table
2. **TBL-4** — a clearer, calmer table: quick wins now, the MTGA-first redesign next sprint
3. **TBL-3** — the hand on Common's `Card2D`, with drag-to-play (KIN's hand, copied)
4. **TBL-2** — play games on MagicTable; every "looks off" becomes a task
5. **LIM-2** — draft against bots: FRA first, a cube later
6. **DEV-3** — sprint tooling: a `/sprint` skill, the story script's sprint view and drift checks
7. **AI-9** — resources priced by what they provide
8. **DATA-2** — rate cards without 17lands, checked against FRA's real rates
9. **DATA-1** — bring in 17lands data (the sealed ratings were its first slice)
10. **AI-4** — strategic plans above single actions
11. **AI-11** — learn from Shayne's games: settle where he and the oracle differ, keep what he got right
12. **AI-12** — a card's threat over time, on an empty board and a busy one
13. **AI-13** — Shayne's lessons as general rules for the search, each measured

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
| [LIM-1](LIM-1-sealed-draft.md) | Sealed for Reality Fracture — authentic Arena packs, an AI build, played on the table | in-progress |
| [TBL-2](TBL-2-playtest-pass.md) | Playtest MagicTable until the games look right | in-progress |
| [TBL-3](TBL-3-common-card2d-hand.md) | MagicTable's cards use Common's Card2D, with drag-to-play | in-progress |
| [TBL-4](TBL-4-clearer-table.md) | A clearer, calmer table — MTGA first, the best of MTGO kept | in-progress |
| [AI-4](AI-4-strategic-plans.md) | Strategic plans — the AI picks a plan above single actions | backlog |
| [AI-11](AI-11-learn-from-shaynes-games.md) | Learn from Shayne's games — settle where he and the oracle differ, keep what he got right | backlog |
| [AI-12](AI-12-threat-over-time.md) | A card's threat over time — what it does if it isn't answered, on an empty board and a busy one | backlog |
| [DATA-1](DATA-1-17lands.md) | Bring 17lands data into the engine's tools | backlog |
| [DATA-2](DATA-2-engine-ratings.md) | Rate a set's cards without 17lands — measured by the engine, checked against FRA's real rates | backlog |
| [DEV-3](DEV-3-sprint-tooling.md) | Sprint tooling — a /sprint skill and the story script's mechanical checks | backlog |
| [LIM-2](LIM-2-draft.md) | Draft Reality Fracture against bots | backlog |
| [AI-1](AI-1-wasted-removal.md) | The AI plays plausibly — no wasted removal | done |
| [AI-2](AI-2-beam-search.md) | A search AI for MagicCore — port the multi-turn beam search | done |
| [AI-3](AI-3-opponent-models.md) | Swappable simulation models in the search, measured for strength against time | done |
| [AI-5](AI-5-evaluator-threat-abilities.md) | The evaluator sees threats and abilities | done |
| [AI-6](AI-6-fast-measurement.md) | Measure an AI change in minutes, or overnight | done |
| [AI-7](AI-7-faster-search.md) | A faster search — profiled, then optimised without changing a decision | done |
| [AI-8](AI-8-playtest-plays.md) | Plays that looked wrong at the table, rebuilt and fixed | done |
| [AI-10](AI-10-winning-ties.md) | A winning AI still presses — ties among won lines no longer pick the passive move | done |
| [DEV-1](DEV-1-story-system.md) | A story system that always says where the work stands | done |
| [DEV-2](DEV-2-push-mtg-real.md) | mtg-real exists somewhere besides this machine | done |
| [LIM-3](LIM-3-greedy-builds.md) | Sealed builds beyond two colours — streamlined, splash or greedy, offered as suggestions | done |
| [MC-1](MC-1-fra-cards.md) | A real-rules engine that plays every Reality Fracture card | done |
| [MC-2](MC-2-filter-refactor.md) | One composable filter instead of six closed vocabularies | done |
| [MC-3](MC-3-special-guests.md) | The 10 FRA Special Guests play | done |
| [TBL-1](TBL-1-magictable-v1.md) | MagicTable — play MagicCore games in Godot, the Arena way | done |
| [TBL-5](TBL-5-game-records.md) | Game records — export a played game, replay it, compare its choices with the AI's | done |

Next free id per area: AI-14 · DATA-3 · DEV-4 · LIM-4 · MC-4 · TBL-6
<!-- stories:end -->
