---
id: TBL-7
title: The table refuses an attack the rules forbid, instead of dropping the creature
status: backlog
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

- [ ] Declaring only creatures that can't attack alone (one Pet Sehlat) is refused at the table, with the
      reason shown, before the declaration is submitted
- [ ] The engine keeps judging the declaration as it finishes — the table's refusal is a convenience, the
      rule stays in `FinishAttackersAction`

## Decisions

Open questions:
- Refuse at submit (a message), or grey out the "attack" button until another creature attacks?

## Tasks

- [ ] TBL-7.1 The table checks a declaration against `Combat.CantAttackAlone` before submitting it

## Next

Not started. Read `MagicCore/Actions/CombatChoices.cs` (`FinishAttackersAction`) and the table's combat
declaration in `SQGodotCommon/MagicTable/`.

## Log

- 2026-11-01: created — Shayne, while Pet Sehlat was scripted in MC-5.3 batch 4c.
