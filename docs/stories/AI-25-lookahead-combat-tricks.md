---
id: AI-25
title: The look-ahead's attacker uses its tricks after blocks — a block isn't free when the attacker can pump
status: done
area: AI
created: 2026-11-13
---

## Why

Shayne's first notes session on the table (TBL-10.3, 2026-11-13, seed 1 AI vs AI): "AI2 should not block the Edgar here.
Trading the 2/3 Greenhouse [Propagator] for a 1/1 rat does not seem like a positive ev play in isolation. This is not a
trade a reasonable person would make with no additional information." Edgar, Ancient Bloodlord (2/3) attacked; AI 2
blocked with Greenhouse Propagator (2/3); AI 1 paid {2} and sacrificed Rank Rat — "{2}, Sacrifice another creature or
planeswalker: Put a +1/+1 counter on Edgar" — Edgar became 3/4 and the Propagator died.

The search's own account (`MagicConsole --record=… --explain=175`, the full search, same choice): Block 0.95, Don't
block −1.19. In the block line the attacker does NOTHING after the block — no activation — so the block only saved 2
life. The look-ahead's attacker never uses an activated ability once blockers are declared, so Edgar's trick is
invisible to the defender, and any "pump after blocks" card makes a block look free.

## Requirements

- [x] The kept position plays as Shayne says: the search does not block Edgar with the Propagator while the attacker
      has {2} and a creature to sacrifice — with a control (no mana, or nothing to sacrifice: the block stays)
- [x] The look-ahead's attacking side may use its activated abilities (and instants, if AI-3.6's responses don't
      already) after blockers are declared, when it scores — behind a switch
- [x] Measured the AI-6 way (the suite, then `--until-clear`), kept by AI-5's rule; cost per decision reported

## Decisions

- 2026-11-13, Shayne: write the notes' causes up as stories (this one is cause C of three; A is AI-13.13, B AI-9.9).
- 2026-11-13 (Claude): the skip is `Rollout.IsManaPump` — AI-17.10's trick window (`TheirResponse`, their own combat
  after blocks) took a mana-costed ability only if its cost picked no card; its own ponytail named the ceiling ("one
  whose cost picks a card (a sacrifice), stays unseen"). Lifted ONLY in that window, still mana-costed (bounded by
  mana): `Evaluator.Terms.TheirSacrificeTricks`, `+notheirsacrifices`. A free ability, and picks outside their
  combat, stay unseen.

## Tasks

- [x] AI-25.1 The position as a test: `KeptRecords.Before("ShayneNotesSeed1", 175)` (MagicCore.Tests/Records, kept
      2026-11-13 — today's default search still blocks: the control holds), plus a hand-built control — DONE as
      `AiPositionTests.ABlock_ThatLosesToTheirSacrificePump_IsNotMade`, the control being the switch off (AI-17.10's
      shape): off, it blocks (c42); on, it doesn't — the block line now imagines "Activate Edgar, sacrificing Rank
      Rat" and scores −13.10 against Don't block −1.19
- [x] AI-25.2 Find where the attacker's after-block actions are skipped (tree combat or `Rollout`), then the change
      behind a switch, and the AI-6 measurement — BUILT (`IsManaPump(picks:)`); suite: 0 of 288 decisions moved by it
      (one change, Compel Brutality's target at seed 2 #81, shows with the switch off too — older drift), cost 10633
      vs 10594 steps (+0.4%); MagicCore.Tests 1587 passed. MATCH: 96 games, 50.0% (48–48), no difference; cost per
      decision 4364 vs 4399 steps. KEPT (the default), `docs/findings/magic-ai.md` "AI-25"
- [x] AI-25.3 The rollout's OWN blocks face the attacker's trick (found at #97, 2026-11-13: with the Vale kept, the Rank
      Rat discard took Koth — Shayne: "Why would trading it in the future cause him to discard it now?" — the play-out
      blocked Edgar with Koth and AI 1's sacrifice pump killed it, no trade). `Rollout.BestAlone` judged each block by
      `Lookahead.Settle`, which only passes in combat; `FacingTheirTrick` now answers the rest of the declaration
      cheaply and gives the attacker `TheirResponse` first. `Evaluator.Terms.BlocksSeeTricks`, `+noblocktricks`. At #97:
      Forest discarded (test `KeepingACreature_IsNotJudgedByTheRolloutBlockingIntoAPump`, control: the term off →
      Koth). Alone it also keeps the Vale there (with ColorShort off: Forest). OURS ONLY (`Rollout.Choose(ours:)`): first
      built for both sides, the suite moved a Rampart Hunter attack (seed 2 #85) against the oracle at +6.4% cost — the
      rollout's opponent fearing our tricks; narrowed, 0 of 288 moved, cost 10660.5 vs 10633.1 (+0.3%).
      MagicCore.Tests 1589 passed. MATCH: 192 games, 48.4% ± 0.9pp (93–99), no 5pp difference (leans negative, 1.8 SE);
      cost +1.2% a decision. KEPT by AI-5's rule (findings "AI-25.3")

## Next

Done. AI-25.3's match leaned negative inside the noise (48.4%): if a sealed run or a later match dips, rerun
`beam,beam+noblocktricks` on new seeds first. Left unseen (`IsManaPump`'s ponytail): a free ability; the rollout's
opponent fearing OUR tricks (tried, moved a decision against the oracle).

## Log

- 2026-11-13: created from Shayne's first notes session (TBL-10.3); the search's explanation read at #175.
- 2026-11-13: built behind `TheirSacrificeTricks`; the position test passes with its control; suite unchanged by it.
- 2026-11-13: the match — no difference over 96 games; kept as the default. Done.
- 2026-11-13: reopened for AI-25.3 — the rollout's own blocks still walked into the pump (the Koth discard at #97).
- 2026-11-13: AI-25.3 measured (48.4% over 192, no difference) and kept; done again.
