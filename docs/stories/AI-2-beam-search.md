---
id: AI-2
title: A search AI for MagicCore — port the multi-turn beam search
status: in-progress
area: AI
created: 2026-10-09
---

## Why

MagicCore has only the one-ply `GreedyAi`: no turn planning, no holding up mana, a passive opponent
in its simulation, and it loses slow races to evasive fliers (`DesignNotes.md`, "MagicCore known
ceilings"). MtgCore's game already has a much stronger AI — `MultiTurnBeamSearchAiStrategy`
(`MtgSimulator/`, 1,455 lines; MtgCore's default, `MtgGameManager.cs:84`): a beam over the current
turn plus greedy rollouts of future turns, with choices branched. It was never ported.

## Requirements

- [ ] A search AI that plans its own turn through MagicCore's priority, stack, combat and choices
- [ ] Every bad play reported becomes a position test it must pass — the first is AI-1's Rank Rat
- [ ] Measured against `GreedyAi` head-to-head, 25+ games per deck pairing, seats alternated, with
      time per decision beside the win rate — recorded in `docs/findings/magic-ai.md`
- [ ] About 1 second per move on MagicTable
- [ ] The opponent inside the simulation sits behind a seam; this story ships `BoardOnly`, AI-3 adds the others
- [ ] Hidden information is a swappable mode; this story ships "real order" (the AI sees the library
      as it is), AI-3 adds "shuffle what isn't known"
- [ ] Selectable on MagicTable and the console; it becomes the default only once measured better
- [ ] Deterministic for a given seed — no thread order or wall clock reaches a decision in a sim run

## Decisions

2026-10-16, Shayne:
- **Port first.** The beam search is known to handle complex interactions better than what exists;
  port it as the starting point and see what comes with it, rather than fixing one scenario (AI-1) first.
- **Judged by** bad-play positions + head-to-head (above).
- **About 1 second per move** on the table.
- **The simulated opponent must be swappable and measured** (strength vs simulation time), not
  committed to one model — that is AI-3.

Carried over from MtgSimulator (`.claude/rules/sim-ai.md` — each one cost real time there):
- Search cost is bounded by a **deterministic rollout budget**; wall clock only as an extra cap on
  the interactive table, never in a sim run. Parallel work fills pre-allocated arrays, ties broken by index.
- **Terminal rewards decay** per half-turn; compare against a win THRESHOLD, never `== WinScore`.
- A winning line found is taken — the **fastest** win, not the first.
- Rollout choices are answered **by their owner**, never by whose simulated turn it is.
- An action is applied **and its choices drained** before its state is scored.
- **Passing must strictly beat acting** to be chosen (an unseen benefit must not make an action lose ties).
- To be measurable, every behaviour change gets a **constructor switch that turns it off**.

2026-10-16, Shayne (second round):
- **Hidden information is two swappable methods, both measured** — not one choice. A pure shuffle is
  wrong: surveil, "put on the bottom" and mulligan bottoms make some positions KNOWN. So: (a) keep the
  real order — the AI "plays around" what is really there; (b) shuffle only what isn't known — which
  needs the engine to track who knows each library card's position (it tracks nothing today). (a)
  ships here; (b) is AI-3.
- **Our rollout policy: cheap greedy first**, as MtgCore. `GreedyAi` there is a measured option in AI-3.
- **No simulator project yet** — the search lives in `MagicCore/Ai/`, beside `GreedyAi`.
- **Searching afresh at every decision is known to balloon time** — plan commitment is needed, now
  or soon. See the open question.

2026-10-16, Shayne (third round):
- **Port MtgCore's committed chain here** (AI-2.5): the beam's best line is replayed while the
  position stays within a score threshold of what it predicted; re-search when it diverges. Matched
  on `DistinctMoves`' full identity (card, X, targets, modes, fodder), so MtgCore's X=0 replay bug
  cannot recur. The higher-level strategic plan is **AI-4**.
- **The harness runs from the console** (`--match=…`) **and as an `[Explicit]` test** for its self-checks.

Left out, with reason: MtgCore's potential evaluators (fast mana) and `CardValueTable` — FRA has
neither the fast-mana cards nor a value table.

## Plan — how MtgCore's search maps onto MagicCore

| MtgCore | MagicCore |
|---|---|
| `SelectAction` on our turn, `ResolveChoice` for choices | one `Decide` at every decision: priority on either turn, or any pending choice we own |
| a turn = our actions until `EndTurnAction` | a turn = our decisions (actions AND choice answers — attacks are `DeclareAttackerChoice`s) until the turn passes; passing priority on an empty stack advances the step |
| attacks are actions; no blocking | attacks and blocks are per-creature choices; the simulated opponent must BLOCK too |
| opponent decisions don't occur inside our turn | they do (priority after each spell, blocks): the opponent model answers them |
| `MtgActionGenerator.GetLegalActions` | `LegalActions` + `DistinctMoves`, after `AutoPass` |
| `StateEvaluator` / `IStateEvaluator` | `GreedyAi.Evaluate`, moved to its own class and shared |

A decision on the OPPONENT's turn (respond, block) has no turn of ours to expand: every candidate is
a leaf scored by finishing their turn with the opponent model and rolling out — one ply plus rollout,
exactly as MtgCore treats a decision at a turn boundary.

## Tasks

- [x] AI-2.1 Read `MultiTurnBeamSearchAiStrategy.cs` and bring a porting plan — this story
- [x] AI-2.2 The evaluator out of `GreedyAi` into `MagicCore/Ai/Evaluator.cs`, behaviour unchanged —
      proven by a golden fingerprint of GreedyAi's decisions over seeded games (the existing golden
      replays play random moves, so they cannot see an AI change)
- [ ] AI-2.3 Strength harness in `MagicCore/Ai/`: AI vs AI over the FRA deck pairings, seats
      alternated, win rate ± SE, time per decision, actions per game. Run by
      `dotnet run --project MagicConsole -- --match=A,B --games=N`; self-checks as an `[Explicit]`
      test: Greedy-vs-Greedy ≈ 50% and Greedy-vs-random clearly apart (a harness that can't see a
      difference measures nothing)
- [ ] AI-2.4 Rollout: finish the current turn, then N half-turns — our cheap greedy policy, the
      opponent through the seam (`BoardOnly`: attacks with its board, blocks what it survives, never
      casts); terminal discount; hidden information through its seam (real order). Measure the cost
      of one rollout — and only then make `Evaluator` cheaper if it shows (the /pre-commit review of
      AI-2.2 noted `CreatureValue` allocates a keyword array per creature and reads toughness 3 times)
- [ ] AI-2.5 The beam: root = this decision's moves (or a choice's options), expand our decisions to
      the turn boundary, opponent decisions by the model, prune, branching caps, rollout budget,
      fastest win, pass bias; the committed chain, re-searched on divergence
- [ ] AI-2.6 Position tests: AI-1's Rank Rat, plus each new bad play
- [ ] AI-2.7 Selectable on MagicTable and the console; ~1 s per move on the table
- [ ] AI-2.8 Measure against `GreedyAi`, record in `docs/findings/magic-ai.md`; default if better

## Next

Next task: **AI-2.3**, the strength harness. Read first: `MagicCore/Ai/GreedyAi.cs`,
`MtgSimulator/MultiTurnBeamSearchAiStrategy.cs`, `.claude/rules/sim-ai.md` (the measured lessons),
`MagicCore/MagicRules.cs` (`DecidingPlayerId`, `LegalActions`, `AutoPass`, `DistinctMoves`).
Callers to switch in AI-2.7: `MagicTableManager.cs` (`_ais`, and the stand-in at line ~203),
`MagicConsole/Program.cs`.

## Log

- 2026-10-16: created from the old handoff's "Later" list.
- 2026-10-16: Shayne chose to start AI-2 now, before AI-1 and TBL-2. Port plan written (AI-2.1).
- 2026-10-16: second round answered — hidden information as two modes, cheap rollout policy, no
  simulator project, plan commitment needed. Requirement added: the hidden-information seam.
- 2026-10-16: third round — chain commitment in AI-2.5, strategic plans split to AI-4, harness on
  the console. Go for AI-2.2.
- 2026-10-16: AI-2.2 done. `GreedyAiReplayTests` fingerprints GreedyAi-vs-GreedyAi over 12 seeded games
  (every ordered FRA pairing twice, ~6 s, in the normal suite) in `GoldenAiReplay.txt`, recorded BEFORE
  the move; all 12 replay identically after it. Revert-checked: the hand weight 1.2 → 1.3 fails it.
  `Evaluator.Evaluate(s, player)` is static; `GreedyAi.Evaluate` is gone (tests call `Evaluator`).
  MagicCore.Tests 896 green; SQGodotCommon.Tests and MagicConsole build.
- 2026-10-16: /pre-commit fixes — the fingerprint now records each decision's option count (an
  option offered or dropped shows even when the pick is the same) and is computed from the FRA deck
  list (a new deck is covered). Re-recorded from HEAD's code in a temporary worktree, so it still
  proves the move changed nothing. Deferred: an `Explain` breakdown of the evaluator's terms (the
  root rule: Explain is the implementation, Evaluate the wrapper) — build it when an inspector or an
  A/B evaluator first needs one, not before.
