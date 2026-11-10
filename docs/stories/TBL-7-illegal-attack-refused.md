---
id: TBL-7
title: The table refuses an attack the rules forbid, instead of dropping the creature
status: done
area: TBL
created: 2026-11-01
---

## Why

Shayne, 2026-11-01 (MC-5.3 batch 4c, Pet Sehlat: "This creature can't attack alone"): "Ideally we would
refuse, but we can also just make a note for now and put it in the backlog for another sprint." Attackers
are declared one creature at a time (`DeclareAttackerChoice`), so the engine judges the whole declaration
as it finishes (`FinishAttackersAction`): a creature that can't attack alone, declared alone, simply
doesn't attack, with a line in the log. The rules (CR 508.1c–d) make that declaration illegal — Arena
won't let you submit it.

## Requirements

- [x] Declaring only creatures that can't attack alone (one Pet Sehlat) is refused at the table, with the
      reason shown, before the declaration is submitted
- [x] The engine keeps judging the declaration as it finishes — the table's refusal is a convenience, the
      rule stays in `FinishAttackersAction`

## Decisions

- 2026-11-10, Claude (open to change): refuse at submit, with the reason shown ("Pet Sehlat can't attack
  alone"), as Arena does — the declaration stays open to fix.

## Tasks

- [x] TBL-7.1 The table checks a declaration against `Combat.CantAttackAlone` before submitting it
      Found half-done: `DeclareCombat` already plays the whole declaration out before committing and refuses any
      pick the engine dropped (a lone blocker on menace's way) — a lone Pet Sehlat was already refused, but as
      "Pet Sehlat → AI isn't allowed". Now the refusal names the rule: "Pet Sehlat can't attack alone" (the rule
      itself stays in `FinishAttackersAction`; the manager only says why). Test
      `ALoneCreatureThatCantAttackAlone_IsRefusedWithWhy_NothingCommitted` (alone: refused, nothing committed;
      with a partner: it attacks). Captured with real clicks: the reason under the declaration, still open

## Next

Not started. Read `MagicCore/Actions/CombatChoices.cs` (`FinishAttackersAction`) and the table's combat
declaration in `SQGodotCommon/MagicTable/`.

## Log

- 2026-11-01: created — Shayne, while Pet Sehlat was scripted in MC-5.3 batch 4c.
- 2026-11-10: TBL-7.1 done (overnight) — the refusal existed; it now says why.
