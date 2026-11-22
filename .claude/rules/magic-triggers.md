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
  `Card.AttackedOnTurn`, stamped as attackers are declared. **"During any turn you attacked with a Rogue"** (Robber of
  the Rich) is the same condition `{ Attacked }`, read from `TurnTally.SubtypesAttacked` (written as attackers are
  declared) — true after the Rogue has left.
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
  dies"; a graveyard or hand card's abilities whose `From` names that zone (`TriggeredAbilityComponent.From`: Darklight
  Phoenix's `Graveyard`, miracle's `Hand` — MC-4.4h9 generalised `FromGraveyard`) — those, and only those, trigger from there; and a trigger granted until end of turn (`UntilEndOfTurn.Trigger`,
  Lyra), which ends at cleanup or a zone change like any other until-end-of-turn effect.
- **"Becomes the target"** is raised when a spell or ability goes on the stack with its targets —
  `CastSpellAction` and `StackResolution.Push` (every ability), the one place each. Its subject is a permanent or a
  spell on the stack (`BecameTargetEvent.Subjects`, MC-4.4k2b): Surrak, Elusive Hunter's "a creature you control or a
  creature spell you control" is `Subject = Is.Creature & Is.Yours` — a creature card targeted in a graveyard is neither;
  MACO Strike Team's "a creature you control" alone is `… & !Is.Spell` (`Is.Spell`: a card on the stack).
- **"When you do"** (a reflexive trigger, CR 603.12) is the `WhenYouDo` effect: its targets are chosen
  as it triggers, after the thing done, and it goes on the stack above — never fold it into the effect.
  It triggers only if the thing was done: after a mill it goes in `Mill.IfMilled` (an empty library mills nothing).
  "When you do, if you control a red permanent other than Ajani" (Ajani, Nacatl Avenger, MC-4.4j2b) is `WhenYouDo { If
  }`: an intervening if — nothing triggers unless it holds, and it is checked again on resolution (the ability's `If`).
- **An intervening "if" (CR 603.4) is `TriggeredAbilityComponent.If`**, never a bare `If` effect:
  checked as it triggers (`Collect`) AND again on resolution (`QueueTargets` wraps the effects).
  Edgar with a spell cast in response to his trigger gets nothing. A condition on the EVENT itself
  can't change before resolution, so it may be an `EventTest` on the `Trigger` instead (Way of the Mind Sculptor's
  "removed two or more"). `If` runs its steps as a child pipeline — so a surveil inside it finishes
  before the next step — and its steps' outputs do not reach the steps after it.
- **A trigger's X is ONE number, `Triggers.XOf`** — for "when this enters" (a Subject of exactly `Is.Source`: a
  narrowed one, `Is.Source & !Is.Tapped`, reads 0 until a card needs it) the X it was cast for, else the event's —
  read by its intervening "if" as it triggers (`ContextOf`) AND by its effects (`QueueTargets`): ravenous' "if X is 5
  or more, draw a card when it enters" (Jacked Rabbit) is `When(SelfEnters, draw) with { If = AtLeast(Count.X, 5) }`.
- **A delayed trigger's "this" is the card it is on; the object that set it up is its `Ref.EventObject`**
  (`DelayedTrigger.MadeBy`, CR 603.7f — nothing once that object has left; set by `Flicker.Then` alone so far: give
  `AtNextEndStep` the same line when a card of its reads its maker). Phelia's "return that card … If it entered
  under your control, put a +1/+1 counter on Phelia" is `Flicker { AtNextEndStep, Then }`: the delayed return's
  `MoveTo.Then`, with `Ref.ThisWay` the card as it entered, "you" Phelia's controller and `Ref.EventObject` Phelia.
  The card returns whether or not she is still there.
- **Prowess is a scripted trigger** (`FraCards.Prowess`), not an engine keyword — each instance
  triggers on its own (Ruric Thar has two).
- **"Entering doesn't cause abilities to trigger"** (Karn, Argent Defender) drops those entering
  events in `Triggers.Collect` before ANY ability is matched — read at the next priority, so Karn
  entering alongside a creature silences it too, as the rulings say.
- **"Whenever [this] deals damage"** — to anything (Cecil, Dark Knight, MC-4.4i7) — is `TriggerEvent.SelfDealsDamage`, on
  `DealtDamageEvent(SourceId, Amount)` raised by `Damage.Deal` for every recipient (a planeswalker too); "that much" is
  the trigger's X (one event per recipient: DesignNotes).
- **"Whenever this creature is dealt damage"** triggers on `CreatureDealtDamageEvent` from `Damage.Deal`;
  "that many" is the trigger's X (`Triggers.Pending.X`). **Cards put into a graveyard from a library**
  (mill, surveil, anything) are counted in `MoveCard` (`TurnTally.CardsFromLibraryToGraveyard`).
- **A delayed trigger on a permanent** ("exile it at the beginning of the next end step") is a
  `DelayedTrigger` component: kept when it loses all abilities, not copied, gone with a zone change.
  On a card it EXILED (`Flicker.AtNextEndStep`, Ghost of Illyria: "return that card at the beginning of the
  next end step") `Triggers.Collect` reads that one component there, never the exiled card's own abilities —
  and on a card that DIED (`AtNextEndStep.On`, Dr. McCoy: "return that card … at the beginning of the next
  end step"), read in the graveyard the same way; leaving the graveyard strips it (a new object) — or on a
  permanent on the battlefield (Sneak Attack: "Sacrifice the creature at the beginning of the next end step",
  `On = Ref.Chosen` in `MoveChosen.Then`; its "gains haste" with no duration is `ModifyUntilEndOfTurn.ForGood`).
  With no card to hold it ("return a land you control", Erica Ortegas) it is `AtNextEndStep`: an object in
  your command zone, removed once it has resolved. Set up during the end step, either waits for the next turn's.
  **The step it waits for is ONE option, `AtNextEndStep.At`** (MC-4.4l3b; never a flag per step): the end step unless
  said; "at the beginning of your next main phase" (Mana Drain) is `At = Step.PreCombatMain` — your precombat main,
  or this turn's postcombat main when set up on your turn before it (so the postcombat main raises a `StepBeganEvent`
  too); "at the beginning of the next turn's upkeep" (Mishra's Bauble, Urza's Bauble) is `At = Step.Upkeep`, whoever's
  turn that is. **A number fixed as it is set up** ("equal to that spell's mana value") is `AtNextEndStep.X`, the delayed
  trigger's `TriggeredAbilityComponent.X` — its effects read it as `Count.X`. **The cards it is about** (Memory Jar: "each
  player discards their hand and returns to their hand each card they exiled this way") are `AtNextEndStep.Those`: the
  cards a Ref names AS IT IS SET UP (`Ref.ThisWay`, in the exile's `Then`), its effects' own `Ref.ThisWay` when it
  resolves — its "this" is a command-zone object, which no `ExiledBy` link names.
  **One on a permanent whose controller is someone else** (The Wrath of Khan's "at the beginning of the next
  combat this turn, gain control of that creature", `AtBeginningOfNextCombat`) names its controller
  (`DelayedTrigger.ControllerId`, CR 603.7d): `Triggers.Collect` reads those as it reads an exiled card's —
  never as the permanent's own, which would make its controller "you".
- **A discard trigger's "that card"** (Currency Converter: "Whenever you discard a card, you may exile that card from your
  graveyard") is its event card — `DiscardedEvent` in `Triggers.EventCard` — so `Exile(Ref.EventCard) { Linked }`, which
  takes it only while it is still in a graveyard. **"Your first, second, or third turn of the game"** (Starting Town) is
  `Count.TurnsYouHaveBegun` — `MagicPlayer.TurnsBegun`, stamped beside `TurnBeganOn` as each turn of that player begins,
  extra turns too — under `DuringYourTurn`.
- **Emblems** (CR 114) are cards in their owner's COMMAND zone (`GetEmblem`): `Triggers.Collect` and
  the statics board read that zone beside the battlefield, so an emblem's trigger has a source
  ("this emblem deals 5 damage") and its anthem applies. Nothing removes them.
- **"Until your next turn"** is `UntilEndOfTurn.UntilTurnOf` (the player): cleanup leaves it, and
  that player's untap step ends it (`TurnStructure.EndUntilYourNextTurn`). One on a permanent rides the
  permanent (Garruk's −4/−1); "until your next turn, whenever …" is an effect card in the command
  zone (`UntilYourNextTurnWhenever`), so it outlives the planeswalker, and is removed when it ends.
- **"When you cast this spell"** (`TriggerEvent.SelfCast`, Emrakul) is read from the spell on the stack, and keeps it as
  it was cast (`EventSnapshot`): storm's copies of a spell countered in response still read it (MC-4.4g7).
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
- **Classes** (CR 716; Does Machines, MC-4.4g10) have a LEVEL, never a counter: `ClassLevel(N)`, status on the card
  (gone as it leaves, never copied; none is level 1), read by `Count.ClassLevelOf`. "[Cost]: Level N" is
  `Scripting.Level(cost, n)` — sorcery speed, only at level N−1, resolving to `GainLevel(n)`, which raises
  `BecameLevelEvent`; "when this Class becomes level N" is `Scripting.WhenLevel(n, …)`, `Trigger.Chapter` on that
  event. An ability in a level's box is had from that level on: a trigger's `While = AtLevel(n)` (judged as the event
  happens, never again on resolution), a static's `If`, an activated ability's `OnlyIf`.
- **Dungeons and the initiative** (CR 309, 701.49, 725; MC-4.4g3, `Effects/Dungeons.cs`). A dungeon is an object in its
  venturer's command zone (`GetEmblem.ToCommandZone`) with `DungeonComponent(Room)`, an index into `Undercity.Rooms`
  (the only dungeon: CR 725.2). A room is NOT a Saga chapter — Undercity branches, so no count says which room you
  are in. `Venture` makes it, or moves the marker below — on the last room it completes that one and begins a new
  one; two exits are ONE `ChooseKeywordChoice` (never a move per
  path), and the room's ability is put on the stack as `WhenYouDo`'s is (`Triggers.QueueTargets`, its source the
  dungeon). On its last room it is removed once that ability has left the stack — the Saga's check
  (`ChapterToResolve`), in the state-based actions. **The initiative** is an object in its holder's command zone
  (`DesignationComponent(Initiative)`) carrying its two triggers: "at the beginning of your upkeep, venture", and "whenever
  creatures deal combat damage to you, the active player takes it". `TakeTheInitiative(who)` moves it (one holder,
  CR 725.3), then ventures — again when you already hold it. "If you have the initiative" is `YouHaveTheInitiative`.
  **A dungeon removed on its last room is completed** (CR 309.7) — `Undercity.Complete`, from both removals (the state
  check and a venture), counts it on its venturer for the game (`MagicPlayer.DungeonsCompleted`); "if you've completed
  a dungeon" (Caves of Chaos Adventurer, MC-4.4i2a) is `YouCompletedADungeon`.
  **The monarch** (CR 724, Palace Jailer, MC-4.4j3) is the same object: both are `DesignationComponent(name)`
  (`Initiative`, `Monarch`), one holder each (`HolderOf`, `Take`); the monarch's triggers are "at the beginning of your
  end step, draw a card" and combat damage to you making the active player the monarch — `BecomeTheMonarch(who)`.
  "Exile … until an opponent becomes the monarch" is `ExileUntilSourceLeaves { UntilAnOpponentTakes = Monarch }`,
  stamped `ExiledUntilLeaves(0, 0) { UntilAnOpponentTakes, Exiler }` — no source, so its leaving never returns it —
  and returned by `Take` as the designation MOVES to an opponent of the exiler, to the zone it left.
- **"Whenever [a creature with …] deals combat damage to a player"** — not "this" — is
  `Trigger.CombatDamageBy`, a filter on the damage's SOURCE (Phlox); the event's subject is the player.
- **"Sacrifice it at the beginning of the next end step"** on a new token is `CreateTokens`/`CreateTokenCopy`
  `.SacrificeAtNextEndStep` — a `DelayedTrigger` added after it enters, like `ExileAtNextEndStep`.
- **"Whenever …, this turn"** from a resolving ability (The Naked Time's chapter II) is `WheneverThisTurn`:
  the trigger on a command-zone object, removed with the turn's other "this turn" effects.
- **A card changing zones** is ONE event, `ChangedZoneEvent(owner, card, From, To)` (MC-4.4c2c), raised by
  `MagicState.MoveCard` for the moves a card reads — out of a graveyard to anywhere, library → exile, and off the
  battlefield to anywhere for a permanent that reads its OWN leaving (MC-4.4d3: `MagicState.ReadsItsLeaving`; a
  token too, `LastKnown` the permanent as it last was — never for every permanent: `MoveCard` is the hottest path,
  and a staged event costs each search node a `Collect` and a state-check round) — and every LAND put into a graveyard
  from the battlefield (MC-4.4k1, Titania, Protector of Argoth: `{ From = Battlefield, To = Graveyard, Subject = Is.Land &
  Is.Yours }`, matched on Titania's battlefield; rare enough to raise for each). **"Whenever you sacrifice a land"**
  (Baloth Prime, MC-4.4k2a) is that trigger with `Also = EventTest.Sacrificed`: `MagicState.Sacrifice` — every sacrifice,
  costs and effects — tells `MoveCard`, which marks the event `Sacrificed`. **"When [this] is put into a graveyard from
  anywhere"** (Worldspine Wurm, MC-4.4k2b) is `{ Subject = Is.Source, To = Graveyard }` on a trigger read `From =
  Graveyard`: `MoveCard` raises the event for a card that reads its own arrival there
  (`MagicState.ReadsItsArrivalInAGraveyard`), from whatever zone it left; "shuffle it into its owner's library" is
  `MoveTo(Ref.Source, Library) { From = [Graveyard], Shuffle }`, beside its "dies" trigger. Widen it
  THERE for another move, never a second event — matched by `Trigger.From` / `Trigger.To`. **"Whenever a card leaves
  your graveyard"** (Vash) is `{ By = You, From = Graveyard }`: every
  path out — cast, exiled, returned, shuffled, paid as a cost; a token isn't a card. **"Whenever one or more cards are
  put into exile from your library and/or your graveyard"** (Laelia) is `{ By = You, To = Exile, NotFrom = [Battlefield],
  OneOrMore }` — no From: the event's moves but a permanent's off the battlefield. **"One or more"** is
  `Trigger.OneOrMore`: one trigger per priority pass, everything that left since a player last had priority
  (Shayne, 2026-11-01).
- **"When this leaves the battlefield"** (Skyclave Apparition) is `TriggerEvent.SelfLeaves` (MC-4.4d3): the zone-change
  event read from the card that LEFT, as it last existed (`ReadFrom.TheCardThatLeft`, CR 603.10a — the one look-back
  "when this dies" uses too, `Triggers.Collect`'s `LookingBack`, so a doubler repeats both) — to any zone. A death
  raises both events and each trigger reads its own: `SelfDies` only `DiedEvent`, `SelfLeaves` only the zone change.
  **"When this permanent is put into a graveyard from the battlefield"** (Serra Paragon's gift) is `SelfLeaves with
  { To = Graveyard }` — any permanent, a land too, never `SelfDies` (a creature or planeswalker); its "exile it" is
  `MoveTo(Ref.EventCard, Exile) { From = [Graveyard] }`, the card only while it is still in that graveyard.
- **"[Permanents] you control have '[triggered ability]'"** is `GrantsTriggeredAbility(scope, ability)`:
  `Triggers.Collect` gives it to each permanent the scope matches, and "this creature" is that one.
- **"Whenever you draw a card"** is `DrewCardEvent`, raised by `MagicState.Draw` once per card (Data, New Life). Its
  subject is the card drawn while it stays in its owner's hand (miracle's "when you draw it": `Subject = Is.Source`) —
  never its event card: an opponent's draw is a hidden card. A trigger from the hand reveals its card as it goes on the
  stack (`PutOnStack`: miracle's reveal), so the AI's hidden-hand sampling never swaps it. The opening hand and a mulligan's are drawn before the first turn (`TurnNumber` 0) and raise none — a
  miracle can't be cast from an opening hand.
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
- **An additional combat phase** (CR 500.8, Fear of Missing Out, MC-4.4i3) is `AdditionalCombatPhase`: a count on
  `MagicGame.AdditionalCombats`, taken by `TurnStructure.EndStep` as an end of combat step ends — creatures removed
  from combat (`Combat.EndCombat`), then a beginning of combat step, no main phase between; reset as each turn begins.
  Set up during combat or the first main phase it is right (two combats in a row either way); anywhere else the effect
  THROWS (DesignNotes). Every combat step's triggers fire again in it.
  **"Whenever this creature attacks for the first time each turn"** is `Trigger.Nth = 1` on an `Attacked` trigger,
  against `AttackedEvent.Number` — that object's attacks this turn (`Card.TimesAttacked`, with `AttackedOnTurn`) —
  never `OncePerTurn`, which stamps a TRIGGER: an attack before delirium never triggered.
- **"Your second spell each turn"** (Stella Lee) is `Trigger.Nth = 2` against `SpellCastEvent.SpellNumber`,
  numbered as it is cast (any kind; `NoncreatureSpellNumber` beside it). **"Whenever an opponent draws their second
  card each turn"** (Faerie Mastermind, MC-4.4e2b) is the same option on a `Drew` trigger, against
  `DrewCardEvent.Number` — the drawer's count this turn, stamped by `MagicState.Draw` as each card is drawn, so two
  drawn in one resolution are the first and the second. `Nth` fits those two kinds and `Attacked` (above) only; never a
  second number.
  **"Whenever an opponent draws a card except the first one they draw in each of their draw steps"** (Orcish
  Bowmasters, MC-4.4h2d) is `Trigger(Drew) { Also = EventTest.NotTheTurnsDraw }`, against `DrewCardEvent.TurnDraw` — the
  draw step's own first card, the one Hullbreacher's `ExceptTheTurnsDraw` excepts (`Draw(…, turnDraw: true)`, i == 0).
- **Magecraft — "whenever you cast or copy an instant or sorcery spell"** (Sedgemoor Witch, MC-4.4h2d) is
  `LsvCards.Magecraft(effects)`: a `SpellCast` trigger and a `SpellCopied` one, sharing the effects. `SpellCopiedEvent`
  is raised by `CopySpell` alone — the one place a spell copy goes on the stack (storm, replicate, Chain of Smog) — By
  the copy's controller. A copy is never a `SpellCastEvent`: every "whenever you cast" and storm's `SelfCast` would see it.
- **"When this enters from anywhere other than a graveyard or exile"** ("Name Sticker" Goblin) is `Trigger.NotFrom`
  against `EnteredBattlefieldEvent.From` — the zone `EnterBattlefield` saw it leave (a resolving spell: the stack;
  null for a token just made, which no NotFrom refuses); `Trigger.From` reads it too. Its "if it's on the
  battlefield" is `Matches(Ref.Permanents(Is.Source), …)` — a permanent NOW: `Ref.Source` in a `Matches` reads the
  source as it last was once it is gone, so it would hold for a dead Goblin.
- **"Whenever you play a card from [zone]"** (Prosper, Tome-Bound) is `Trigger.From` on two rows: a `SpellCast`
  (`SpellCastEvent.From`, the zone it was cast from) and a `LandPlayed` (`LandPlayedEvent`, raised by `PlayLandAction`
  alone — a land put onto the battlefield isn't played). A prepared copy isn't a card: `Subject = !Is.Copy`.
- **Day and night** (CR 730; daybound / nightbound, CR 702.145 — Outland Liberator // Frenzied Trapbreaker, MC-4.4k3)
  is the GAME's designation, `MagicGame.Daytime` (`Rules.DayNight`), never a player's object as the monarch is: neither
  until a daybound permanent enters (`EnterBattlefield`: day), then judged as each turn ends for the next
  (`TurnStructure.FinishCleanup` → `DayNight.NextTurn`, before anything untaps) by the spells that turn's ACTIVE player
  cast — none by day: night; two or more by night: day. `DayNight.Become` transforms every daybound permanent that
  doesn't match (front by day, back by night; `TransformedEvent` raised), and one entering at night enters transformed.
  Daybound is the printed keyword on a card with a back face (`TransformsInto`); the front's keywords are both faces'
  (Scryfall), so daybound is asked of a front and nightbound of a back. Read where something CHANGES, never by the
  state-based check (DesignNotes).
