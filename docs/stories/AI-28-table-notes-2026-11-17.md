---
id: AI-28
title: Shayne's table notes of 2026-11-17 — the AI plays they flagged, each traced and fixed
status: in-progress
area: AI
created: 2026-11-17
---

## Why

Shayne's first games against the new default (AI-26.16: the option price, the held-off share on targets, tap
abilities at their last chance), his Soup-Red-AGT against the AI's RB-Investigator
(`game_records/2026-11-17_105828_seed1084418130.json`, notes beside it). Four of his notes are about the AI; the
table's three are TBL-2.20–2.22.

## Requirements

- [ ] A creature spell the AI can cast this turn is cast BEFORE combat when its own "at the beginning of combat" trigger
      (or a haste body) would give it something this turn — L'Rell's copy off Q, cast in the declare-blockers step.
- [ ] "Target creature you control gets +1/+0" at the beginning of combat goes on a creature that will attack — best a
      token that dies at end of turn anyway (Shayne) — not on one that stays home.
- [ ] A Clue is priced by what it gives — a card for {2} — not as a flat permanent (AI-9's rule, Shayne's "value by use").
- [ ] Tricorder ("whenever equipped creature attacks, surveil 1") doesn't balloon the AI's think time — measured first.

## Decisions

- 2026-11-17, Shayne (asked whether Q's gift can cast the copy mid-combat): curious, asked for a ruling. No ruling for
  Q exists (Scryfall lists none); CR 608.2g settles it: a spell cast during resolution follows 601.2a–i, so the normal
  timing permissions (117.1a, 307.1) don't apply, and the gift's own limit is "Activate only during your turn". The
  AI's cast was LEGAL; only its timing was wrong (no Klingon this turn).
- 2026-11-17, Shayne (told the Captain can't target THIS turn's Klingon — both trigger at the beginning of combat, so
  the target is chosen before the token exists): "Yes good point, they could have still done the same thing but on
  their other 2/1 token and it would have forced me to take 3 damage or trade a 3/3 for a 3/1. So not as good as
  putting it on the 2/1 that would be sacrificed, but still a play the AI should have made."
- 2026-11-17, Shayne: "please put them all into stories, but lets try to tackle them here if we can".
- 2026-11-17, Shayne (AI-28.5, trigger order): "Trigger order seems more complex and its actually kind of a rare
  scenario when it actually matters. I think put it in the backlog for now."
- 2026-11-17, Shayne (AI-28.3, a card against life): his rule is recorded in AI-27's Decisions — 5 life paid at 20 is
  about a card; a card spent to gain life needs 8+. The Clue waits on it.
- 2026-11-17, Shayne (AI-28.4): "Sure we can run a measurement for tricorder".

## Tasks

- [x] AI-28.1 L'Rell's copy before combat. Traced (`--explain=211`): in its main phase the copy scored 11.8 BELOW
      passing (11.83 against 23.64); at the beginning of combat they tied and the tie waited. **The cause was AI-28.2's:**
      the Klingon priced as a 2/1 kept, so the line that made one lost a 2/1 in combat. With a creature gone at the next
      end step counting nothing past this turn, #211 casts the copy in the main phase (21.74, first). Guard:
      `AiPositionTests.LRellsCopy_IsCastBeforeCombat_ForItsKlingon` (kept record `LRellCopyBeforeCombat`, #211) — fails
      with the rule out ("Pass")
- [ ] AI-28.2 Cantankerous Captain's +1/+0 on a creature that attacks (turn 14: it chose L'Rell, who stayed home).
      Traced (`--explain=340`): every target tied at 7.93 and the first listed won, because no imagined line attacked
      after it. **Done: a creature sacrificed or exiled at the next end step is worth nothing past this turn**
      (`Evaluator.CreatureValue`, `ReturnFromExileUnderYourControl.LeavesAtTheEndStep`) — the Klingon now attacks in
      every line. **Still tied:** the menace half ("if creatures you control have total power 8 or greater") needs
      L'Rell's token on the battlefield before the Captain's ability resolves, and the engine stacks a player's
      simultaneous triggers in battlefield order — the rules let the controller choose (CR 603.3b). An engine gap; for
      Shayne (AI-28.5). 1602 tests pass; suite 0 of 288 changed (FRA makes no such token), same cost
- [ ] AI-28.3 A Clue priced by its use. Traced (`--explain=364`): Commander Raffi Musiker's "exile up to one card,
      gain 2 life" (7.93) over "investigate, lose 2 life" (6.43) — the Clue a flat +2.00 ("other permanents"), 4 life
      at 21 worth 3.5, and the look-ahead never cracked the Clue in four turns. **Tried and reverted:** a permanent
      that sacrifices itself priced as its ability (a card, less {2} at 0.5 a mana) — 1.5, BELOW the flat 2.0: an
      unseen card is ~2.5 in the AI's units (half the measured average, lands mixed in), so a Clue can't beat 4 life
      at 21 on price. What a card is worth against life is AI-27's scale. **His rule (Decisions): 5 life paid at 20 ≈ a
      card.** By it a card should be ~5 points; it is 2.65 — with a card at 5 a Clue (5 less {2}) beats the 2.5-point
      life swing and the AI investigates. Waits on AI-27
- [x] AI-28.4 Tricorder and the AI's think time — measured on his game, a quiet machine, 2026-11-17 (findings,
      "AI-28.4"): **Tricorder is not the cause.** Over the 72 AI decisions made with it equipped, the search took 6.16 M
      engine steps with it and 7.15 M with it unattached (0.86). The late turns are slow because the board fills with
      tokens: 15–27 AI decisions a turn, uncapped 41k–169k steps each (53–115 s a turn), and the table's 1 s cap
      overran to 1.1, 1.7 and 2.4 s at his three slowest moves (#347, #412, #529) — checked between batches. Since
      TBL-2.22 the table stays live meanwhile; the time itself is AI-21's (big-board combat) and AI-24's
- [ ] AI-28.5 The order of a player's own simultaneous triggers is the player's choice (CR 603.3b) — the engine stacks
      them in battlefield order. With it, the Captain resolves after L'Rell's Klingon is made and menace is reached.
      An engine change both lanes stand on. **Backlog (Shayne, 2026-11-17): "a rare scenario when it actually matters"**

## Next

AI-28.1 and 28.4 done; 28.2 half done (the token's price) — its other half, AI-28.5 (trigger order), is backlog by his
word; 28.3 waits on AI-27 (his rule: 5 life at 20 ≈ a card; the AI's card is 2.65). Reopen any note's position with the command in
its notes file; `--record=FILE --explain=N` shows what the search imagined before move N (the notes number moves as
the reopen command counts).

## Log

- 2026-11-17: created from Shayne's notes; three of the four traced (above). Q's mid-combat cast is legal (CR 608.2g).
- 2026-11-17: a creature gone at the next end step priced at nothing past this turn — fixes AI-28.1 (#211 casts the
  copy in the main phase, guarded by a kept record) and half of 28.2; the Clue price tried and reverted (28.3); the
  trigger-order gap found (28.5). 1602 tests pass (one new); suite 0 of 288 changed. Not committed.
