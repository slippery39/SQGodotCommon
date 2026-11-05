---
paths:
  - "MagicCore/*.cs"
  - "MagicCore/*/*.cs"
  - "MagicCore/Sets/Fra/*.cs"
  - "MagicCore/Sets/Trk/*.cs"
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
  Modes`), a trigger's as it goes on the stack, an activated ability's as it is activated
  (`ActivateAbilityAction.Modes`, one legal action per mode — Tea, Earl Grey, Hot; never two abilities
  with one cost: TRK's dilemma counts mode choices). A mode with an unchoosable required target can't be
  chosen. The scripted mode count must match the printed bullets — `Scripting.Printed` throws otherwise.
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
- **"Doesn't untap during your untap step"** (`DoesntUntapInYourUntapStep`) is read by the untap step
  ONLY — an untap effect still untaps it (Traxos' own trigger). A stun counter, by contrast, eats every untap.
- **A subtype narrows a trigger or a static:** the trigger's `Subject` ("whenever a Plains you
  control enters" is `Is.Land & Is.Yours & Is.Subtype("Plains")` — no prompt for any other land) and a static's scope
  (`Is.Subtype("Thopter")`: "Thopters you control have haste"). Never `TriggeredAbilityComponent.If`
  for this: that is checked again on resolution, so a Plains bounced in response would lose Koth's counter — there's no "if" in the text.
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
  `Damage.Excess`, so excess sees the replaced amount) and `MagicState.PutCounters` — the ONE way
  counters of any kind are put on, entering with them included. Lifted from MtgCore's ReplacementEngine.
- **-1/-1 counters** are `Card.MinusOneCounters`, read into `OwnPower`/`OwnToughness`; the
  state-based check cancels them against +1/+1 counters in pairs (CR 704.5q).
- **How much combat damage a creature assigns** is `Combat.Assigns` — power, or toughness (Ghalta), or a
  negative power as positive (Loot). Combat never reads `s.Power` for damage directly.
- **What tapping a permanent for mana makes** is `ManaAbilities.YieldOf`: units of mana, each with its
  allowed colours — several of ONE colour (Loot, the Nexus: `SameColor`), a Mountain's extra {R} under
  Molten Tide (`ExtraRedFromMountains`, a `ThisTurnEffect`), mana that can't pay for spells from your hand
  (Heartwood Crafter). `ManaPayment.Find` solves units: a permanent taps once, and what the payment
  doesn't use floats (`Payment.Floats`). **A mana ability that costs mana** (Starfleet Academy: "{5}, {T}:
  Choose a color. Add N", `ManaOfOneColorComponent.Cost`) is never a plain tap: `Find` tries it only when
  nothing else pays (`WithCostlyMana`), its N units of one colour in place of its {C}, its own cost owed
  beside the spell's and never paid by the mana it makes (`Need.NotFrom`).
  Manual tapping floats restricted mana AS restricted (`ManaPool.Restricted`).
  A restriction is a `ManaRestriction` the solver checks against the spell it pays for
  (`Find(…, forSpell)`), or the source of the activated ability it pays for (`forAbilityOf` —
  Isolinear Processor's "an ability of an artifact source"). Treasure and Lotus are `ManaOfOneColorComponent`
  with `SacrificeSelf`: `Pay` sacrifices them, and the solver spends them last.
- **Every token is made by `CreateTokens`** — so a replacement on tokens (Draconic Visitor's "artifact
  tokens are Dragons instead"; Holo-Engineer's ANY token an Illusion, `ArtifactTokensBecome.AnyToken`) is
  applied in that one place (`CreateTokens.Instead`, copies too), under whoever `ControlledBy` names.
- **A card put onto the battlefield from anywhere enters through `MagicState.EnterBattlefield`** —
  a search, a return, a token: never a bare `MoveCard` to the battlefield, which skips its entry
  (controller, tapped, its "enters" event). `MoveChosenToAction` routes a battlefield search there.
- **Copying a spell** is `CopySpell` (CR 707.10): a `Card.IsCopy` on top of the stack, controlled by
  the copier, not cast (no "whenever you cast"), its targets chosen anew with the original's among the
  options — and gone once it leaves the stack (the state-based check prepared copies use). "When you
  next cast an instant or sorcery this turn" is a delayed trigger kept in the command zone
  (`CopyNextInstantOrSorcery`, a `ThisTurnEffect`), taken by that cast and carried on its `SpellCastEvent`.
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
- **Cost changes all live in `Costs.Of`**: each is a `CostChange` — on a permanent, for the spells
  `By` names (Thalia's tax on opponents, Geist's reduction on yours), or `OnItself` ("this spell costs
  … less"); an activated ability's (Warrior's Blades) is `ActivatedAbilityComponent.CostChange`, read
  by `Costs.OfAbility` with the targets known — and a spell's own (Guidance Failure: "if it targets an
  attacking creature") sees its targets too, `Costs.Of(…, targets)` from the cast.
- **Costs beyond mana are one list of `AdditionalCost`** (Effects/AdditionalCosts.cs) — activated
  abilities, a spell's `AdditionalCostsComponent`, flashback, ward. A cost that picks a card names it
  in the action's `SacrificeId`/`DiscardId`/`ExileId`/`BeholdId`/`TapId`/`SecondTapId`/`ReturnId` (`CostSlot`), a set in `TapIds` — and the table's
  `Targeting.PicksOf`/`WithoutPicks`, so it is clicked — one legal action per
  choice; `CostList` is the one place candidates, legality and "or pay" read them. Taps are paid
  first; ward pays by choosing (`PaidByChoosing`).
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
  only; mass effects never target). **Destroying is `MagicState.Destroy`** — the Destroy effect and the
  state-based check's lethal damage and deathtouch alike: indestructible (`s.IsIndestructible`) is asked
  there, and Dr. McCoy's replacement ("instead remove all +1/+1 counters … damage healed",
  `CountersRemovedInsteadOfDestruction`) applies there. Several at once are JUDGED together before any
  leaves (CR 701.7) — a wrath doesn't end an indestructible grant halfway, nor kill McCoy before the creature
  he saves — then leave in the battlefield's order. 0 toughness isn't destruction and still kills.
- **Auras and Equipment are one mechanism** (`Card.AttachedToId`, Rules/Attachments.cs). An Aura targets
  as it is cast (`AuraComponent`) and attaches on resolving; what either grants is a live static
  (scope `Is.AttachedToSource`), never stamped. "When enchanted creature dies" looks back through
  `DiedEvent.Attached` — the state check takes the Aura in the same pass.
- **Countering is `StackResolution.Counter`** — a spell to its graveyard through MoveCard (flashback and
  prepared copies still hold), an ability removed, "can't be countered" untouched. "Unless its
  controller pays" asks that player (`CounterUnlessPays`). **Ward** is a trigger whose "it" is the
  targeting spell or ability, carried as the event card of `BecameTargetEvent`.
- **"When you do"** (a reflexive trigger, CR 603.12) is the `WhenYouDo` effect: its targets are chosen
  as it triggers, after the thing done, and it goes on the stack above — never fold it into the effect.
  It triggers only if the thing was done: after a mill it goes in `Mill.IfMilled` (an empty library mills nothing).
- **A target is a zone and a filter** (`TargetSpec(From, Filter)`): the zone says where candidates
  come from, the filter which of them (`Is.…`), and `TargetSpec` alone adds the targeting rules —
  hexproof on the battlefield, and off it a spell or ability never targets its own card. Targets in a
  graveyard are `TargetSpec.InYourGraveyard` (or `InAnyGraveyard`, "from a graveyard") — the same
  legality test on choosing and resolving. A COST that picks (sacrifice, exile from your graveyard) is
  not targeting: a bare `Filter` over the chooser's own zone (`filter.Among(…)`, in `FilterChoices`).
  "One or two target creatures" is two words, the second `Optional` and `NotThePrevious`.
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
  can't change before resolution, so it may be an `EventTest` on the `Trigger` instead (Way of the Mind Sculptor's
  "removed two or more"). `If` runs its steps as a child pipeline — so a surveil inside it finishes
  before the next step — and its steps' outputs do not reach the steps after it.
- **Prowess is a scripted trigger** (`FraCards.Prowess`), not an engine keyword — each instance
  triggers on its own (Ruric Thar has two).
- **"Entering doesn't cause abilities to trigger"** (Karn, Argent Defender) drops those entering
  events in `Triggers.Collect` before ANY ability is matched — read at the next priority, so Karn
  entering alongside a creature silences it too, as the rulings say.
- **"Without paying its mana cost"** (Omnipresence) is `CastSpellAction.WithoutPayingManaCost`, offered
  and checked by `Playable.MayCastWithoutPaying`: X is 0, and `Costs.Of(…, free: true)` still adds
  taxes and additional costs.
- **A card exiled for a linked ability** (Null Summoner) carries `ExiledBy(source, incarnation)`;
  `Playable.IsCastableThroughLink` asks the source's static (`MayCastWhatItExiled`) — so only while that
  same object is on the battlefield. "Mana of any type can be spent" is `ManaCost.OfAnyType` in `Costs.Of`.
- **"If you cast it"** is `Card.WasCast`, set by `EnterBattlefield(cast: true)` from a resolving spell.
- **"Whenever this creature is dealt damage"** triggers on `CreatureDealtDamageEvent` from `Damage.Deal`;
  "that many" is the trigger's X (`Triggers.Pending.X`). **Cards put into a graveyard from a library**
  (mill, surveil, anything) are counted in `MoveCard` (`TurnTally.CardsFromLibraryToGraveyard`).
- **Who knows a hidden card is `Card.KnownTo`** (`Knowers`, relative to the owner — story AI-3): in a
  library, who knows its position; in a hand, whether the opponent saw it. `MoveCard` sets it on every
  move — out of a public zone into a hand or library is `Everyone`; into its library the owner knows
  where it went; into a hand only the opponent's knowledge counts, and only from the top (a draw — a
  search takes it face down) — and `Shuffle` clears the library's. Anything else that LOOKS or REVEALS
  adds with `s.MakeKnown`: surveil's look, a search's `Reveal` (set it whenever the text says "reveal
  it"), "reveals their hand", behold from the hand (a cost — Theorist's Sanctum's behold as it enters
  is a condition and reveals nothing, a known gap), "look at target opponent's hand" (`LookAtHand`, V'Ger —
  the table logs what you now know there; showing it in the hand is TBL-6). **Exiled face down**
  (`ExileTopPlayable.FaceDown`, Vulcan Mind-Meld) is `ExiledFaceDown` + `KnownTo` (who looked); every front
  end asks `Card.IsHiddenFrom(viewer)` and draws a back. **Zefram's "look at the top card any time"** is
  read, never stamped — `MagicState.TopIsKnownToOwner` (the top changes on every path). A random order (Identity Echo's bottom) is NOT
  known. A new card that looks at or reveals a hidden card must do the same, or the search's "shuffle
  what isn't known" reshuffles a card its player has seen.
- **"From outside the game"** (Extrapolate the Impossible) is the `Sideboard` zone — Bo1 limited's
  unplayed pool. It exists only in a game given one, through `MagicRules.WithSideboard` (NewGame's
  `sideboard1/2`, and tests), made after both decks: a game without keeps every id it had —
  the golden replays and the position suite's move tokens hold ids. `CardsIn` of a missing sideboard
  is empty. A card leaving it was revealed, so `MoveCard` makes it known to everyone.
- **Randomness** (a discard at random, a random order) takes `s.TakeRng()`, so a seeded game replays.
  A search for several cards may take two of a name unless `SearchLibrary.DifferentNames` (Fblthp).
  A spell that exiles itself as it resolves (Sphinx's Approach) is left there by `FinishSpellAction`.
- **Copying a permanent** (CR 707.2) is `Card.WithCopiableValuesOf` — name, cost, types, colours,
  P/T, keywords and abilities, never its status (`Card.IsStatus`: counters, damage, what it was
  given; the same set a zone change strips). A token copy (`CreateTokenCopy`) is a blank token with
  those values, entering through `EnterBattlefield` so its own "enters" fires; a source that just died
  is copied as it last existed. "Becomes a copy until end of turn" (Hall of Echoes) keeps the original
  in `CopyUntilEndOfTurn`, restored in cleanup AND as it leaves (`Card.Fresh`), so a dead Hall is a Hall.
- **A delayed trigger on a permanent** ("exile it at the beginning of the next end step") is a
  `DelayedTrigger` component: kept when it loses all abilities, not copied, gone with a zone change.
  On a card it EXILED (`Flicker.AtNextEndStep`, Ghost of Illyria: "return that card at the beginning of the
  next end step") `Triggers.Collect` reads that one component there, never the exiled card's own abilities —
  and on a card that DIED (`AtNextEndStep.On`, Dr. McCoy: "return that card … at the beginning of the next
  end step"), read in the graveyard the same way; leaving the graveyard strips it (a new object).
  With no card to hold it ("return a land you control", Erica Ortegas) it is `AtNextEndStep`: an object in
  your command zone, removed once it has resolved. Set up during the end step, either waits for the next turn's.
- **Emblems** (CR 114) are cards in their owner's COMMAND zone (`GetEmblem`): `Triggers.Collect` and
  the statics board read that zone beside the battlefield, so an emblem's trigger has a source
  ("this emblem deals 5 damage") and its anthem applies. Nothing removes them.
- **"Until your next turn"** is `UntilEndOfTurn.UntilTurnOf` (the player): cleanup leaves it, and
  that player's untap step ends it (`TurnStructure.EndUntilYourNextTurn`). One on a permanent rides the
  permanent (Garruk's −4/−1); "until your next turn, whenever …" is an effect card in the command
  zone (`UntilYourNextTurnWhenever`), so it outlives the planeswalker, and is removed when it ends.
- **"−X" loyalty** is `LoyaltyAbilityComponent.MinusX`: one legal action per X up to the loyalty, X on
  the stack for the effects. "Activate only if" is `LoyaltyAbilityComponent.OnlyIf`.
- **Restricted floating mana** ("spend this only to cast a noncreature spell") is
  `ManaPool.Restricted`: the solver offers it first, only to a spell `ManaAbilities.Allows` — the one
  restriction check, permanents' mana too — and `Pay` takes that unit. `ManaPool.Total` excludes it.
- **A type change from an Aura** (Puppet Crafting: "is a 5/5 Construct creature in addition") is
  STAMPED on the host while the Aura holds it (`Card.AnimatedBy`, what it was kept in `Animated`) and put
  back by `Attachments.Check` once it doesn't, and by `Card.Fresh` as the host leaves. Every rule reads
  `card.Is(Creature)`, so a live layer-4 read would need every one of them; the stamp is right by the
  time anyone gets priority.
- **Station** (Edge of Eternities' mechanic; TRK's Spacecraft, MC-5.2) is `TrkCards.Station(n, keywords)`. The
  station ability's cost is `TapUntappedCost(1, …)` — tapping ONE is a choice (`ActivateAbilityAction.TapId`,
  one legal action per creature; summoning sickness doesn't matter), and that creature is the ability's
  event card, so "its power" is read as it resolves, from its snapshot once it left (a `Count` only —
  `Ref.CountedAsTheyWere`; a `Matches` asks where it is now;
  a known ceiling: the snapshot is as it was tapped). A negative power counts 0 (`Count.Value`). **"It's an
  artifact creature at N+" is read LIVE by `Card.Is`** from the card's OWN charge counters
  (`StationComponent`) — not stamped like Puppet Crafting, since nothing outside the card decides it — so it
  is a creature exactly while it has N, and stays one when it loses all abilities (a type change, layer 4).
- **Casting in the middle of a resolution** ("you may cast that card", Chandra, Torch of Defiance;
  Uldaros' free copies) is `CastNow`: a real `CastSpellAction` with `DuringResolution` — the engine's
  own cast candidates, checked by `ValidateAdd` (no priority or timing; modes, targets, X, additional
  costs all still apply), the player picking one; then `Cast`, through `PutOnStack` — so "whenever you
  cast" sees it. A copy of a permanent spell resolves into a token.
- **"When you cast this spell"** (`TriggerEvent.SelfCast`, Emrakul) is read from the spell on the stack.
- **Base P/T effects** ("has base power and toughness 4/5") sit under counters and pumps, latest
  wins (`UntilEndOfTurn.SetBase`, read in `Card.OwnPower`/`OwnToughness` under `s.Power`/`Toughness`).
  With no duration (Seven of Nine: "base power and toughness become 7/9") it is `ModifyUntilEndOfTurn.ForGood`
  — `UntilTurnOf = UntilEndOfTurn.Never`, which no turn ends: only leaving the battlefield.
- **An activated ability an Aura grants** ("enchanted creature has '{T}: …'", Splinter Twin) is
  `GrantsActivatedAbility` on the Aura. `s.ActivatedAbilitiesOf(card)` is THE list activation indexes
  into — own, then granted — read by the candidates, the action and `Describe`; never
  `AbilitiesOf<ActivatedAbilityComponent>` for activation, or a granted ability is offered and refused.
  It is not copiable: a token copy of the host has none. A delayed trigger on a new token
  (`CreateTokenCopy.ExileAtNextEndStep`) is added AFTER it enters — entering strips one given before.
- **"[Permanents] enter tapped"** from another permanent (Root Maze) is `PermanentsEnterTapped(filter)`
  on it, read by `EnterBattlefield` beside the card's own `EntersTappedComponent` — everyone's, not just yours.
- **A permanent spell resolves in two steps** (`EnterFromStackAction`, then `ActivePlayerGetsPriorityAction`),
  so a choice it makes as it enters is answered before anyone gets priority — and before the state
  check sees Flesh Duplicate as a 0/0. **"Enter as a copy"** (`MayEnterAsCopy`) is that choice, then
  `BecomeCopyOfChosenAction`: its copiable values, with `EnteredAsCopy(original)` restored as it leaves
  (`Card.WithoutCopyEffect`), never in cleanup. **Vanishing** is `Vanishing.Of(n)`: the marker, an upkeep
  trigger (`RemoveTimeCounter`) and `Card.TimeCounters`.
- **Targeting an ability on the stack** is `TargetSpec.Ability(filter)` (`From.Ability`; a `StackAbility`,
  not a card); `Is.TriggeredAbility` / `Is.ActivatedAbility` read `StackAbility.Triggered`, set where a
  trigger goes on the stack. Countering one removes it (`StackResolution.Counter`).
- **Replicate** is `CastSpellAction.Replicate` (enumerated like X, in `MoveKey`): the cost grows by that many
  `ReplicateComponent` costs, and a triggered ability above the spell copies it that many times.
  **"Choose one or more"** is `SpellEffectsComponent.ChooseAtLeast` — `Modal.Choices`/`IsLegal` take every size.
- **Necrodominance's rules each live in their one place:** a skipped draw step never begins (`EnterStep`),
  hand size is `TurnStructure.MaxHandSizeOf`, and "exile instead of your graveyard" is decided in `MoveCard`
  with the other exile-instead cases — so nothing of yours dies.
- **Becoming tapped is `MagicState.Tap`** — the ONE way a permanent taps: attacking, a {T} cost, "tap N
  untapped" (Station's too), the Tap effect, tapping for mana and mana payment. It raises
  `BecameTappedEvent` only on untapped → tapped ("whenever Hemmer becomes tapped"); entering tapped is
  not becoming tapped. A new tap path must call it, or Hemmer silently misses it.
- **"Tap two untapped …"** (Worf, Tenured Tethermage) is the player's choice of both: `TapUntappedCost(2, …)`
  is split as `ActivatedAbilityComponent.Costs` is built into two one-pick costs (`TapId`, `SecondTapId`),
  each pair in either click order (the table), one move by `MoveKey` (the AI) — Shayne, 2026-10-31.
  **"Tap three" or more** (Collation Trimatrix, Picard's seven Officers) is ONE cost picking the whole set
  (`CostSlot.TapSet`, `ActivateAbilityAction.TapIds`): one legal action per set, never per order
  (`MagicRules.TapSets`), clicked in any order on the table (`Targeting.Narrow`) — Shayne, 2026-11-05.
- **Beam me up** (TRK) is a `FlashbackComponent` whose cost beside the mana is `BeamMeUpCost` — return a
  creature you control (`CastSpellAction.ReturnId`, one cast per creature) — so it is cast and exiled as
  flashback is (`TrkCards.BeamMeUp`). Marooned's `EnchantedCantBeBeamedUp` takes its creature out of the
  candidates.
- **Face a dilemma** (TRK) is `FacedDilemmaEvent`, ONE however many modes, raised where modes are chosen:
  a cast (`PutOnStack`, a cast during a resolution too), an activation with `Modes`, and a modal trigger's
  `ChooseModesChoice` (`QueueModalTriggerAction`) — whether or not it resolves. Copies choose nothing.
- **"Whenever you attack WHILE …"** (power 8) is `Trigger.While` — part of the trigger condition, judged
  only as the event happens (`Triggers.Matches`); never `TriggeredAbilityComponent.If`, which is judged
  again on resolution (release notes: "it doesn't matter … when [ability] resolves").
- **Look and take** ("look at the top N, you may reveal a [filter] card and put it into your hand, the rest
  on the bottom in a random order") is `LookAndTake` — `Must`, `RestToGraveyard`, `Reveal` (known to
  everyone). The rest go down as Identity Echo's: a random order, not known.
- **A distribution decided as it is cast** (Pure Energy, CR 601.2d) reads `TargetChosen(i)` — the targets
  as CHOSEN (`ContextKeys.ChosenTargets`); `Ref.Target` reads 0 for one gone illegal, which would hand
  its share to the others.
- **One card, a printing per set (MC-5.5).** `CardRegistry` is the one lookup across sets: a card's
  rules data and script exist once; its printing (`CardPrinting`: rarity, collector number) is per set,
  and a card in play carries its printing's set (`Card.Set`, kept through zone changes) — read for its
  ART only, never by a rule. A sealed or draft session passes its set (`CardRegistry.Get(name, set)`);
  boosters, ratings and drafts take a set code (`Boosters.Pack(set, rng)`, `CardRatings.Of(set)`,
  `Draft.New(set, seed)`). Rarity is never read from `CardData` — it isn't there.
- **Counters of every kind are put on and removed through `MagicState.PutCounters` alone** (`CounterKinds`):
  the named ones (TRK: quest, lore, promotion) in ONE map, `Card.Counters`, raising `CountersPutEvent` (how
  many it had and has); the older kinds (+1/+1, -1/-1, charge, stun, time, loyalty) in their own fields
  (`Card.CountersOf`/`WithCounters`). Finality is a yes/no flag, not a count. `CounterWritesTests` fails on
  an engine file that adds to a counter field itself — it would skip every replacement.
  "A permanent with a counter on it" is ANY kind — `Is.WithAnyCounter` (`Card.HasAnyCounter`) reads every
  field and the map; a new counter field must join it. Proliferate doesn't see the map yet (no card needs it).
- **Sagas** (CR 714) are `SagaComponent(chapters)` + `Scripting.Chapter(n, …)`: a trigger on
  `CountersPutEvent` that fires as the lore count goes from below n to at least n (`Trigger.Chapter`). The
  lore counter is added as it enters (`EnterBattlefield`) and as its controller's precombat main phase
  begins — a TURN-BASED action in `EnterStep`, no stack (Shayne, 2026-11-01). The state-based check
  sacrifices a Saga at its last chapter only once no chapter ability of it is waiting: collected and not
  yet on the stack (`GivePriority` passes them to `StateBasedActions.Check`) or on the stack.
- **Kicker** is `KickerComponent(mana) { Costs }` and `CastSpellAction.Kicked` (one cast unkicked, one
  kicked; `|k` in `MoveKey` only when kicked): the mana joins the total in `Costs.Of(…, added:)`, the costs
  join `CastSpellAction.CostsOf(…, kicked)` — a kicker sacrifice is the ordinary `SacrificeId`. The stack
  keeps it (`OnStackComponent.Kicked`, copies too); "if kicked" is `WasKicked`; a target word that reads
  differently kicked is `TargetSpec.IfKicked`, swapped in by `StackResolution.TextOf(…, kicked)`.
- **"Can't block"** is `Combat.CantBlock`, a keyword-like grant read by `Combat.CanBlock` — a static
  (Cleveland Booker's Worm) or until end of turn (La'An).
- **"Activate only once each turn"** is `ActivatedAbilityComponent.OncePerTurn`, stamped on the card as it
  is activated (`Card.ActivatedOncePerTurnOn`) — a new object may activate again.
- **"Whenever [a creature with …] deals combat damage to a player"** — not "this" — is
  `Trigger.CombatDamageBy`, a filter on the damage's SOURCE (Phlox); the event's subject is the player.
- **"Sacrifice it at the beginning of the next end step"** on a new token is `CreateTokens`/`CreateTokenCopy`
  `.SacrificeAtNextEndStep` — a `DelayedTrigger` added after it enters, like `ExileAtNextEndStep`.
- **A linked "the exiled card"** (The Measure of a Man) is `Exile { Linked }` (stamps `ExiledBy` with this
  object of the source) and `ReturnLinkedExile` — nothing once the source has left (CR 607.2a).
- **"Whenever …, this turn"** from a resolving ability (The Naked Time's chapter II) is `WheneverThisTurn`:
  the trigger on a command-zone object, removed with the turn's other "this turn" effects.
- **"Whenever a card leaves your graveyard"** is `LeftGraveyardEvent`, raised by `MagicState.MoveCard` for every
  path out — cast, exiled, returned, shuffled, paid as a cost; a token isn't a card. **"One or more"** is
  `Trigger.OneOrMore`: one trigger per priority pass, everything that left since a player last had priority
  (Shayne, 2026-11-01).
- **A permanent's subtypes are `s.Subtypes(card)`** (Characteristics) — never `Card.Subtypes` in a rule.
  "Becomes a Doctor (it loses all other creature types)" until end of turn (`ModifyUntilEndOfTurn.BecomesCreatureType`)
  or under an Aura (`EnchantedIsCreatureType`, De-evolve) is that one type plus the subtypes that aren't
  creature types (`Characteristics.IsCreatureType`). `Is.Subtype`, Federation's count, behold and Kindred
  Judgment read it.
- **"Can't be blocked by [creatures with …]"** is `CantBeBlockedBy(filter)` on the attacker, the filter asked
  of each blocker (`Combat.CanBlock`). **"Can't be the target of abilities from [sources]"** is
  `CantBeTargetedByAbilitiesFrom(filter)`, asked in `TargetSpec` beside hexproof — abilities only: the source
  is a permanent, not a spell on the stack.
- **"If this is the second time this ability has resolved this turn"** is `IfNthResolutionThisTurn(n, …)`:
  counted on the source as it resolves (`Card.ResolvedTimes`, by turn), the rest run on exactly the Nth.
- **"[Permanents] you control have '[triggered ability]'"** is `GrantsTriggeredAbility(scope, ability)`:
  `Triggers.Collect` gives it to each permanent the scope matches, and "this creature" is that one.
- **Gaining control is `MagicState.GainControl`** — the same object (counters, damage, attachments,
  until-end-of-turn effects stay), moved to its new controller's battlefield zone, summoning sick for them,
  out of combat; no zone change, no event.
- **"That many plus one of each kind of counter"** (Brad Boimler, `CountersPlusOneThisTurn`) is
  `Rules.Replacements.Counters`, applied in `MagicState.PutCounters` — so it reaches every counter path:
  effects, costs (`Loyalty.Change`), entering with loyalty, time or +1/+1 counters, proliferate.
  **"Twice that many charge counters on an artifact you control"** (Geordi La Forge,
  `ChargeCountersOnYourArtifactsDoubled`) is `Rules.Replacements.ChargeCounters` there too, AFTER the plus
  one — (n + 1) × 2, the order its controller would pick (CR 616.1) — entering with them included (Picard's
  seven become fourteen).
- **Life is lost in `Rules.Life.Lose` alone** — damage to a player, `LoseLife`, paying life — which counts it
  in `TurnTally.LifeLost` (Gul Dukat: "the life that player lost this turn", `Count.LifeLostThisTurnOf`).
  His "you gain life equal to the life lost this way" is scripted gain FIRST, so both read the count before
  the loss adds to it.
- **"Reveal the top X, put every [type] card onto the battlefield, the rest on the bottom in a random
  order"** (Branching Timeline) is `RevealUntilOntoBattlefield { Top }` — Identity Echo's effect, every match
  among the top X instead of "until".
- **"Whenever you draw a card"** is `DrewCardEvent`, raised by `MagicState.Draw` once per card (Data, New Life).
- **"Can't attack alone"** (Pet Sehlat) is `Combat.CantAttackAlone`, a keyword-like grant judged on the whole
  declaration as it finishes (`FinishAttackersAction`): one such creature declared alone doesn't attack; two
  together each aren't alone. The table doesn't refuse the declaration yet (TBL-7).
- **"Lands you control enter untapped"** (Warp Speed, `LandsYouControlEnterUntapped`) wins over every "enters
  tapped" in `EnterBattlefield` — the land's own, Root Maze's, a token or a return made tapped (Shayne,
  2026-11-01). **"[Permanents] you control enter with N additional [kind] counters"** (Zefram Cochrane) is
  `YourPermanentsEnterWithCounters`, put on there through `PutCounters`.
- **Playing a card from exile** is `PlayableFromExile(player, until)` on it — anyone's card, in its owner's
  exile (`Playable.IsImpulsePlayableBy`; `Candidates` reads both exiles): `AnyType` ("mana of any type",
  Vulcan Mind-Meld) and `Tax` (Zero's "{2} more") are read by `Costs.Of`. `ExileTopPlayable` exiles it —
  whose library (`Of`), who plays (`By`), "for as long as it remains exiled" (`Forever`), face down, or only
  a chosen card (`Choose`, Chekov's land). Cast or played, the card is public again (`ExiledFaceDown` is status).
- **"Once during each of your turns, cast an artifact spell from the top of your library"** (Zefram) is
  `Playable.LibraryTopPermission`, the permanent stamped as the cast uses it (`Card.CastFromLibraryTopOn`).
- **"The first spell you've cast this game"** is stamped as it is cast (`OnStackComponent.FirstSpellThisGame`,
  from `MagicPlayer.SpellsCastThisGame`), read by `FirstSpellYouCastThisGame` — a copy wasn't cast.
- **"When that creature dies this turn, you …"** (A Good Day to Die) is `WheneverThisTurn { About }`: the
  trigger on YOUR command-zone object, its subject bound to that one object as it resolves (`TheObject`).
- **"Enter as a copy of any artifact or creature, except it isn't legendary, with its counters"** (Dominion
  Saboteur) is `MayEnterAsCopy { Of, NotLegendary, WithCounters, Optional }` — the counters through
  `PutCounters` (`Card.AllCounters`); `Optional` is a real "may" (Flesh Duplicate's stays forced).
- **"You may cast this card from your graveyard"** (Storied Veteran) is `MayCastFromYourGraveyard`, read by
  `Playable.CanCastFromHere` — not flashback: it isn't exiled leaving the stack, it resolves onto the
  battlefield (with a finality counter, `EnterFromStackAction.Finality`). **"… from exile"** (Khan) is
  `MayCastFromExile` beside it — your own exile, never face down (a face-down card has no abilities).
- **"When this land enters untapped, choose one —"** (TRK's five, `TrkCards.EntersUntapped`) is a `SelfEnters`
  trigger whose Subject is `Is.Source & !Is.Tapped` — read as it entered, so Warp Speed's untapped lands
  trigger too, and one entering tapped never does.
- **A dying permanent's own "whenever a creature … dies" sees its own death** (CR 603.10a, Excalbian Gamemaster's
  "this creature or another creature you control"): `Triggers.Collect` reads the dying card's Died triggers as
  it last existed, not only "when this dies"; another's death is matched on the battlefield, never there.
- **An activated ability's event card** is the creature tapped for its cost (Station's "its power") or, with
  none tapped, the one sacrificed (Gowron: "if you sacrificed a Klingon this way" — `Matches(Ref.EventCard, …)
  { AsItLastWas }`, its snapshot from before the cost: a sacrificed token has ceased to exist by then).
- **"No maximum hand size until your next turn"** (Deep Space Scan) is `NoMaximumHandSizeUntilYourNextTurn`: an
  `UntilEndOfTurn { NoMaximumHandSize }` object in your command zone, read by `TurnStructure.MaxHandSizeOf`.
- **"Choose one. If [condition], choose one or more instead"** (Captain Kirk) is
  `TriggeredAbilityComponent.ChooseAnyNumberIf`, judged as the modes are chosen (the trigger going on the stack).
- **The shocklands' "you may pay 2 life. If you don't, it enters tapped"** is `EntersTappedComponent { UnlessYouPayLife }`:
  it enters tapped, and `EnterBattlefield` spawns a `May` ONLY when its own text is what taps it — not under Warp
  Speed (untapped anyway) or Root Maze / "onto the battlefield tapped" (tapped anyway) — and the controller has
  the life (CR 119.4). Yes is `LoseLife` (the one life-loss path) and it untaps. The AI's answer is the search's
  (Shayne, 2026-11-05): both answers are beam nodes, so it pays when the turn's plan spends the mana.
- **Sunburst** (`Sunburst`, Engineered Explosives) is the colours actually SPENT: the cast pays with the most
  different colours it can for the X chosen (`ManaPayment.Find(…, mostColors)`, Shayne, 2026-11-05), records
  them (`ManaPayment.ColorsSpent` → `OnStackComponent.ColorsSpent`), and `EnterFromStackAction` puts that many
  charge counters through `PutCounters` (Geordi doubles them). A copy spent nothing (CR 702.44c). It never
  sacrifices a Treasure or uses Starfleet Academy for a colour that the plain payment wouldn't have.
- **"With mana value equal to [a number]"** counted as it resolves is `Ref.ManaValueIs` (a `Count`) on a
  `Ref.All` — Explosives' own charge counters, read from the sacrificed source as it last was.
- **"X target [things]"** (Dominion Infiltration) is `TargetSpec.PerX`: `StackResolution.TextOf(…, x)` makes it
  X words, each `NotThePrevious` (`AreLegal` refuses any repeat in the chain). There is ONE legal cast per X
  (`CastSpellAction.ChoosesTargets`): casting it asks `ChooseXTargetsChoice` (exactly X), then casts with them —
  never a cast per set, which is 2^n and hung the fuzz probe (Shayne, 2026-11-05). The AI copies the biggest
  (`GreedyAi`). "For each of them" is `Ref.EachTarget`.
- **Changeling** ("every creature type") is `s.HasSubtype(card, type)`, asked one type at a time — every
  subtype question goes there (`Is.Subtype`, Kindred Judgment), never `s.Subtypes(…).Contains`, except behold
  (planeswalker types). Read from the card's own keywords: `s.Has` would loop through the statics' scopes.
- **"As this enters, choose a number"** (Nyota Uhura) is `ChoosesNumberAsItEnters` → `ChosenNumber` on the
  card (status: gone as it leaves); "a spell with mana value equal to the chosen number" is
  `Is.ManaValueIsChosenNumber`. The AI names the mana value commonest among the opponent's nonland cards it
  knows of, else 2 (`GreedyAi`, Shayne) — the beam doesn't branch on it (`ChooseNumberChoice.NamesAManaValue`).
