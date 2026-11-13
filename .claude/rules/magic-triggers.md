---
paths:
  - "MagicCore/MagicGame.cs"
  - "MagicCore/MagicPlayer.cs"
  - "MagicCore/Turns/Triggers.cs"
  - "MagicCore/Turns/TurnStructure.cs"
  - "MagicCore.Tests/Turns/*.cs"
  - "MagicCore/Sets/*.cs"
  - "MagicCore/Sets/*/*.cs"
  - "MagicCore/Effects/*.cs"
  - "MagicCore.Tests/Sets/*.cs"
---

# MagicCore — triggers, turns and "this turn"

The real-rules engine's per-mechanic rules for what triggers and from where, intervening and reflexive triggers, delayed triggers, the turn's steps and tallies, extra turns, winning and losing — each the ONE way that mechanic works
here. The rules that hold on every task are in `MagicCore/CLAUDE.md`. One file per subsystem (DEV-4):
`magic-casting.md`, `magic-triggers.md`, `magic-combat.md`, `magic-mana.md`, `magic-zones.md`, `magic-statics.md`, `magic-ai.md`; a card script or card test loads all but the AI's. A new rule goes in the one file
whose subsystem it lives in.

- **"At the beginning of [step]"** triggers on a `StepBeganEvent`, raised by `EnterStep` for upkeep,
  draw, the first main phase, beginning of combat and end. **"This turn" lives in `MagicPlayer.ThisTurn`** (a `TurnTally`:
  lands, spells, cards drawn, life gained and lost, surveils), REPLACED whole for BOTH players as each turn
  begins — "this turn" is every turn, not only yours, and a new count cannot miss the reset. Update
  it with `s.Tally(…)`. The finished turn's tally becomes `LastTurn` ("last turn" = the turn before
  this one, whoever's). Creatures that died this turn (anyone's) are on `MagicGame`. **A tally holds
  counts only:** one card's "this turn" effect (Hall of Echoes' no legend rule, Theorist's Proxy) is
  `new ThisTurn(new X())` — an object in the command zone, which the table shows, ended with the tally;
  `X` is a `ThisTurnEffect`, read with `s.ThisTurnEffects<X>(player)`, and one used up is removed.
- **"This ability triggers only once each turn"** is `TriggeredAbilityComponent.OncePerTurn`, stamped
  on the card as it goes on the stack (`PutOnStack`) — a new object (CR 400.7) may trigger again.
- **"Entered this turn"** is `Card.EnteredOnTurn`, stamped by `EnterBattlefield` — not
  `ControlledSinceTurn`, which a control change would also move — it is gone once the permanent leaves.
  **"A [subtype] entered under your control this turn"**, even one that left again (Sulu), is
  `SubtypeEnteredThisTurn`, read from `TurnTally.SubtypesEntered` (written by `EnterBattlefield`). **"Attacked this turn"** is
  `Card.AttackedOnTurn`, stamped as attackers are declared.
- **"Doesn't untap during its controller's untap step"** is ONE piece, `DoesntUntap(filter)` (MC-6.15): the HOLDER's
  rule over what its filter matches, from its side — Traxos' own (`Is.Source`), Infinite Coursework's and Marooned's
  (`Is.AttachedToSource`) — read with `AbilitiesOf` on the holder, so Traxos losing all abilities untaps while the
  enchanted creature losing its own stays down. Read by the untap step
  ONLY — an untap effect still untaps it (Traxos' own trigger). A stun counter, by contrast, eats every untap.
- **A subtype narrows a trigger or a static:** the trigger's `Subject` ("whenever a Plains you
  control enters" is `Is.Land & Is.Yours & Is.Subtype("Plains")` — no prompt for any other land) and a static's scope
  (`Is.Subtype("Thopter")`: "Thopters you control have haste"). Never `TriggeredAbilityComponent.If`
  for this: that is checked again on resolution, so a Plains bounced in response would lose Koth's counter — there's no "if" in the text.
- **"Whenever you cast a spell that targets …"** reads the spell's targets from its `OnStackComponent`;
  "your first noncreature spell each turn" is numbered AS IT IS CAST (`SpellCastEvent`) — a count read
  later would let a spell cast in response steal the trigger.
- **"You win the game"** (`WinTheGame`) ends it at once: a same-moment draw from an empty library never
  reaches the state-based check.
- **Where triggers are read from:** permanents on the battlefield; a dying card's own "when this
  dies"; a graveyard card's abilities marked `FromGraveyard` (Darklight Phoenix) — those, and only
  those, trigger from there; and a trigger granted until end of turn (`UntilEndOfTurn.Trigger`,
  Lyra), which ends at cleanup or a zone change like any other until-end-of-turn effect.
- **"Becomes the target"** is raised when a spell or ability goes on the stack with its targets —
  `CastSpellAction` and `StackResolution.Push` (every ability), the one place each.
- **"When you do"** (a reflexive trigger, CR 603.12) is the `WhenYouDo` effect: its targets are chosen
  as it triggers, after the thing done, and it goes on the stack above — never fold it into the effect.
  It triggers only if the thing was done: after a mill it goes in `Mill.IfMilled` (an empty library mills nothing).
- **An intervening "if" (CR 603.4) is `TriggeredAbilityComponent.If`**, never a bare `If` effect:
  checked as it triggers (`Collect`) AND again on resolution (`QueueTargets` wraps the effects).
  Edgar with a spell cast in response to his trigger gets nothing. A condition on the EVENT itself
  can't change before resolution, so it may be an `EventTest` on the `Trigger` instead (Way of the Mind Sculptor's
  "removed two or more"). `If` runs its steps as a child pipeline — so a surveil inside it finishes
  before the next step — and its steps' outputs do not reach the steps after it.
- **Prowess is a scripted trigger** (`FraCards.Prowess`), not an engine keyword — each instance
  triggers on its own (Ruric Thar has two).
- **"Entering doesn't cause abilities to trigger"** (Karn, Argent Defender) drops those entering
  events in `Triggers.Collect` before ANY ability is matched — read at the next priority, so Karn
  entering alongside a creature silences it too, as the rulings say.
- **"Whenever this creature is dealt damage"** triggers on `CreatureDealtDamageEvent` from `Damage.Deal`;
  "that many" is the trigger's X (`Triggers.Pending.X`). **Cards put into a graveyard from a library**
  (mill, surveil, anything) are counted in `MoveCard` (`TurnTally.CardsFromLibraryToGraveyard`).
- **A delayed trigger on a permanent** ("exile it at the beginning of the next end step") is a
  `DelayedTrigger` component: kept when it loses all abilities, not copied, gone with a zone change.
  On a card it EXILED (`Flicker.AtNextEndStep`, Ghost of Illyria: "return that card at the beginning of the
  next end step") `Triggers.Collect` reads that one component there, never the exiled card's own abilities —
  and on a card that DIED (`AtNextEndStep.On`, Dr. McCoy: "return that card … at the beginning of the next
  end step"), read in the graveyard the same way; leaving the graveyard strips it (a new object).
  With no card to hold it ("return a land you control", Erica Ortegas) it is `AtNextEndStep`: an object in
  your command zone, removed once it has resolved. Set up during the end step, either waits for the next turn's.
  **"At the beginning of your next main phase"** (Mana Drain) is `AtNextEndStep { NextMainPhase }`: your precombat main,
  or this turn's postcombat main when set up on your turn before it (so the postcombat main raises a `StepBeganEvent`
  too). **A number fixed as it is set up** ("equal to that spell's mana value") is `AtNextEndStep.X`, the delayed
  trigger's `TriggeredAbilityComponent.X` — its effects read it as `Count.X`.
  **One on a permanent whose controller is someone else** (The Wrath of Khan's "at the beginning of the next
  combat this turn, gain control of that creature", `AtBeginningOfNextCombat`) names its controller
  (`DelayedTrigger.ControllerId`, CR 603.7d): `Triggers.Collect` reads those as it reads an exiled card's —
  never as the permanent's own, which would make its controller "you".
- **Emblems** (CR 114) are cards in their owner's COMMAND zone (`GetEmblem`): `Triggers.Collect` and
  the statics board read that zone beside the battlefield, so an emblem's trigger has a source
  ("this emblem deals 5 damage") and its anthem applies. Nothing removes them.
- **"Until your next turn"** is `UntilEndOfTurn.UntilTurnOf` (the player): cleanup leaves it, and
  that player's untap step ends it (`TurnStructure.EndUntilYourNextTurn`). One on a permanent rides the
  permanent (Garruk's −4/−1); "until your next turn, whenever …" is an effect card in the command
  zone (`UntilYourNextTurnWhenever`), so it outlives the planeswalker, and is removed when it ends.
- **"When you cast this spell"** (`TriggerEvent.SelfCast`, Emrakul) is read from the spell on the stack.
- **Necrodominance's rules each live in their one place:** a skipped draw step never begins (`EnterStep`),
  hand size is `TurnStructure.MaxHandSizeOf`, and "exile instead of your graveyard" is decided in `MoveCard`
  with the other exile-instead cases — so nothing of yours dies.
- **"Whenever you attack WHILE …"** (power 8) is `Trigger.While` — part of the trigger condition, judged
  only as the event happens (`Triggers.Matches`); never `TriggeredAbilityComponent.If`, which is judged
  again on resolution (release notes: "it doesn't matter … when [ability] resolves").
- **Sagas** (CR 714) are `SagaComponent(chapters)` + `Scripting.Chapter(n, …)`: a trigger on
  `CountersPutEvent` that fires as the lore count goes from below n to at least n (`Trigger.Chapter`). The
  lore counter is added as it enters (`EnterBattlefield`) and as its controller's precombat main phase
  begins — a TURN-BASED action in `EnterStep`, no stack (Shayne, 2026-11-01). The state-based check
  sacrifices a Saga at its last chapter only once no chapter ability of it is waiting: collected and not
  yet on the stack (`GivePriority` passes them to `StateBasedActions.Check`) or on the stack.
- **"Whenever [a creature with …] deals combat damage to a player"** — not "this" — is
  `Trigger.CombatDamageBy`, a filter on the damage's SOURCE (Phlox); the event's subject is the player.
- **"Sacrifice it at the beginning of the next end step"** on a new token is `CreateTokens`/`CreateTokenCopy`
  `.SacrificeAtNextEndStep` — a `DelayedTrigger` added after it enters, like `ExileAtNextEndStep`.
- **"Whenever …, this turn"** from a resolving ability (The Naked Time's chapter II) is `WheneverThisTurn`:
  the trigger on a command-zone object, removed with the turn's other "this turn" effects.
- **"Whenever a card leaves your graveyard"** is `LeftGraveyardEvent`, raised by `MagicState.MoveCard` for every
  path out — cast, exiled, returned, shuffled, paid as a cost; a token isn't a card. **"One or more"** is
  `Trigger.OneOrMore`: one trigger per priority pass, everything that left since a player last had priority
  (Shayne, 2026-11-01).
- **"[Permanents] you control have '[triggered ability]'"** is `GrantsTriggeredAbility(scope, ability)`:
  `Triggers.Collect` gives it to each permanent the scope matches, and "this creature" is that one.
- **"Whenever you draw a card"** is `DrewCardEvent`, raised by `MagicState.Draw` once per card (Data, New Life).
- **"When that creature dies this turn, you …"** (A Good Day to Die) is `WheneverThisTurn { About }`: the
  trigger on YOUR command-zone object, its subject bound to that one object as it resolves (`TheObject`).
- **"When this land enters untapped, choose one —"** (TRK's five, `TrkCards.EntersUntapped`) is a `SelfEnters`
  trigger whose Subject is `Is.Source & !Is.Tapped` — read as it entered, so Warp Speed's untapped lands
  trigger too, and one entering tapped never does.
- **A dying permanent's own "whenever a creature … dies" sees its own death** (CR 603.10a, Excalbian Gamemaster's
  "this creature or another creature you control"): `Triggers.Collect` reads the dying card's Died triggers as
  it last existed, not only "when this dies"; another's death is matched on the battlefield, never there.
- **"No maximum hand size until your next turn"** (Deep Space Scan) is `NoMaximumHandSizeUntilYourNextTurn`: an
  `UntilEndOfTurn { NoMaximumHandSize }` object in your command zone, read by `TurnStructure.MaxHandSizeOf`.
- **"Whenever it deals combat damage to a player this turn, you draw that many"** (The Inner Light) is
  `WheneverThisTurn { About }` on a `CombatDamageBy` trigger — About narrows the damage's SOURCE there (the
  subject is the player) — and "that many" is the trigger's X, `PlayerDealtDamageEvent.Amount`.
- **"You don't lose the game for having 0 or less life"** (Georgiou, `DontLoseForZeroLife`) is asked by the
  state-based check alone (`TurnStructure`), only once life is 0 or less; she leaves, the next check takes the game.
- **"That ability triggers an additional time"** (CR 603.2d) is decided in `Triggers.Collect` alone
  (`AdditionalTimes`): each pending trigger from a PERMANENT repeated once per `TriggersAdditionalTime(cause,
  whoseAbilities)` whose cause the one `Triggers.Matches` accepts from the doubler's side (null: any) and whose
  filter matches the ability's source — Dr. Crusher (`YouGainLife`, `Is.Yours`, hers included), Isshin ("a
  creature attacking": `Attacked` and `AttackersDeclared` — "whenever you attack" too, Shayne 2026-11-06) and
  Lwaxana Troi's link (stamped on her `{ UntilUntapped }`, `TheObject` that object of the creature — read only
  while she is tapped, removed by `MagicState.Untap` and by leaving). The linked creature's own "when this dies" looks back
  and is doubled too; a DELAYED trigger hosted on a permanent isn't its ability and triggers once (CR 603.7c).
  Nothing is looked up unless a doubler is on the battlefield. Two "until end of turn" control effects on one
  permanent end together: it goes back to the controller before the FIRST.
- **"You may choose not to untap [it] during your untap step"** (Lwaxana Troi, `MayChooseNotToUntap`) is a
  yes/no the untap step asks before untapping — only while her link is live (Shayne, 2026-11-06); the rest of the
  step is `TurnStructure.FinishUntap`, after the answer (`FinishUntapAction`).
- **An extra turn** (H.M.S. Bounty, Time Walk, CR 500.7) is `TakeExtraTurn` (Time Warp's target player: `Who`): queued on `MagicGame.ExtraTurns`,
  the last queued taken first by `TurnStructure.FinishCleanup`, which also sets `MagicPlayer.BegunExtraTurn`
  ("haven't begun an extra turn this game", `HaventBegunAnExtraTurn`). `TurnNumber` counts it like any turn, so
  every "this turn" stamp holds. Turns no longer alternate: **summoning sickness reads `MagicPlayer.TurnBeganOn`**,
  never "the turn before".
- **"Your second spell each turn"** (Stella Lee) is `Trigger.SpellNumber = 2` against `SpellCastEvent.SpellNumber`,
  numbered as it is cast (any kind; `NoncreatureSpellNumber` beside it).
- **"Whenever you play a card from [zone]"** (Prosper, Tome-Bound) is `Trigger.From` on two rows: a `SpellCast`
  (`SpellCastEvent.From`, the zone it was cast from) and a `LandPlayed` (`LandPlayedEvent`, raised by `PlayLandAction`
  alone — a land put onto the battlefield isn't played). A prepared copy isn't a card: `Subject = !Is.Copy`.
