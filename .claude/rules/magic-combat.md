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
- **A creature blocked by two or more divides its damage as its controller chooses** (CR 510.1c since Foundations,
  2024 — no damage assignment order, no lethal-first; trample still needs lethal on every blocker before the player,
  702.19c). `Combat.BeginDamage` asks `AssignCombatDamageChoice` for each blocker but the last (which takes the rest),
  in each damage step, then `DealCombatDamageAction` deals it all at once. The AI's quick answer and every replay of a
  record or suite made before MC-7 (`GameRecord.Unasked`, `PositionSuite.Replay`) is `Combat.DefaultSplit` — the
  split the engine made alone before; a new question like it needs the same three (QuickAnswer, `Settle`'s
  exclusions, the replays) or the look-ahead and old records silently change.
- **An attack limit** ("no more than one creature can attack this planeswalker") is an option
  `DeclareAttackerChoice` doesn't offer, read from the declarations already made in that pipeline.
- **"Whenever you attack"** and **"one or more … are dealt combat damage"** are ONE event each per
  declaration or damage step (`AttackersDeclaredEvent`, `CombatDamageToPlayersEvent`) — never one per creature.
- **"Whenever one or more creatures an opponent controls attack you and aren't blocked"** (Coveted Jewel, MC-4.4l3c)
  is ONE `AttackedUnblockedEvent` as blockers are declared — `Combat.RaiseUnblocked`, from `FinishBlockersAction` and
  from the declare-blockers step that has nothing to block with: its subjects every creature attacking a PLAYER that
  no creature blocks (an attack on a planeswalker isn't "you"), none raised when there is no such attacker. The
  trigger's `Subject` is `Is.Creature & Is.Opponents`; "that player" is the opponent (two players).
- **"Can't block"** is `Combat.CantBlock`, a keyword-like grant read by `Combat.CanBlock` — a static
  (Cleveland Booker's Worm) or until end of turn (La'An).
- **"Can't be blocked by [creatures with …]"** is `CantBeBlockedBy(filter)` on the attacker, the filter asked
  of each blocker (`Combat.CanBlock`). **"Can't be the target of abilities from [sources]"** is
  `CantBeTargetedByAbilitiesFrom(filter)`, asked in `TargetSpec` beside hexproof — abilities only: the source
  is a permanent, not a spell on the stack.
- **"Can block only creatures with flying"** (Brazen Borrower) is `CanBlockOnly(filter)` on the BLOCKER, the filter
  asked of each attacker from its side (`Combat.CanBlock`) — `CantBeBlockedBy`'s mirror.
- **"Can't attack alone"** (Pet Sehlat) is `Combat.CantAttackAlone`, a keyword-like grant judged on the whole
  declaration as it finishes (`FinishAttackersAction`): one such creature declared alone doesn't attack; two
  together each aren't alone. The table doesn't refuse the declaration yet (TBL-7).
- **"Enters tapped and attacking"** (Kira Nerys, `PermanentsEnter(Is.Creature & Is.Yours) { Attacking }`) is set in `EnterBattlefield` —
  `AttackingId` the opponent, never declared, so no "whenever … attacks" (CR 508.4); `Combat.Attackers` reads it live.
  **A token created attacking** (mobilize, Voice of Victory: "create two tapped and attacking 1/1 red Warrior creature
  tokens. Sacrifice them at the beginning of the next end step") is `CreateTokens { Tapped, Attacking,
  SacrificeAtNextEndStep }` (MC-4.4d2b) — `EnterBattlefield(attacking:)`, the same stamp; tapped is its own word there.
- **Goad** (CR 701.15, Undercity's Arena) is `Undercity.Goad(ref)`: the keyword-like grant `Combat.Goaded` until the
  goader's next turn (`ModifyUntilEndOfTurn { UntilYourNextTurn }`), read from that stamp by `Combat.IsGoaded` — an
  effect, so losing all abilities doesn't end it. **"Attacks each combat if able"** (Dragon's Rage Channeler's delirium,
  MC-4.4i2a) is the ability-side twin, the keyword-like grant `Combat.AttacksEachCombat` (a static's, so losing all
  abilities ends it); **"attacks that player this combat if able"** (Ursine Monstrosity, MC-4.4k2b) is a third,
  `Combat.AttacksThePlayer`, stamped until end of turn and read from the stamp as goad's (`Combat.MustAttackThePlayer`:
  an effect, so losing all abilities doesn't end it) — it is also offered no planeswalker; all are ONE question,
  `Combat.MustAttack`. `DeclareAttackerChoice` offers such a creature no "Don't attack",
  so **"don't attack" is ONE answer, `DeclareAttackerChoice.NoAttack`** (the first target for a goaded creature) — the
  look-ahead's quick answer, `WholeAttacks` and the table's `DeclareCombat`; never a literal `CombatOption.None`.
- **Exalted** (CR 702.83, Noble Hierarch) is a scripted trigger, `Scripting.Exalted`, not an engine keyword: a creature
  you control attacking alone (`EventTest.Alone` — a player or a planeswalker), each instance on its own.
- **Landwalk and fear are no combat rule** (MC-6.8): `Scripting.Landwalk("Island")` is the plain "can't be blocked"
  under an `If` (the opponent controls one); `Scripting.Fear` is a `CantBeBlockedBy`, granted until end of turn
  through `ModifyUntilEndOfTurn.Gains` (Shizo; the keyword word beside it is only for the table). **Shadow** (Dauthi
  Voidwalker) is `Scripting.Shadow`: `CantBeBlockedBy(!Is.With("Shadow"))` and `CanBlockOnly(Is.With("Shadow"))`.
- **How many it takes to block an attacker is ONE number, `Combat.MinimumBlockers`** (MC-4.4h2e): menace's two, Troll of
  Khazad-dûm's "can't be blocked except by three or more creatures" (`MinimumBlockers(3)`). Fewer are dropped as blockers
  are declared (`FinishBlockersAction`), and the look-ahead's chumps and joins and the misplay scan read it — never
  `s.Has(attacker, "Menace")` for a block count.
