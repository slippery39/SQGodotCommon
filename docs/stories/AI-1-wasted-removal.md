---
id: AI-1
title: The AI plays plausibly — no wasted removal
status: done
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

- 2026-10-16, Shayne: **(a) — value a held card by what it can still do.**
- 2026-10-16, Shayne: **measured per card** (a sandbox, MtgSimulator's CardValueSandbox ported), not
  a set average for removal, not (b). Valuing a card by its best use NOW can't work: casting realizes
  that value, holding only keeps a fraction — the held value must come from beyond the current board.
- 2026-10-16, Shayne: the table is **tracked in git** (MtgSimulator's untracked table needed a Godot
  copy that went stale); **GreedyAi stays unchanged** (its fingerprint holds) — only the search uses
  held values; held value is a **setting**, measured against its absence.
- 2026-10-16: measured to the END of the cast's turn with no attacks, not by a rollout — the rollout
  measured combat, not the card (findings). Held value floors at the flat hand value (findings).
- 2026-10-16, Shayne: a held spell is an OPTION, cashed in when keeping it is worth less than playing
  it (AI-5). This story's held value is the first, static price of that option — enough to keep
  premium removal off a Rat; when a long hold should end is AI-5's.

Why the search alone may not fix it (2026-10-16): every non-land card in hand scores a flat 1.2
(`Evaluator.Side`, `MagicCore/Ai/Evaluator.cs`), the Rat dying about 4.5 and the drawn card 1.2 — so casting nets ~+2 over passing,
and any removal is worth less held than almost any target. MtgCore's search has the same flat hand
value (1.4) and its default opponent model never casts, so no better target ever arrives in a
rollout. AI-3's richer opponent models are the other half of the cure.

## Tasks

- [x] AI-1.1 Capture the position as a test — built by hand in AI-2.6:
      `AiPositionTests.RankRat_TheSearch_HoldsItsRemoval` (`[Ignore]`d until this story passes it)
- [x] AI-1.2 Does the search still waste removal? **Yes** (2026-10-16) — see the Log
- [x] AI-1.3 Shayne picks the approach — (a), 2026-10-16
- [x] AI-1.5 Settle the exact form of (a) with Shayne — measured per card, plan below (2026-10-16)
- [x] AI-1.6 The sandbox, `MagicCore/Ai/CardValues.cs`: each card cast into a fixed board and measured
      at the end of its turn (findings: why not a rollout); tests revert-checked
- [x] AI-1.7 The table: `MagicConsole -- --card-values` writes a TRACKED file the Godot table reads
      too; regenerate when the evaluator, the rollout or a card changes (Commands.md)
- [x] AI-1.8 Held value in the evaluator: a non-land card in hand is worth max(flat, fraction ×
      measured) — a setting; GreedyAi unchanged, only the search uses it. Un-ignore the Rank Rat test
- [x] AI-1.9 Measure: the search with values vs without, 72 games, fraction swept; land drops and
      creature casts must not suffer (MtgSimulator's hand term failed exactly there); default if not worse
      — 0.5 is the default (findings)
- [x] AI-1.4 ~~Capture a real game's seed if Shayne meets it again~~ — moved to TBL-2 (TBL-2.3), the
      playtest story where such a report lands

## Next

Done. The search plays with held cards at half their measured value. What stays green:
`AiPositionTests.RankRat_TheSearch_HoldsItsRemoval` and `ABigThreat_TheSearch_SpendsItsRemoval` (the
default search), with their controls (GreedyAi and the plain search still make the bad play). When a
long hold should END — what may still come, time, unused mana, our plan — is AI-5's.

## Log

- 2026-10-16: created from the DesignNotes entry.
- 2026-10-16: open question answered — AI-2 first.
- 2026-10-16: AI-1.6 done — the sandbox measures all 284 playable FRA cards in ~1 s: no negatives,
  twelve honest zeros, Prophesied End 14.2 against the Rank Rat's 4.5. /pre-commit fixes: our own
  choices answered by GreedyAi (a "you may" was always declined, a trigger hit the first legal
  target), every cast line tried (a cap of 16 cut the best), casts only (not landcycling), a fixture
  that fails to reach its main phase throws. Tests: ordering, removal, the end-step sacrifice, a
  "you may" taken, unmeasured cards — revert-checked. The review also noted the rollout's own combat
  (attackers judged alone) still shapes the SEARCH's scores — recorded in AI-3.
- 2026-10-16: AI-1.7 done — `MagicCore/Sets/FRA.values.json` (284 cards, 248 measured), embedded
  with FRA.json so the Godot table has it with no copy; `MagicConsole -- --card-values` regenerates
  it; `CardValuesTests.TheTrackedTable_IsCurrent` re-measures the set (~1 s) and fails while the table
  is stale (revert-checked: one evaluator weight changed fails it). Written with `\n` on every
  machine; values to two decimals so a diff shows only real moves.
  /pre-commit fixes: `CardValues.Fra` parses the table ONCE (AI-1.8 reads it inside the evaluation);
  plain-text JSON (no `'` escapes, so it is searchable and its diff readable); LF pinned in
  `.gitattributes`; one embedded-resource helper (`CardDataSet.ResourceText`) instead of two
  hand-built names; the API is FRA-only, as the measuring is; the console checks it is writing into
  the repository's Sets folder; the load test checks the round trip, not one card's status.
- 2026-10-16: AI-1.8 done — `Evaluator.Evaluate(s, player, HeldValues?)`: our own non-land cards in
  hand at max(flat 1.2, fraction × measured value), the opponent's (hidden) at the average held value
  of a measured card — so "they draw a card" costs on the same scale. Null keeps the flat price:
  GreedyAi and the sandbox pass none (GreedyAi's fingerprint and the table unchanged). The search
  carries it in `Rollout.Settings.Held`, so tree, rollouts, plan checks and the opponent step all
  evaluate alike; `BeamSearchAi.Settings.WithHeldValues()` (fraction 0.5) and `--match=beam-held`.
  **The Rank Rat test passes** (un-skipped; at fraction 0 it fails again — revert-checked), and a new
  position checks holding isn't hoarding: against a 5/5 the removal is spent. Default stays OFF until
  AI-1.9 measures it.
- 2026-10-16: /pre-commit fixes to AI-1.8 — the opponent's hand was not blind (their lands still
  priced 0.3, their spells as unknown): with held values every hidden card now gets ONE price, a
  limited deck's share of lands (17/40) at the land price and the rest at the average held spell;
  the search's internal GreedyAi (mulligans, choices of several picks — a discard) priced flat, so
  GreedyAi takes the same optional held values (off by default: its fingerprint holds); `HeldValues`
  is a class (a record's `with` copy kept a stale blind price); a missing table throws instead of
  silently pricing everything flat; settings compose (`new Settings().WithHeld()`). Controls: the
  PLAIN search still kills the Rat (so the held values are what hold it), and at fraction 2.0 the 5/5
  survives (the hoarding test can fail) — revert-checked, as is the blind hand. Not pinned by a test:
  the multi-pick path and the missing-table throw. **For AI-1.9:** the plan's divergence threshold
  (5.0) was tuned for flat prices; held values move the score more per card, so watch time per
  decision for plans being dropped more often.
- 2026-10-16: AI-1.9 done — swept 0.25 / 0.5 / 1.0 against the plain search, 72 games each:
  38–34, 38–34, 26–46 (1.0 hoards). Land drops held at every fraction. At 0.5, sequential: +11% time
  per decision. **0.5 is the search's default**; `--match=beam-plain` keeps the old search for A/B.
  The harness now reports lands played and spells cast per game, and takes `beam-held=F`. **Story done.**
  /pre-commit fixes: the default is lazy (a missing table no longer poisons the whole type, and
  `WithoutHeld()` needs none) and resolved once; `BeamSearchAi.DefaultRollout` is the base for a
  custom rollout (Rollout.Default has no held values); fractions must be finite and >= 0 (NaN made
  every score NaN); both seats' tallies are diffed each decision (a spell cast on resolution was
  missed); the mirror test pins the counters, a test pins the name parsing (revert-checked); the
  usage line no longer lists a placeholder; the findings say how to reproduce the plain search's 54–18.
- 2026-10-16: **the search wastes the removal too.** In the rebuilt position both GreedyAi and
  BeamSearchAi cast Prophesied End on the non-attacking Rat. By the evaluator: casting nets about +2
  (the Rat ~4.5 gone, −1.2 for their card, −1.2 for ours) while holding is worth the flat 1.2 of a
  card in hand — and in the rollout the BoardOnly opponent never casts, so a better target never
  arrives and waiting can never pay, at any depth.
