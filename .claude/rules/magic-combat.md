---
paths:
  - "MagicCore/Turns/Combat.cs"
  - "MagicCore/Actions/CombatChoices.cs"
  - "MagicCore.Tests/Turns/*.cs"
  - "MagicCore/Sets/*.cs"
  - "MagicCore/Sets/*/*.cs"
  - "MagicCore/Effects/*.cs"
  - "MagicCore.Tests/Sets/*.cs"
---

# MagicCore — combat

The real-rules engine's per-mechanic rules for attacking, blocking and the restrictions on both, combat damage, the combat events — each the ONE way that mechanic works
here. The rules that hold on every task are in `MagicCore/CLAUDE.md`. One file per subsystem (DEV-4):
`magic-casting.md`, `magic-triggers.md`, `magic-combat.md`, `magic-mana.md`, `magic-zones.md`, `magic-statics.md`, `magic-ai.md`; a card script or card test loads all but the AI's. A new rule goes in the one file
whose subsystem it lives in.

- **"Attacks a player alone"** is read as attackers are declared: it is the only creature its
  controller has attacking, and it attacks a player, not a planeswalker.
- **How much combat damage a creature assigns** is `Combat.Assigns` — power, or toughness (Ghalta), or a
  negative power as positive (Loot). Combat never reads `s.Power` for damage directly.
- **An attack limit** ("no more than one creature can attack this planeswalker") is an option
  `DeclareAttackerChoice` doesn't offer, read from the declarations already made in that pipeline.
- **"Whenever you attack"** and **"one or more … are dealt combat damage"** are ONE event each per
  declaration or damage step (`AttackersDeclaredEvent`, `CombatDamageToPlayersEvent`) — never one per creature.
- **"Can't block"** is `Combat.CantBlock`, a keyword-like grant read by `Combat.CanBlock` — a static
  (Cleveland Booker's Worm) or until end of turn (La'An).
- **"Can't be blocked by [creatures with …]"** is `CantBeBlockedBy(filter)` on the attacker, the filter asked
  of each blocker (`Combat.CanBlock`). **"Can't be the target of abilities from [sources]"** is
  `CantBeTargetedByAbilitiesFrom(filter)`, asked in `TargetSpec` beside hexproof — abilities only: the source
  is a permanent, not a spell on the stack.
- **"Can't attack alone"** (Pet Sehlat) is `Combat.CantAttackAlone`, a keyword-like grant judged on the whole
  declaration as it finishes (`FinishAttackersAction`): one such creature declared alone doesn't attack; two
  together each aren't alone. The table doesn't refuse the declaration yet (TBL-7).
- **"Enters tapped and attacking"** (Kira Nerys, `YourCreaturesEnterAttacking`) is set in `EnterBattlefield` —
  `AttackingId` the opponent, never declared, so no "whenever … attacks" (CR 508.4); `Combat.Attackers` reads it live.
- **Landwalk and fear are no combat rule** (MC-6.8): `Scripting.Landwalk("Island")` is the plain "can't be blocked"
  under an `If` (the opponent controls one); `Scripting.Fear` is a `CantBeBlockedBy`, granted until end of turn
  through `ModifyUntilEndOfTurn.Gains` (Shizo; the keyword word beside it is only for the table).
