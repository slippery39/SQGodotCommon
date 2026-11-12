# Two lanes (DEV-6)

This repository has two worktrees, each a lane with its own VS Code window and Claude session:

| Folder | Branch | Lane | Story areas |
|---|---|---|---|
| `C:\SQGodotHelperApps\SQGodotCommon` | `mtg-real` | the AI, UI, sealed testing | `AI-` `TBL-` `LIM-` `DATA-` `DEV-` |
| `D:\GameDev\SQGodotCommon-cube` | `mtg-cube` | new sets, the cube | `MC-` |

- **Know your lane** (`git branch --show-current`) and take a new story id only from its areas, so the "next free
  id" never clashes. The shared docs ("Next up", the sprint file, `CLAUDE.md`) merge as text.
- **Neither session sees the other's work until a merge** — commits are shared at once, but a branch only gets them
  by `git merge`. Merge often and small; after every merge, the full tests and the position suite.
- **The engine is the real overlap.** A cube piece, or MC-6 generalising one, can change how FRA and TRK cards play
  — which the AI lane's measurements stand on. An engine fix both lanes need is made in one and merged into the other
  at once.
- **Generated files are never resolved by hand** (`AbilityCatalog.md`, card sheets, coverage, `*.values.json`,
  `FRA.positions.txt`, the golden replays, the story table): take either side, then regenerate after the merge.
- **Per folder, not in git:** `sim_results/` (run `/regen-card-values` once — four tests need it), `ai_reports/`,
  `game_records/`, `.godot/`. **Shared:** Claude's memory (it follows the repository) and Godot's `user://`, so F7
  scenarios and settings saved in one lane appear in the other.
- **One machine:** a game's budget is wall-clock, so no long sim while the other lane builds or tests heavily.
- **One weekly Claude budget:** `python tools/usage_report.py --all` lists both lanes' sessions.
- After a merge, `git diff --stat` on `Commands.md` (it mixes CRLF and LF). `.claude/settings.local.json` is tracked
  but holds only permissions — on a conflict, keep both lists.
