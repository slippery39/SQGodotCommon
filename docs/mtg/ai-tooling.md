# MTG — Seeing what the AI is doing

Moved out of the root `CLAUDE.md` when this repo became the DOOMJAM project.

Evaluation changes were argued rather than watched for two sessions, and several were wrong. **Use
the tooling before proposing a scoring change.**

| | |
|---|---|
| **Space** in game | Pause the AI — do this *before* F6 so you can click through candidates |
| **F6** in game | AI inspector overlay — every ranked action, and the chosen one's score by evaluator term |
| **F7** in game | Save the live position to `user://scenarios/` |
| Console **mode 5** | Load a scenario, have several strategies decide in it side by side |
| Console **mode 6** | Evolve a constructed metagame |
| Console **mode 7** | Discover synergy engines in a pool — solitaire only. "Does this archetype assemble?", asked before "is it competitive?" |

Three rules worth carrying: a scenario is **serialized state**, not a `GameStateSnapshot` report;
`StateEvaluator.Explain` is the implementation with `Evaluate` as the wrapper — never write a second
copy of the sum for display; and **goldfish speed is measured to be the wrong fitness for a combo
deck** — dismantling Storm makes it goldfish *faster*. Use `EngineProbe`.
