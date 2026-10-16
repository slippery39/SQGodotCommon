---
name: pre-commit
description: Review uncommitted changes before a commit — the built-in /code-review for bugs, then this repo's own rules (the CLAUDE.md files and .claude/rules/ that govern each changed file), test counts, doc maps and leftovers. Use when asked to "review before committing", "pre-commit", "is this ready to commit", "check my changes", or before making any commit Shayne asked for. Reports findings; never fixes or commits unasked.
---

# Pre-commit review

Two passes over the same diff: **bugs** (the built-in reviewer) and **this repo's rules** (which no
general reviewer knows — every one of them is a bug that already cost real time here).

Never commit as part of this skill. Shayne asks for each commit separately.

## 1. Scope the diff

```
git status --short
```

**Untracked files are invisible to a diff.** Make new work visible without staging its content:

```
git add -N <each untracked path that belongs to the change>
```

(`git reset -- <path>` undoes it.) Skip scratch files — but flag them in step 4. Then list what
changed: `git diff --stat`.

## 2. Bug pass

Invoke the `code-review` skill on the working-tree diff, passing through any level the user gave
(default: no level). Keep its findings for the report; do not let it apply fixes.

## 3. Repo-rules pass

For each changed file, collect **every instruction file that governs it**:

- the root `CLAUDE.md` (General Principles, Serialization Rule)
- the `CLAUDE.md` of the project the file is in (`MagicCore/`, `MtgCore/`, …)
- every `.claude/rules/*.md` whose `paths:` frontmatter matches the file

Read the "rules that hold everywhere" / principles sections of each, and check the diff against
**each rule individually** — quote the rule next to any violation. The rules live in those files,
not here; do not work from memory of them.

Some of them are mechanical — grep the diff rather than reading for them:

| Look for | Why it matters |
|---|---|
| `Func<`, `Action<`, `=>` lambdas stored on a `GameAction`/`GameComponent`/`GameObject` | Serialization Rule — delegates in `GameState` are forbidden |
| A legality check written outside `ValidateAdd` | a second copy drifts; "the AI can do it and I can't" |
| Raw `MoveObject` taking a card off the battlefield/stack | skips the zone-change reset (`MoveCard` in MagicCore, `MoveCardTracked` in MtgCore) |
| A test asserting a number copied out of card content | tests read authored values, never restate them |
| A new card with no ability components / effects | an inert card throws no error — is there a test that the ability FIRES? |
| New `ponytail:` comments | fine, but each must name its ceiling and upgrade path |
| A MagicCore engine or card change — run `python tools/magic_docs.py --check` | the ability catalog and card sheet are generated; stale, the next session builds on a wrong picture |
| A new MagicCore effect/component/condition with no `///` summary | the catalog lists it as "(no summary)" — invisible to the next batch |
| `x is SomeType ? [a] : b` — a type test, then a ternary over a collection expression (grep `is [A-Z]\w*( \{[^}]*\})? \? \[`) | valid C#, but CSharpier 0.29 reads `SomeType?` as nullable and silently skips formatting the file. Use a `switch` expression |
| A test reading a state field in the same expression as a helper that replaces it: `s.GetCard(Play(…))`, `s.Act(new … { CardId = Hand(…) })` | C# reads `s` first — the call runs on the state before the card existed. Bit MagicCore's tests twice |

## 4. Hygiene

- **Tests:** run the test project for every changed project. Report counts.
- **Test count went down?** For each modified test file compare
  `git show HEAD:<file> | grep -c "\[Test"` with the working copy. A drop after "adding tests" is
  how eight tests were once silently overwritten.
- **Docs in the same step:** a new file in a project with a source map must appear in that
  project's `CLAUDE.md` map; a new console prompt must update `RunningSimulations.md`; a new
  command belongs in `Commands.md`.
- **Stories:** the change's story (`docs/stories/`) says what the change did — tasks ticked, new
  decisions dated, changed requirements edited and logged, Next current. Run
  `python tools/stories.py --check`. Propose a commit subject that starts with the story ID.
- **Leftovers:** temp probes, `TmpProbe.cs`-style files, debug prints, files in `shots/`,
  `sim_results/` or the scratchpad that slipped into the change.

## 5. Report

One ranked list, most severe first — each finding: `file:line`, what is wrong, which rule or which
bug, and the concrete scenario where it bites. Then: test results, untracked files made visible,
and anything skipped. End with a one-line verdict: **ready to commit** or **fix first** (and what).
