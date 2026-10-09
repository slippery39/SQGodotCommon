---
paths:
  - "MagicCore/*.cs"
  - "MagicCore/*/*.cs"
  - "MagicCore/Sets/Fra/*.cs"
  - "MagicCore.Tests/*.cs"
  - "MagicCore.Tests/*/*.cs"
---

# MagicCore — each mechanic's rules

The real-rules engine (`MagicCore/`, branch `mtg-real`). The rules that hold on every task are in
`MagicCore/CLAUDE.md`; these are the per-mechanic ones, each the ONE way that mechanic works here.

- **Planeswalkers:** enter with printed loyalty (`EnterBattlefield`), loyalty abilities are sorcery
  speed and once per turn per permanent, damage removes loyalty, 0 loyalty is a state-based death,
  and the defending player's planeswalkers are attack options. Empower Jace creates and loads the
  token in ONE resolution, so a fresh token never meets the check at 0.
- **Modes (CR 700.2):** a modal text is its `Modes` + `Choose`. `Modal.Compose` is the ONE place the
  chosen modes become target words and steps, in printed order; each mode's effects see only its own
  targets (`Target(0)` is that mode's first). A spell's modes are chosen when cast (`CastSpellAction.
  Modes`), a trigger's as it goes on the stack. A mode with an unchoosable required target can't be
  chosen. The scripted mode count must match the printed bullets — `FraCards` throws otherwise.
- **"At the beginning of [step]"** triggers on a `StepBeganEvent`, raised by `EnterStep` for upkeep,
  draw, beginning of combat and end. **"This turn" lives in `MagicPlayer.ThisTurn`** (a `TurnTally`:
  lands, spells, cards drawn, life gained, surveils), REPLACED whole for BOTH players as each turn
  begins — "this turn" is every turn, not only yours, and a new count cannot miss the reset. Update
  it with `s.Tally(…)`. The finished turn's tally becomes `LastTurn` ("last turn" = the turn before
  this one, whoever's). Creatures that died this turn (anyone's) are on `MagicGame`.
- **"This ability triggers only once each turn"** is `TriggeredAbilityComponent.OncePerTurn`, stamped
  on the card as it goes on the stack (`PutOnStack`) — a new object (CR 400.7) may trigger again.
- **"Entered this turn"** is `Card.EnteredOnTurn`, stamped by `EnterBattlefield` — not
  `ControlledSinceTurn`, which a control change would also move. **"Attacked this turn"** is
  `Card.AttackedOnTurn`, stamped as attackers are declared.
- **"Doesn't untap during your untap step"** (`DoesntUntapInYourUntapStep`) is read by the untap step
  ONLY — an untap effect still untaps it (Traxos' own trigger). A stun counter, by contrast, eats every untap.
- **A subtype narrows a trigger or a static:** `TriggeredAbilityComponent.EventSubtype` ("whenever a
  Plains you control enters" — no prompt for any other land) and `StaticEffectComponent.OnlySubtype`
  ("Thopters you control have haste"). Never `TriggeredAbilityComponent.If` for this: that is checked
  again on resolution, so a Plains bounced in response would lose Koth's counter — there's no "if" in the text.
- **Discarding is `MagicState.Discard`** — effects, costs (cast or ability) and the cleanup alike: one
  `DiscardedEvent` per batch ("one or more cards"), counted in `TurnTally.CardsDiscarded`. A chosen
  card going to the graveyard is a discard only from the hand (`MoveChosenToGraveyardAction`; surveil
  isn't). "When you discard this card" triggers `FromGraveyard`, where the card now is. Something that
  triggers in the cleanup (a discard to hand size) gets priority THERE, then another cleanup step
  follows (`TurnStructure.FinishCleanup`, CR 514.3a) — never carried into the next turn.
- **"Attacks a player alone"** is read as attackers are declared: it is the only creature its
  controller has attacking, and it attacks a player, not a planeswalker.
- **"Whenever you cast a spell that targets …"** reads the spell's targets from its `OnStackComponent`;
  "your first noncreature spell each turn" is numbered AS IT IS CAST (`SpellCastEvent`) — a count read
  later would let a spell cast in response steal the trigger.
- **"You can't cast this unless …"** is `CastOnlyIf`, read by `Playable.CanCastFromHere` — never offered.
  **"Behold a … or pay {N}"** is `CastSpellAction.BeholdId` (a card you control, or in hand, not this
  one); nothing moves.
- **Numeric replacements** ("… that much plus one instead") are `AmountReplacement` on a permanent,
  applied by `Rules.Replacements.Apply` inside the one place the event happens: `Damage.Deal` (and
  `Damage.Excess`, so excess sees the replaced amount) and `MagicState.AddPlusOneCounters` — the ONE
  way +1/+1 counters are put on, entering with them included. Lifted from MtgCore's ReplacementEngine.
- **-1/-1 counters** are `Card.MinusOneCounters`, read into `OwnPower`/`OwnToughness`; the
  state-based check cancels them against +1/+1 counters in pairs (CR 704.5q).
- **How much combat damage a creature assigns** is `Combat.Assigns` — power, or toughness (Ghalta), or a
  negative power as positive (Loot). Combat never reads `s.Power` for damage directly.
- **What tapping a permanent for mana makes** is `ManaAbilities.YieldOf`: units of mana, each with its
  allowed colours — several of ONE colour (Loot, the Nexus: `SameColor`), a Mountain's extra {R} under
  Molten Tide (`TurnTally.ExtraRedFromMountains`), mana that can't pay for spells from your hand
  (Heartwood Crafter). `ManaPayment.Find` solves units: a permanent taps once, and what the payment
  doesn't use floats (`Payment.Floats`). Restricted mana is never floated — manual tapping refuses it.
- **A card put onto the battlefield from anywhere enters through `MagicState.EnterBattlefield`** —
  a search, a return, a token: never a bare `MoveCard` to the battlefield, which skips its entry
  (controller, tapped, its "enters" event). `MoveChosenToAction` routes a battlefield search there.
- **Copying a spell** is `CopySpell` (CR 707.10): a `Card.IsCopy` on top of the stack, controlled by
  the copier, not cast (no "whenever you cast"), its targets chosen anew with the original's among the
  options — and gone once it leaves the stack (the state-based check prepared copies use). "When you
  next cast an instant or sorcery this turn" is a delayed trigger kept on the player
  (`TurnTally.CopyNextInstantOrSorcery`), taken by that cast and carried on its `SpellCastEvent`.
  Replace a component with `WithComponentReplaced` — `WithComponent` ADDS, and `GetComponent` reads the first.
- **Convoke** is `ManaPayment.Find(…, convoke: true)`: your untapped creatures join the sources after
  lands, each paying its own colour or {1} (as Colorless); summoning sickness doesn't matter.
- **"During combat, no spells or abilities"** (Yuriko) and **split second** (Samut) are both
  `Combat.LockedDown`, asked by casting and activating alike — never by mana abilities or triggers.
- **"This spell can't be countered" for one cast** (Theorist's Proxy) is `OnStackComponent.CantBeCountered`
  — gone with the stack object, so a recast can be countered; `StackResolution.CanBeCountered` reads both.
- **An attack limit** ("no more than one creature can attack this planeswalker") is an option
  `DeclareAttackerChoice` doesn't offer, read from the declarations already made in that pipeline.
- **"Whenever you attack"** and **"one or more … are dealt combat damage"** are ONE event each per
  declaration or damage step (`AttackersDeclaredEvent`, `CombatDamageToPlayersEvent`) — never one per creature.
- **"You win the game"** (`WinTheGame`) ends it at once: a same-moment draw from an empty library never
  reaches the state-based check.
- **"Loses all abilities"** is `s.HasLostAllAbilities` — until end of turn (`UntilEndOfTurn.LosesAllAbilities`)
  or under an Aura (`EnchantedLosesAllAbilities`, found once per state with the statics). It reaches
  every rule because every rule reads abilities through `s.AbilitiesOf<T>`, keywords through `s.Has`,
  mana through `ManaAbilities.ColorsOf` (a basic land's intrinsic ability included). What the AURA
  says ("doesn't untap") is the Aura's, so it still applies. No timestamps: everything goes.
- **Cost changes all live in `Costs.Of`** — taxes on the opponent's side, reductions on yours
  (`YourNoncreatureSpellsCostLess`), and the card's own.
- **Where triggers are read from:** permanents on the battlefield; a dying card's own "when this
  dies"; a graveyard card's abilities marked `FromGraveyard` (Darklight Phoenix) — those, and only
  those, trigger from there; and a trigger granted until end of turn (`UntilEndOfTurn.Trigger`,
  Lyra), which ends at cleanup or a zone change like any other until-end-of-turn effect.
- **"Becomes the target"** is raised when a spell or ability goes on the stack with its targets —
  `CastSpellAction` and `StackResolution.Push` (every ability), the one place each.
- **Casting from the graveyard (flashback):** `Costs.Flashback` is the ONE answer to "may this card
  be cast from its graveyard, and for what" (its own flashback, or its mana cost when it gained
  flashback this turn); `Costs.Of` starts from it, so taxes still apply. The stack records where a
  spell came from (`OnStackComponent.CastFrom`), and `MoveCard` exiles a flashback spell whenever it
  would leave the stack — resolved, fizzled or bounced (CR 702.34a).
- **Prepare** (Secrets of Strixhaven's mechanic; FRA reuses it) lives in `Rules/Prepare.cs`. The card is
  a creature in every zone; its spell half (`PrepareSpellComponent`, the second face) is never cast
  from hand. Becoming prepared puts a real COPY of the spell in exile (`Card.IsCopy`, linked to the
  creature's id AND incarnation); its controller may cast it at the spell's own timing while that
  same creature is still prepared, and casting it unprepares the creature. A copy anywhere but the
  stack ceases to exist, unless it is a live prepared copy in exile (state-based, CR 704.5e).
- **Abilities from the hand or graveyard** (landcycling, "exile this from your graveyard:") are
  `ActivatedAbilityComponent.From`; card costs (discard this / a card, exile this / another card)
  are chosen like a sacrifice, one legal action each. A finality counter exiles a creature instead
  of letting it die (`MoveCard`); `MagicState.Untap` is the ONE untap, so a stun counter eats it.
- **Hexproof** is checked in `TargetSpec.IsLegal` through `s.Has` (an opponent's spells and abilities
  only; mass effects never target). **Indestructible** is `s.IsIndestructible` — the Destroy effect and
  the lethal-damage check both ask it; 0 toughness still kills.
- **Auras and Equipment are one mechanism** (`Card.AttachedToId`, Rules/Attachments.cs). An Aura targets
  as it is cast (`AuraComponent`) and attaches on resolving; what either grants is a live static
  (`Affects.AttachedCreature`), never stamped. "When enchanted creature dies" looks back through
  `DiedEvent.Attached` — the state check takes the Aura in the same pass.
- **Countering is `StackResolution.Counter`** — a spell to its graveyard through MoveCard (flashback and
  prepared copies still hold), an ability removed, "can't be countered" untouched. "Unless its
  controller pays" asks that player (`CounterUnlessPays`). **Ward** is a trigger whose "it" is the
  targeting spell or ability, carried as the event card of `BecameTargetEvent`.
- **"When you do"** (a reflexive trigger, CR 603.12) is the `WhenYouDo` effect: its targets are chosen
  as it triggers, after the thing done, and it goes on the stack above — never fold it into the effect.
  It triggers only if the thing was done: after a mill it goes in `Mill.IfMilled` (an empty library mills nothing).
- **Targets in a graveyard** are `TargetKind.CardInYourGraveyard` (or `CardInAnyGraveyard`, "from a graveyard") — the same legality test on choosing
  and resolving. "One or two target creatures" is two words, the second `Optional` and `NotThePrevious`.
- **"You may … If you do"** is `May` (a `YesNoChoice`, asked only when it can be done) or
  `MaySacrifice`; both clear the shared "chosen" key so a "no" cannot read a stale "yes". "Its owner
  may" is `May.Who` — another player answers, the effects stay yours — and "if they didn't" is `Otherwise`.
- **"Exile it instead" of dying** is decided in `MoveCard` alone: a finality counter, or Essence Burn's
  `UntilEndOfTurn.ExileIfItWouldDie` — so the card never dies and no "dies" trigger sees it.
- **Excess damage** (CR 120.4a) is `Damage.Excess`, measured BEFORE the damage is dealt (past
  loyalty, or toughness less marked damage; 1 from deathtouch). `DealDamage.IfExcess` runs with X = it.
- **An intervening "if" (CR 603.4) is `TriggeredAbilityComponent.If`**, never a bare `If` effect:
  checked as it triggers (`Collect`) AND again on resolution (`QueueTargets` wraps the effects).
  Edgar with a spell cast in response to his trigger gets nothing. A condition on the EVENT itself
  can't change before resolution, so it may be a `TriggerEvent` instead (Way of the Mind Sculptor's
  "removed two or more"). `If` runs its steps as a child pipeline — so a surveil inside it finishes
  before the next step — and its steps' outputs do not reach the steps after it.
- **Prowess is a scripted trigger** (`FraCards.Prowess`), not an engine keyword — each instance
  triggers on its own (Ruric Thar has two).
- **Base P/T effects** ("has base power and toughness 4/5") sit under counters and pumps, latest
  wins (`UntilEndOfTurn.SetBase`, read in `Card.OwnPower`/`OwnToughness` under `s.Power`/`Toughness`).
