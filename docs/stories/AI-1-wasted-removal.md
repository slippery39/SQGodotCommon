---
id: AI-1
title: The AI plays plausibly — no wasted removal
status: backlog
area: AI
created: 2026-10-09
---

## Why

A playtest only means something against a plausible opponent. On 2026-10-09 the AI cast **Prophesied
End** ("Destroy target creature. If it wasn't attacking, its controller draws a card.") on a
non-attacking **Rank Rat**: premium removal on a 1-drop, AND a card for Shayne. `GreedyAi` simulates
each move one ply and scores the result — it cannot see that the card drawn is worth more than the
rat, or that the spell is better held for an attacker. Source: `DesignNotes.md`, "MagicCore's AI
plays implausibly — one-ply greedy is not enough (backlog)".

## Requirements

- [ ] In the Prophesied End position — rebuilt by hand from the report (AI-2.6), not captured from
      the real game — the AI does not kill the Rat while it isn't attacking, on our turn or theirs
- [ ] "They draw a card" costs in the evaluation; a removal target's value is weighed against the spell's
- [ ] Every change measured over 25+ games per deck pairing and recorded in `docs/findings/magic-ai.md`
      — including when the result is noise (it was, twice)
- [ ] Evaluation terms stay monotonic (`MagicCore/CLAUDE.md` — a cap silently picks the passive move)

## Decisions

- 2026-10-16, Shayne: **AI-2 (the beam search port) goes first**; this story waits to see what the
  search fixes on its own. The Rank Rat position becomes one of AI-2's position tests (AI-2.6).

Open question (2026-10-16, for Shayne): which approach — (a) value a held card by what it can still
do (e.g. removal by the best target it could hit — but MtgSimulator measured a hand-value term in the
SHARED evaluator as a 25-point loss, because it distorted land drops: `.claude/rules/sim-ai.md`);
(b) a rollout opponent that casts threats (AI-3), so holding has something to wait for; (c) both,
measured separately.

Why the search alone may not fix it (2026-10-16): every non-land card in hand scores a flat 1.2
(`Evaluator.Side`, `MagicCore/Ai/Evaluator.cs`), the Rat dying about 4.5 and the drawn card 1.2 — so casting nets ~+2 over passing,
and any removal is worth less held than almost any target. MtgCore's search has the same flat hand
value (1.4) and its default opponent model never casts, so no better target ever arrives in a
rollout. AI-3's richer opponent models are the other half of the cure.

## Tasks

- [x] AI-1.1 Capture the position as a test — built by hand in AI-2.6:
      `AiPositionTests.RankRat_TheSearch_HoldsItsRemoval` (`[Ignore]`d until this story passes it)
- [x] AI-1.2 Does the search still waste removal? **Yes** (2026-10-16) — see the Log
- [ ] AI-1.3 Shayne picks the approach (the open question above, under Decisions)
- [ ] AI-1.4 If Shayne meets it again on MagicTable: capture that real game's seed as a second
      position — the hand-built one lacks the real board and hands

## Next

**Waiting on Shayne: AI-1.3** — no evaluator or model work before he picks (a), (b) or (c). The
test to make pass is `AiPositionTests.RankRat_TheSearch_HoldsItsRemoval` (remove its `[Ignore]`);
its control, `RankRat_Control_GreedyAiStillMakesTheBadPlay`, says the position still provokes the
play — if (a) changes GreedyAi too, the control's job is done: update it, don't delete the evidence.
Then read `MagicCore/Ai/Evaluator.cs`, `MagicCore/Ai/Rollout.cs` and `docs/findings/magic-ai.md`,
and measure with `--match` (`--sequential` for time) before claiming anything.

## Log

- 2026-10-16: created from the DesignNotes entry.
- 2026-10-16: open question answered — AI-2 first.
- 2026-10-16: **the search wastes the removal too.** In the rebuilt position both GreedyAi and
  BeamSearchAi cast Prophesied End on the non-attacking Rat. By the evaluator: casting nets about +2
  (the Rat ~4.5 gone, −1.2 for their card, −1.2 for ours) while holding is worth the flat 1.2 of a
  card in hand — and in the rollout the BoardOnly opponent never casts, so a better target never
  arrives and waiting can never pay, at any depth.
