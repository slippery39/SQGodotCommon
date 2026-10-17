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
- [ ] Every bad play reported becomes a position test it must pass — the first is AI-1's Rank Rat.
      Met for the Rank Rat since AI-1 (2026-10-16): the search prices held cards by their measured
      value by default and keeps its removal (`AiPositionTests`).
- [ ] Measured against `GreedyAi` head-to-head, 25+ games per deck pairing, seats alternated, with
      time per decision beside the win rate — recorded in `docs/findings/magic-ai.md`
- [ ] About 1 second per move on MagicTable. Met since AI-2.7 (2026-10-16) bar the root: a 1 s cap
      (`Settings.Interactive`) — p99 1.12 s, 10 of 1 913 decisions over 1.2 s (slowest 1.65 s);
      a 16-move root alone can take 1.1–2.2 s
- [ ] The opponent inside the simulation is decided in ONE place (the rollout's pass for the other
      player); this story ships `BoardOnly`, AI-3 adds the others and the setting that picks them
- [ ] Hidden information: this story ships "real order" (the AI sees the library as it is, marked
      `ponytail:` and in DesignNotes); AI-3 adds "shuffle what isn't known" and the setting
- [ ] Selectable on MagicTable and the console; it becomes the default only once measured better.
      Selectable since AI-2.7 (the deck select's "Search AI" box, `--search` on both); default: AI-2.8
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
- [x] AI-2.3 Strength harness in `MagicCore/Ai/`: AI vs AI over the FRA deck pairings, seats
      alternated, win rate ± SE, time per decision, actions per game. Run by
      `dotnet run --project MagicConsole -- --match=A,B --games=N`; self-checks as an `[Explicit]`
      test: Greedy-vs-Greedy ≈ 50% and Greedy-vs-random clearly apart (a harness that can't see a
      difference measures nothing)
- [x] AI-2.4 Rollout: finish the current turn, then N half-turns — our cheap greedy policy, the
      opponent through the seam (`BoardOnly`: attacks with its board, blocks what it survives, never
      casts); terminal discount; hidden information through its seam (real order). Measure the cost
      of one rollout — and only then make `Evaluator` cheaper if it shows (the /pre-commit review of
      AI-2.2 noted `CreatureValue` allocates a keyword array per creature and reads toughness 3 times)
- [x] AI-2.5 The beam: root = this decision's moves (or a choice's options), expand our decisions to
      the turn boundary, opponent decisions by the model, prune, branching caps, rollout budget,
      fastest win, pass bias; the committed chain, re-searched on divergence
- [x] AI-2.6 Position tests: AI-1's Rank Rat, plus each new bad play — `AiPositionTests`; the Rank Rat
      FAILS for the search too, so it is skipped with its reason and owned by AI-1
- [x] AI-2.7 Selectable on MagicTable and the console; ~1 s per move on the table (commit pending)
- [ ] AI-2.8 Measure against `GreedyAi`, record in `docs/findings/magic-ai.md`; default if better

## Next

Next task: **AI-2.8** — measure `beam` against `GreedyAi` again (`--match=beam,greedy --games=72
--sequential`, Release): the 54–18 was the plain search, before AI-1's held values. If it is still
better, make it the default — `MagicTableManager.Search`'s default, the deck select's box ticked,
and the console's `--search` reversed — and record it in `docs/findings/magic-ai.md`.
`beam` in `--match` is UNCAPPED on purpose: a wall clock in a sim run makes results depend on the
machine; the table's cap (`Settings.Interactive`) is never measured for strength.
Not done, deferred with costs in DesignNotes: the search runs on the table's main thread (the window
stalls up to ~1.1 s while it thinks), and the root level ignores the cap.
New bad plays go into `MagicCore.Tests/Ai/AiPositionTests.cs`. Read first: `MagicCore/Ai/BeamSearchAi.cs`,
`MagicCore/Ai/AiMatch.cs`, `.claude/rules/sim-ai.md`.

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
- 2026-10-16: AI-2.3 done — `IAi` (GreedyAi implements it), `RandomAi`, `AiMatch`, and
  `MagicConsole --match=A,B --games=N [--sequential]`. Self-checks, both in the normal suite: an AI
  against itself scores exactly 50% ± 0 (revert-checked by breaking the seat swap), and
  `GreedyAiTests.BeatsRandomPlay` now runs through the harness (71–1 at 72 games). The standard error
  is over mirror pairs, not games. Baselines in `docs/findings/magic-ai.md`: GreedyAi takes ~2 ms per
  decision sequentially, parallel runs inflate that 7–8x, and the Jace deck goes 2–22 in GreedyAi's hands.
- 2026-10-16: AI-2.4 done — `Rollout.Score(s, me, horizon, settings)`: finishes the current turn and
  plays `LookaheadTurns` (2) more to an ABSOLUTE horizon turn, our side by cheap greedy (land, then up
  to `ActionsPerTurn` (1) plays at sorcery speed if not worse; attacks by settled score), the opponent
  `BoardOnly` (attacks and safe blocks, never casts); a decided game decays 0.95 per turn. `Settle` and
  `QuickAnswer` moved from GreedyAi to `Lookahead` (fingerprint unchanged). Tests: lethal on our
  board, the opponent's lethal, a sooner win higher, determinism — each revert-checked. Cost: ~22 ms
  per rollout; attack declarations are ~45% of it.
- 2026-10-16: /pre-commit fixes to AI-2.4 — the terminal discount counted from each rollout's own
  start, not the search's root (two candidates losing on the same turn scored differently); blocks
  were safe-only, so facing lethal we never chump-blocked — now judged by what they lead to, like
  attacks; a forbidden `is X ? [..] :` ternary became a switch; the two one-value setting enums were
  never read, so they are gone until AI-3 brings a second value (requirements reworded); the
  ceilings are marked `ponytail:` and in DesignNotes; `magic_docs.py`'s enum regex now stops at a
  nested enum's own brace. Each fix pinned by a revert-checked test.
- 2026-10-16: AI-2.5 done — `BeamSearchAi` (`--match=beam,…`): the beam over our decisions to the
  turn's end, rollout-scored, fastest win, pass bias, plans committed and re-found by the new
  `MagicRules.MoveKey`; mulligans stay GreedyAi's. **Beats GreedyAi 54–18 (75.0% ± 4.2pp) over 72
  games; 177 ms per decision on average, 3.4 s at the slowest** (findings). /pre-commit fixes: the
  plan fell out of step after our own spells (now we pass on our own stack object for real too, as
  the tree does); a plan replayed a pass over an opponent's unforeseen spell (dropped now); loyalty
  moves were never re-found (MoveKey spells them out); the rollout answered the opponent outside
  `OpponentStep`; the opponent's turn now gets one ply per priority window (blocks still branched).
  Tests: lethal taken, chump block, determinism, MoveKey (X; loyalty) — revert-checked. Not pinned
  by a test: the plan fixes — measured instead (same strength, 14% faster).
- 2026-10-16: AI-2.6 — `AiPositionTests` rebuilds the Rank Rat report by hand (two Plains,
  Prophesied End in hand; the opponent's Rank Rat on the battlefield, not attacking). GreedyAi kills
  the Rat there, so the position is the report. **The search kills it too** — the "every bad play is
  a position test it passes" requirement is unmet for this one, and AI-1 owns it; the test is
  `[Ignore]`d with the reason so every run shows it (un-skipped, it fails — checked). /pre-commit
  fixes: the test plays THEIR turn too (an instant cast in their upkeep is the same bad play) and
  fails on the Rat dying while not attacking; a live control asserts GreedyAi still makes the play
  there, so a broken position can't pass for nothing (revert-checked); a stuck driver loop now fails
  instead of passing; the three AI test classes share `TestGames.EmptyHands` and `TestGames.PlayAis`.
- 2026-10-16: AI-2.7 done — the search on the table (the deck select's "Search AI" box; `--search`)
  and the console (`--search`); GreedyAi stays the default until AI-2.8. `Settings.TimeLimit` caps a
  decision for people only (`Settings.Interactive`, 1 s). **Checking the elapsed time between levels
  capped nothing** — a level costs 0.4–0.6 s mid-game, so 91 of 1 899 decisions still ran past 1.5 s;
  the cap now starts a level only if its estimate (one parallel batch per kept line, at the last
  batch's cost) fits: 3 of 2 048 past 1.5 s, p99 1.02 s (findings). Left over: the 16-move root
  (up to 2.2 s), `ponytail:` and in DesignNotes. On the table the slowest `StepAi` took 1.08 s, on
  the main thread — DesignNotes too. Tests: no time left = a one-level search, decision by decision
  over a real game (revert-checked: without the gate it differs at decision 3); the table plays the
  search to turn 3 and Replay keeps it. MagicCore.Tests 923, MagicTable tests 12, green.
- 2026-10-16: /pre-commit fixes to AI-2.7 — the level estimate was too cautious (the root's whole
  time as the first batch; lines whose turn was over counted as batches), so the capped search often
  stopped at one level: now each line is expanded only if one more batch fits, the first estimated
  from the root's scaled to `ExpandBranching`. Capped, it reaches the third level in 363 searches
  against the uncapped 469; p99 1.12 s, slowest 1.65 s (findings). Undo now rebuilds the AIs — the
  search's committed plan would have replayed past the undone decisions (not pinned by a test: it
  needs a contrived interrupted plan). The cap test now also checks that a one-hour limit searches
  every level (revert-checked: a flipped gate fails both cases, an estimate that always overshoots
  fails the hour case). MagicCore.Tests 924, MagicTable tests 12, green.
