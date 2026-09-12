# MTG — Regenerating the measured tables

Moved out of the root `CLAUDE.md` when this repo became the DOOMJAM project.

`sim_results/` is **untracked and disposable**, but four tests need the card-value table in it:

```
dotnet test MtgSimulator.Tests --filter "FullyQualifiedName~CardValueSweep.SweepTheCoreSetCube"
```

**`sim_results/` resolves against the WORKING DIRECTORY, which under `dotnet test` starts as the test
binary's folder, not the repository.** Anything reading or writing it must call
`TestPaths.ChdirToSolutionRoot()` first. Getting this wrong does not look like a path bug: the table
loads empty, every card reads 0.00pp, and the tests fail exactly as though the data were missing —
while passing in isolation, because alone nothing has moved the directory.

**Never quote a card's win rate from memory or from a CLAUDE.md — compute it from `sim_results/*.json`
every time.**
