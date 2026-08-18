# MtgCore — MTG Card Game Engine

## Source Map

```
MtgCore/
├── Abilities/Activated/     # ActivatedAbilityComponent, ActivatedAbilityAction
│                            # ActivatedAbilityComponent.Condition — optional ActivationCondition gate ("activate only if...")
│                            # ActivationCondition (abstract; IsSatisfied + Describe). Subclasses:
│                            #   LifeAboveStartingCondition (Speaker of the Heavens — reads MtgPlayer.StartingLife, not a hardcoded 20)
│                            #   ControlsMoreLandsCondition (Knight of the White Orchid)
│                            # RequiresTap now genuinely gates AND exhausts — see "Exhaust" below.
│   └── Static/              # StaticAbilityComponent (abstract; Filter, AffectedIds, ActiveInZone), StaticPTBoostAbility, StaticGrantKeywordAbility
│                            # StaticAbilityComponent.ActiveInZone — zone the SOURCE must be in for the ability to apply.
│                            #   Default Battlefield. Set to Graveyard for Wonder-style effects. See "Zone-Dependent Statics".
│                            # AppliedKeywordComponent.Duration — Permanent grants are owned by StaticAbilityEngine;
│                            #   UntilEndOfTurn grants are stamped by GrantKeywordAction and cleared by StartTurnAction.
├── Emblems/                 # Emblem (Name, TriggerCondition, CardEffect) — player-owned persistent triggered abilities
│                            # GrantEmblemComponent — placed on a land card; PlayLandAction/PutLandIntoPlayAction add the emblem to the player when the land is played
│                            # MtgPlayer.Emblems: ImmutableList<Emblem> holds all active emblems; scanned by CheckStateBasedEffectsAction after battlefield/graveyard passes
├── Actions/                 # All GameAction subclasses; ContextKeys; MtgActionGenerator
│                            # EffectAction — abstract base for all state-changing actions (DealDamageAction,
│                            #   GainLifeAction, LoseLifeAction, DrawCardsAction, DiscardCardsAction,
│                            #   AddTemporaryManaAction, ExileAction, DestroyCreatureAction, AddModifierAction).
│                            #   Provides TargetContextKey, AmountContextKey, TargetIds, WithTargets(),
│                            #   ResolveTargetIds(), ResolveAmount(), and lenient ValidateResolve.
│                            # Data/query actions (CountCardsWithSubtypeAction, CountCardsWithNameAction,
│                            #   RevealTopCardAction, LookAtTopCardsAction, SelectCardFromLibraryAction,
│                            #   SelectCardFromHandByManaCostAction, SelectCreatureFromBattlefieldByManaCostAction)
│                            #   are NOT EffectActions — they still use PlayerIdContextKey (scalar).
│                            # SelectCardFromHandByManaCostAction — picks highest/lowest non-land card from a player's (or opponent's)
│                            #   hand by ManaCost; writes card ID to OutputKey. No-ops (writes nothing) if no non-land cards found.
│                            #   TargetOpponent=true derives the opponent via Player1/Player2 well-known IDs.
│                            # SelectCreatureFromBattlefieldByManaCostAction — same pattern but targets battlefield creatures.
│                            # DiscardRandomCardAction — GameAction (not EffectAction); picks a random card from a player's (or
│                            #   opponent's) hand and moves it to graveyard. Emits CardDiscardedEvent. No-ops if hand is empty.
│                            # DrainLifeAction — GameAction; opponent loses Amount life, caster gains Amount life. Emits
│                            #   PlayerLostLifeEvent + PlayerGainedLifeEvent. Used by Siege Rhino ETB.
│                            # MillAction — EffectAction; moves Amount cards from each target player's library to their
│                            #   graveyard. Targets are PLAYERS. Emits CardMilledEvent per card (distinct from
│                            #   CardDiscardedEvent so discard payoffs don't fire on self-mill) and LibraryEmptyEvent
│                            #   when the library runs out. OutputKey writes the milled card IDs to pipeline context.
│                            # ExhaustCreatureAction — EffectAction; "tap target creature". Sets
│                            #   CreatureComponent.IsExhausted and emits CreatureExhaustedEvent. See "Exhaust".
│                            # ExileTopCardPlayableAction — EffectAction; impulse draw. Exiles each target
│                            #   PLAYER's top card and stamps ExiledPlayableComponent. See "Impulse Draw".
│                            # DealDamageAction damages PLANESWALKERS as well as players and creatures —
│                            #   the walker arm routes to DamagePlaneswalker. See "Planeswalkers".
│                            # GainPermanentManaAction — EffectAction; +MaxMana AND +CurrentMana permanently.
│                            #   How "search for a Plains and put it onto the battlefield" is expressed: lands are
│                            #   consumed into MaxMana and exiled, so there is no land permanent to fetch.
│                            #   Contrast AddTemporaryManaAction, which raises CurrentMana only.
│                            # CountCardsWithSubtypeAction.CreaturesOnly — restricts the count to cards with a
│                            #   CreatureComponent. Needed for "for each creature you control" (Lena): an empty
│                            #   Subtype on the battlefield otherwise counts artifacts and enchantments too.
│                            # GrantKeywordAction — EffectAction; stamps AppliedKeywordComponent onto target creatures
│                            #   for a Duration (default UntilEndOfTurn). The effect-driven counterpart to
│                            #   StaticGrantKeywordAbility — use for combat tricks and "gains X until end of turn".
│                            # ReturnToHandAction — EffectAction; targeted counterpart to MoveCardToHandAction.
│                            # FightAction — EffectAction; source (from ContextKeys.SourceCardId) and target deal
│                            #   damage to each other. Deliberately NOT routed through AttackAction: fighting must not
│                            #   set HasAttacked, must ignore summoning sickness, and must ignore Taunt and the Flying
│                            #   restriction — a ground creature can fight a flyer it could never attack.
│                            # Includes: PutIntoBattlefieldAction (CardIdContextKey for pipeline use), CastCreatureAction, ResolveCreatureAction
│                            #           CastPermanentAction, ResolvePermanentAction (non-creature permanents → battlefield)
│                            #           CreateTokenAction
│                            #           TransformAction (EffectAction; swaps card face in place via TransformComponent; carries creature state across)
│                            #           AttachEquipmentAction (ITargetedAction; reads equipment ID from ContextKeys.SourceCardId)
│                            #           PlayLandAction (play land from hand: MaxMana+totalMana, CurrentMana+manaThisTurn, LandsPlayedThisTurn++, LandsPlayedTotal++, card → exile; checks BonusManaLandComponent and LandPlayEffectComponent)
│                            #           PutLandIntoPlayAction (effect-sourced land: same mana logic as PlayLandAction, LandsPlayedTotal++ only, card → exile; used by Rampant Growth/Primeval Titan)
│                            #           SelectCardFromZoneAction (like SelectCardFromLibraryAction but targets any ZoneType; used by Simic Growth Chamber to find a land in exile)
│                            #           CastFromGraveyardAction (flashback: casts a spell from graveyard at FlashbackManaCost; card exiles after resolution via MoveCardToExileAction)
│                            #           MoveCardToExileAction (post-resolution cleanup for flashback; analogous to MoveCardToGraveyardAction but routes to exile)
│                            #           MoveCardToGraveyardAction (post-resolution cleanup for normal spells)
│                            #           ResolveSpellAction.ExileAfterResolution — when true, spawns MoveCardToExileAction instead of MoveCardToGraveyardAction
│                            #           GiveFlashbackAction (EffectAction; adds FlashbackComponent{FlashbackManaCost=card.ManaCost} to a target spell in the graveyard; no-ops if already present)
│                            # DealDamageAction has PlayerOutputKey and CreatureOutputKey for pipeline chaining.
├── Costs/                   # AdditionalCost (abstract), LifeAdditionalCost, SacrificeAdditionalCost, DiscardAdditionalCost
│                            # DiscardAdditionalCost.Filter — restricts what may be pitched ("discard a land
│                            #   card"). Enforced in BOTH GetValidPayments and Validate; the generator reads
│                            #   the first and the human UI path reaches the second.
├── Cards/                   # Card (GameObject subclass, has Subtypes + HasSubtype()), CardLibrary
│                            # CardType — [Flags] enum: Creature, Instant, Sorcery, Artifact,
│                            #   Enchantment, Land, Planeswalker, plus AnyPermanent / AnySpell.
│                            # Card.Types (declared, set by builders) and Card.EffectiveTypes
│                            #   (falls back to derivation). Ask Card.HasType(...) — see "Card Types".
│                            # PlaneswalkerComponent { StartingLoyalty, Loyalty, HasActivatedThisTurn }
│                            # Card lookup: use CardLibrary.GetByName("Name") — do NOT add new static per-card accessor methods.
│                            # The existing static accessors (LightningBolt(), GrizzlyBears(), etc.) are legacy and are being phased out.
│   ├── Builders/            # Fluent card builder API: CardFactory (entry point), SpellCardBuilder, CreatureCardBuilder,
│   │                        # TargetBuilder (use via 'using static'), TriggerConditions (static helpers: OnSelfEntersBattlefield, OnSelfEntersBattlefieldAsNonCreature, OnYourUpkeep, OnAnyCreatureDies, OnAnyCreatureAttacks, OnSelfAttacks, OnLandfall)
│   │                        # Usage: CardFactory.Spell("Name", manaCost).WithDamage(3).WithTarget(Single().PlayersOrCreatures()).Build()
│   │                        # SpellCardBuilder.WithFlashback(cost) adds FlashbackComponent — card becomes castable from graveyard at that cost
│   │                        # SpellCardBuilder.WithGiveFlashback() — effect that adds FlashbackComponent to a random instant/sorcery in your graveyard; use in ETB triggers
│   │                        # CreatureCardBuilder.WithEtbTrigger(name, effect) — shorthand for WithTriggeredAbility using OnSelfEntersBattlefield() condition
│   │                        # TargetBuilder.InstantOrSorceryInYourGraveyard() — targets an instant/sorcery in the caster's own graveyard
│   │                        # TargetBuilder.CreatureInYourGraveyard() — targets a creature card in the caster's own graveyard
│   │                        # TriggerConditions.OnAnyArtifactDies() — fires on ArtifactLeftBattlefieldEvent (any artifact sacrificed or destroyed)
│   └── Components/          # PermanentComponent (battlefield marker), CreatureComponent (HasHaste, HasDoubleStrike, HasFlying, HasTaunt, HasReach, HasShroud, HasHexproof), SpellComponent (HasStorm), GraveyardCountComponent
│                            # FlashbackComponent { FlashbackManaCost } — marks a spell castable from graveyard; MtgActionGenerator scans graveyard for these and generates CastFromGraveyardAction
│                            # EquipmentComponent (PowerBonus, ToughnessBonus, EquippedToCardId — tracks attachment state)
│                            # ExtraLandPerTurnComponent — marker; presence on a controlled battlefield permanent grants +1 land play per turn (used by Exploration)
│                            # LandPlayEffectComponent { Effect: CardEffect } — spawns a ResolveEffectAction when the land is played or put into play; used by Glimmervoid (gain 2 life) and Simic Growth Chamber (return exile land to hand)
│                            # BonusManaLandComponent { ExtraMana, Deferred } — overrides land mana production: adds (1+ExtraMana) to MaxMana; if Deferred=true, CurrentMana is unchanged (mana usable next turn only); used by Simic Growth Chamber
│                            # TransformComponent (OtherFaceName, OtherFaceSubtypes, OtherFaceComponents) — stores the other face of a double-faced card; TransformAction swaps Name/Subtypes/Components in place, preserving the card's ID and carrying creature state across
│                            # AffinityComponent — marker (no data); when present on a card, CastCreatureAction and CastSpellAction reduce ManaCost by the number of artifact permanents the casting player controls (min 0). Used by Frogmite, Myr Enforcer, Thoughtcast.
│                            # ExiledPlayableComponent — marker; this exiled card may still be played this
│                            #   turn (impulse draw). See "Impulse Draw" below.
├── Effects/                 # CardEffect (data-only effect descriptor)
├── Events/                  # EventTypeNames, MtgEvents (includes CreatureEnteredBattlefieldEvent, CombatDamageDealtToPlayerEvent,
│                            #   SpellCastEvent emitted by CastSpellAction, CreaturePlayedEvent emitted by CastCreatureAction
│                            #   PermanentEnteredBattlefieldEvent emitted by ResolvePermanentAction for non-creature permanents
│                            #   LandPlayedEvent { PlayerId, CardId } emitted by both PlayLandAction and PutLandIntoPlayAction; triggers Steppe Lynx landfall)
│                            #   ArtifactLeftBattlefieldEvent { CardId, OwnerId } — emitted by SacrificeAdditionalCost when the sacrificed permanent HasSubtype("Artifact");
│                            #   fired in addition to PermanentLeftBattlefieldEvent. Used by Disciple of the Vault and OnAnyArtifactDies() trigger condition.
├── Extensions/              # CreatureEvaluator (P/T aggregation extension methods), StaticAbilityEngine (push-model ETB/LTB logic)
│                            # ReplacementEngine.ApplyReplacements(evt, playerId, amount) — call this at every site
│                            #   that produces a replaceable number. Multipliers apply before additions; result clamped at 0.
│                            # CostEngine.ComputeEffectiveCost(card, playerId) — the SINGLE place mana cost is
│                            #   adjusted. Affinity reduction then SpellTaxComponent increase, floored at 0.
│                            #   All three cast actions route through it; they each had a private copy before,
│                            #   and CastPermanentAction had none, so non-creature permanents ignored affinity.
├── Modifiers/               # PowerToughnessModifier (abstract base), StaticPowerToughnessModifier, AppliedStaticPTBoost
│                            # ReplacementModifierComponent (abstract) + ReplaceableEvent enum — numeric replacement
│                            #   effects. LifeGainBonusComponent is the only concrete one (Angel of Vitality).
│                            #   See "Replacement Effects" below; structural replacement is NOT covered.
│                            # LifeTotalComponent — +X/+X while your life is >= Minimum (Angel of Vitality at 25)
│                            # CreatureCountComponent — P/T equal to creatures you control; how */* is expressed
│                            #   (Crusader of Odric is base 0/0 plus this). Both must be Duration = Permanent.
│                            # EquippedBoostComponent (stamped on creature by AttachEquipmentAction; removed on detach/creature-death)
│                            # LandsPlayedCountComponent — dynamic P/T modifier; bonus = controller's LandsPlayedTotal. Used by Terravore. Must be stamped with Duration = Permanent in card definitions.
├── Sets/                    # CardSet (Code, Name, Cards; Draftable filters lands), SetRegistry (All, Default, Get)
│   ├── CoresetCube/         # The CSC set, built from an external cube list (cubecobra magiccoreset20xx).
│   │                        # WHITE, BLUE, BLACK AND RED COMPLETE — 268 cards. The cube is 450: 67 per colour,
│   │                        # 50 colourless, 53 multicolour. CoresetCube.cs assembles the files:
│   │                        #   CoresetCubeWhite.cs           38 creatures
│   │                        #   CoresetCubeWhiteSpells.cs     10 instants + 6 sorceries
│   │                        #   CoresetCubeWhitePermanents.cs 8 enchantments + 1 equipment + 4 planeswalkers
│   │                        #   CoresetCubeBlue.cs            28 creatures
│   │                        #   CoresetCubeBlueSpells.cs      18 instants + 11 sorceries
│   │                        #   CoresetCubeBluePermanents.cs  6 enchantments + 4 planeswalkers
│   │                        #   CoresetCubeBlack.cs           38 creatures
│   │                        #   CoresetCubeBlackSpells.cs     8 instants + 10 sorceries
│   │                        #   CoresetCubeBlackPermanents.cs 5 enchantments + 4 auras + 2 planeswalkers
│   │                        #   CoresetCube*Tokens.cs         token templates, excluded from the card list
│   │                        # Read each file's header before adding cards — they list every divergence from
│   │                        # the printed card and why.
│   │                        #   CoresetCubeRed.cs             38 creatures
│   │                        #   CoresetCubeRedSpells.cs       12 instants + 8 sorceries
│   │                        #   CoresetCubeRedPermanents.cs   4 enchantments + 1 artifact + 4 planeswalkers
│   │                        # Green not started — it is the last mono-coloured section.
│   └── Hollowmere/          # The HLM graveyard set. Hollowmere.cs assembles 11 theme files + subtype constants;
│                            # HollowmereTokens.cs holds token templates (excluded from the card list).
│                            # Read the header of Hollowmere.cs before adding cards — it states the rate bar and
│                            # why no-blocker combat drives every cost in the set.
│                            # A draftable card pool. Cards do NOT know their set — the set owns the list.
│                            # No SetCode on Card, no rarity, no pack-composition rules; packs stay uniform
│                            # random samples, which is what a cube wants. Add those only when a set needs them.
│                            # CardLibrary.All is registered as the "LEG" (Legacy) set so existing drafts and
│                            # the existing trained model keep working. Sets are separate pools, never merged —
│                            # the trained draft picker is keyed by card name and does not generalise across pools.
├── Players/                 # MtgPlayer (GameObject subclass) — fields: Life, StartingLife (set alongside Life by
│                            #   MtgGameFactory; read by LifeAboveStartingCondition), LifeGainedThisTurn (resets
│                            #   each turn; read by LifeGainedThisTurnCondition), MaxMana, CurrentMana,
│                            #   LandsPlayedThisTurn (resets each turn), LandsPlayedTotal (never resets; used by
│                            #   Terravore and by the land-count conditions), Emblems
├── Targeting/               # TargetSpecification (base), ZoneSpecification (abstract base for zone specs), TargetingContext, TargetingStrategy
│                            # Zone specs: IsOnBattlefieldSpecification, IsInHandSpecification, IsInstantOrSorceryInOwnGraveyardSpecification, IsCreatureInOwnGraveyardSpecification
│                            # HasManaCostAtMostSpecification — "mana value N or less" (Sun Titan)
│                            # HasPermanentPowerBonusSpecification — "if it had a +1/+1 counter on it"
│                            #   (Basri's Lieutenant). A permanent AddModifierAction IS our counter, so this is
│                            #   the faithful question, not a workaround. Excludes UntilEndOfTurn buffs.
│                            # PowerAtLeastSpecification — "power 4 or greater" (Intrepid Hero); effective power
│                            # PowerLessThanSourceSpecification — "power less than this creature's" (Lena)
│                            # IsPlaneswalkerSpecification — a walker on the battlefield. TargetSpecification
│                            #   .PlayersOrCreatures() ("any target") includes it; before it existed NO spell
│                            #   could name a planeswalker at all. See "Planeswalkers".
│                            # HasFlyingSpecification — effective flying; compose with .Not() for Earthquake's
│                            #   "each creature without flying".
│                            # Other specs: IsCreatureSpecification (enforces Shroud/Hexproof AND subtype protection at IsSatisfiedBy level), IsPlayerSpecification, IsSubtypeSpecification,
│                            #              IsControlledByYouSpecification, IsControlledByOpponentSpecification,
│                            #              IsSourceCardSpecification, IsNotSelfSpecification, AlwaysFalseSpecification
│                            # Shroud/Hexproof: enforced in IsCreatureSpecification.IsSatisfiedBy — no other spec changes needed.
│                            #   HasShroud = no one can target (including controller). HasHexproof = opponents can't target (controller can).
│                            # Composites: AndSpecification (zone-first candidate narrowing), OrSpecification, NotSpecification
├── Triggers/                # TriggeredAbilityComponent { Name, Condition, Effects, ActiveInZone (default Battlefield) }, EventTriggerCondition, TriggerCondition
│                            # MaxTriggers (lifetime cap, never reset — this is renown's "if it isn't renowned")
│                            #   and MaxTriggersPerTurn (reset by StartTurnAction) are INDEPENDENT. 0 = unlimited.
│                            #   Collapsing them into one field silently turns renown into a creature that grows
│                            #   every turn. Enforced in CheckStateBasedEffectsAction.EvaluateCardTriggers.
│                            # AndTriggerCondition — fires only when every sub-condition fires; how an
│                            #   "intervening if" clause is expressed without a bespoke type per card.
│                            # OpponentControlsMoreLandsCondition — board question, not an event question;
│                            #   meant to be combined via AndTriggerCondition (Knight of the White Orchid).
│                            # LifeGainedThisTurnCondition { Minimum } — fires on TurnEndedEvent when the
│                            #   controller's MtgPlayer.LifeGainedThisTurn has reached Minimum (Resplendent Angel).
│                            # Effects is a LIST — read it, not Effect. `Effect = ...` is a write-only convenience
│                            #   that appends, kept so single-effect definitions read naturally. Multiple effects
│                            #   resolve in order through one ResolveEffectAction, which is the only way each can
│                            #   carry its own targeting strategy (PipelineAction steps read targets from context
│                            #   keys, so mass "all valid" targeting is unavailable inside a pipeline).
│                            # SpellsCastLastTurnCondition { Minimum, Maximum } — fires at the controller's turn start
│                            #   when MtgGame.SpellsCastLastTurn is in range. The werewolf transform condition.
│                            # ActiveInZone = ZoneType.Graveyard for abilities that fire from the graveyard (e.g. Bloodghast landfall)
│                            # LandsPlayedCondition { Threshold } — fires when LandPlayedEvent.PlayerId == controller AND LandsPlayedTotal >= Threshold; used by Valakut's emblem
│                            # CheckStateBasedEffectsAction scans battlefield, graveyard, AND player emblems; ActiveInZone guards card-based triggers; emblems always fire
├── Turns/                   # BeginGameAction, SetupGameAction, StartTurnAction, EndTurnAction, TurnPhase
├── Zones/                   # Zone, ZoneType
│                            # ZoneTransitionExtensions.MoveCardTracked(cardId, destZoneId) — MoveObject that emits
│                            #   CardEnteredGraveyardEvent / CardLeftGraveyardEvent when the move crosses a graveyard
│                            #   boundary. ALL graveyard-touching moves must use this, not MoveObject, or zone-dependent
│                            #   statics silently stop updating. Already used by DiscardCardsAction, DiscardRandomCardAction,
│                            #   MillAction, DestroyCreatureAction, AttackAction, DealDamageAction, MoveCardToGraveyardAction,
│                            #   MoveCardToExileAction, MoveCardToHandAction, PutIntoBattlefieldAction, ExileAction,
│                            #   CastFromGraveyardAction.
│                            #   It ALSO emits PermanentLeftBattlefieldEvent when the move leaves a battlefield for
│                            #   a non-battlefield zone — see "Leaving the Battlefield" below. Battlefield-to-
│                            #   battlefield is excluded, since that is GainControlAction and the permanent stays in
│                            #   play. Deduped against an LTB already in PendingGameEvents, so the destroy/sacrifice/
│                            #   combat paths that announce it themselves do not fire it twice.
│                            #   It ALSO clears CreatureComponent.Damage on any zone change — marked damage belongs
│                            #   to the permanent, so a bounced or reanimated creature arrives undamaged. Another
│                            #   reason a battlefield move must not use MoveObject directly.
├── MtgGame.cs               # Core game state object (ActivePlayerId, TurnNumber, SpellsCastThisTurn)
├── MtgGameFactory.cs        # Factory: Create() and CreateForTesting()
└── MtgGameStateExtensions.cs # API boundary — BeginGame, ShuffleLibrary, etc.
```

## ImmutableGameObjects Usage

All state changes go through `GameAction.Execute()` — never mutate directly. No delegates, lambdas, or `Func<>` / `Action<>` on any type stored in `GameState`. `MtgGame`, `MtgPlayer`, `Card`, all `GameAction` subclasses, and all `GameComponent` subclasses must remain fully serializable. Pipeline context key constants live in `Actions/ContextKeys.cs`.

## Card Effects Pattern

Cards are `GameObject` subclasses. Effects are `GameAction` subclasses — pure data with execution logic in `Execute()`. No interpreter layer.

- **Simple fixed-value effects** (deal 3 damage, gain 3 life): plain actions, no pipeline needed.
- **Variable effects** where values are only known at resolution time (Dark Confidant, Cruel Edict): use `PipelineAction` to chain steps and pass context.
- **Targeting**: targets are chosen upfront when playing a spell or activating an ability — no `ChoiceAction` goes on the stack for targeting. Only triggered abilities and "choose on resolve" effects use `ChoiceAction` mid-pipeline.
- **Mass effects** (Pyroclasm, Wrath): query game objects directly and spawn individual actions per target.
- **Restriction-based effects** (Smother): enforce restrictions at resolution in `ValidateResolve` or `Execute`.

## P/T Evaluation

`CreatureEvaluator` (`Extensions/CreatureEvaluator.cs`) aggregates P/T and keywords from multiple sources in a single battlefield scan:

1. Base values on `CreatureComponent`
2. `PowerToughnessModifier` components on the card (from spells like Giant Growth)
3. `StaticAbilityComponent` on battlefield permanents controlled by the same player — one scan covers both `StaticPTBoostAbility` and `StaticGrantKeywordAbility`

**Prefer `GetEffectiveStats(state, cardId) → CreatureStats`** when multiple properties are needed — it reads from the card's own components, returning power, toughness, and all keywords (including `HasShroud`, `HasHexproof`) in one O(1) pass. The individual methods (`GetEffectivePower`, `GetEffectiveToughness`, `GetEffectiveHaste`, `GetEffectiveShroud`, `GetEffectiveHexproof`, etc.) delegate to it.

`AttackAction` and `DealDamageAction` must never read base values directly. `AttackAction.ValidateTauntConstraint` calls `GetEffectiveStats` once per creature to cover Taunt, Flying, and Reach checks in a single pass.

**Static ability effects are pre-computed (push model):** `StaticAbilityEngine` stamps `AppliedStaticPTBoost` and `AppliedKeywordComponent` onto affected permanents via `CheckStateBasedEffectsAction` in response to `CreatureEnteredBattlefieldEvent` and `PermanentLeftBattlefieldEvent`. `GetEffectiveStats` reads those applied components directly — no board scan. `MtgGame.StaticSourceIds` caches the set of battlefield permanents with active static abilities for O(k) source lookup.

`GetEffectiveHaste(state, cardId)` returns true if the creature has intrinsic `HasHaste` on `CreatureComponent` OR an `AppliedKeywordComponent{GrantsHaste=true}` is stamped on it. `AttackAction.ValidateAdd` and `MtgActionGenerator` both call this instead of reading `HasHaste` directly.

### PowerToughnessModifier

`PowerToughnessModifier` is an abstract `GameComponent` base with `Duration` (`UntilEndOfTurn` or `Permanent`) and `SourceCardId`. Subclasses implement `GetPowerBonus(GameState, int cardId)` and `GetToughnessBonus(GameState, int cardId)`. `CreatureEvaluator` calls these methods — no type switching.

- `StaticPowerToughnessModifier` — fixed `PowerBonus` / `ToughnessBonus` values. Used by `AddModifierAction` for spells like Giant Growth and Unholy Strength.
- `GraveyardCountComponent` — dynamic modifier; both bonus methods return the card count in the controller's own graveyard. Used by Tarmogoyf (base Power = 0, base Toughness = 1).

Applied via `AddModifierAction`. `UntilEndOfTurn` modifiers are cleared by `StartTurnAction`.

## Mana System

Land-based. Both players start at `MaxMana = 0`, `CurrentMana = 0`. All permanent mana comes from playing land cards.

- **Playing a land** (`PlayLandAction`): `MaxMana++`, `CurrentMana++`, `LandsPlayedThisTurn++`, `LandsPlayedTotal++`. If the card has `GrantEmblemComponent`, adds the emblem to the player before emitting the event (so the emblem is active when the LandPlayedEvent triggers are evaluated). Card moves Hand → Exile. Emits `LandPlayedEvent`.
- **Effect-sourced lands** (`PutLandIntoPlayAction`): same MaxMana/CurrentMana/LandsPlayedTotal increments (and same `GrantEmblemComponent` check), but does NOT increment `LandsPlayedThisTurn` (doesn't consume the land-per-turn). Used by Rampant Growth and Primeval Titan ETB.
- `StartTurnAction` refills `CurrentMana = MaxMana` and resets `LandsPlayedThisTurn = 0`. It does **not** auto-increment `MaxMana`.
- **Land limit**: one land play per turn. Each permanent with `ExtraLandPerTurnComponent` controlled by the player adds +1. Limit is computed dynamically in `PlayLandAction.ValidateAdd` — no stored `LandsAllowedThisTurn` field.
- `CastCreatureAction` and `CastSpellAction` validate sufficient mana in `ValidateAdd` and deduct `ManaCost` in `Execute`.
- **Fast mana** (`AddTemporaryManaAction`) adds to `CurrentMana` only — `MaxMana` is unchanged, so the bonus evaporates at the start of the next turn. Used by Rite of Flame, Seething Song, Lotus Bloom.
- `MtgGameFactory.CreateForTesting()` gives both players `MaxMana = 99` / `CurrentMana = 99` — use in all unit tests not specifically testing the land system.

## Storm Mechanic

`MtgGame.SpellsCastThisTurn` (int) counts every spell cast this turn by either player. It is:
- Incremented by `CastSpellAction.Execute()` and `CastCreatureAction.Execute()` after mana is spent.
- Rolled into `MtgGame.SpellsCastLastTurn` and then reset to 0 by `StartTurnAction.Execute()` at the start of each turn. `SpellsCastLastTurn` is what werewolf transform conditions read — "last turn" means the immediately preceding half-turn, since a turn here is one player's turn.
- Read by `ResolveSpellAction` when `SpellComponent.HasStorm = true`.

`SpellComponent.HasStorm` — when true, `ResolveSpellAction` spawns `Math.Max(SpellsCastThisTurn, 1)` copies of `ResolveEffectAction` instead of one. This gives any spell the storm mechanic for free. The storm count already includes this spell since `CastSpellAction` increments before resolution.

`TryGetGame()` on `GameState` finds the `MtgGame` instance without requiring a known ID. Used by cast actions and `ResolveSpellAction` (storm handling) so they don't need to carry `GameId`.

## Permanent System

Cards are divided into **permanents** (stay on the battlefield after resolving) and **non-permanents** (instants and sorceries, which go to the graveyard). This is modelled via components:

- **`PermanentComponent`** — marker with no data. Every card that enters the battlefield must carry this component. All creature cards in `CardLibrary` already include it. New non-creature permanents (artifacts, enchantments) must also include it.
- **`CreatureComponent`** — combat data (power, toughness, keywords). Independent of `PermanentComponent`. A card can gain or lose creature status mid-game by adding/removing `CreatureComponent` without any zone change.

**Casting routing** in `MtgActionGenerator.AddHandActions`:
1. `HasComponent<CreatureComponent>()` → `CastCreatureAction` (handles summoning sickness setup)
2. `HasComponent<PermanentComponent>()` and not a creature → `CastPermanentAction` → `ResolvePermanentAction` → battlefield
3. Otherwise (`SpellComponent` only) → `CastSpellAction` → resolves and goes to graveyard

**Card type identity** (Artifact, Enchantment, etc.) is stored as strings in `Card.Subtypes` — e.g., `"Artifact"` or `"Enchantment"`. Use `HasSubtype("Artifact")` in targeting specifications. `PermanentComponent` itself carries no type data.

**Events**:
- `CreatureEnteredBattlefieldEvent` — fired by `PutIntoBattlefieldAction` for creatures (cast or cheated in).
- `PermanentEnteredBattlefieldEvent` — fired by `ResolvePermanentAction` for non-creature permanents.
- `PermanentLeftBattlefieldEvent` — fired for all permanents leaving the battlefield (death, exile, sacrifice).

**Factory rule**: Always construct creature cards through `CardLibrary` factory methods. Both `PermanentComponent` and `CreatureComponent` must be present. `CastPermanentAction.ValidateAdd` rejects cards that have `PermanentComponent` but also `CreatureComponent` — and vice versa for `CastCreatureAction`.

## Equipment System

Equipment is an Artifact subtype that stays on the battlefield and can be attached to creatures you control.

**Components on the equipment card:**
- `PermanentComponent` — battlefield marker (same as all permanents)
- `EquipmentComponent` — `PowerBonus`, `ToughnessBonus`, `EquippedToCardId` (0 = unequipped)
- `ActivatedAbilityComponent` — equip activated ability with `ManaCost` and `AttachEquipmentAction` as `ActionTemplate`

**`AttachEquipmentAction` (ITargetedAction):**
- Gets the equipment card ID from `InputContext[ContextKeys.SourceCardId]` (injected by `ResolveEffectAction`)
- Gets the target creature from `TargetIds` (injected by `WithTargets` at resolution time)
- Removes `EquippedBoostComponent(SourceCardId == equipmentId)` from the previously-equipped creature (if re-equipping)
- Updates `EquipmentComponent.EquippedToCardId` on the equipment card
- Stamps `EquippedBoostComponent` onto the target creature — extends `PowerToughnessModifier` so `CreatureEvaluator` picks it up with no changes

**Detach on creature death:** `CheckStateBasedEffectsAction.DetachEquipmentFromLeavingCard` scans both battlefields for equipment whose `EquippedToCardId` matches the leaving card ID, and resets it to 0. The `EquippedBoostComponent` on the creature is left as-is since the creature is leaving anyway.

**`ContextKeys.SourceCardId`:** Added alongside `CastingPlayerId`. `ResolveEffectAction` injects both into the action's `InputContext` before spawning it, enabling any `ITargetedAction` to identify its source card.

## Additional Costs

Beyond mana, cards and abilities can carry `AdditionalCost` entries (on `Card.AdditionalCastCosts` and `ActivatedAbilityComponent.AdditionalCosts`). Two categories:

- **Resource costs** (`LifeAdditionalCost`): validate against player state, no selection needed.
- **Selection costs** (`SacrificeAdditionalCost`, `DiscardAdditionalCost`): player chooses game objects. Payment IDs are carried in `AdditionalCostPayments` on the cast/activate action — same pattern as `TargetIds`.

Mana is always the primary cost (`ManaCost: int`); this matches MTG's "0:" notation for free abilities. Additional costs are paid before mana in `Execute`, before the card moves to the stack.

`MtgActionGenerator` calls `GetValidPayments` and picks the first valid option per selection cost — sufficient for the AI. The console auto-selects the first valid payment. The Godot UI walks the costs one at a time, highlighting valid payments on the battlefield **and in hand**.

`AdditionalCost.Describe()` supplies the player-facing instruction ("Discard a card from your hand"). It is abstract so a new cost type cannot ship without one; presentation layers must never type-switch to build this text.

Selection costs are real zone changes: `DiscardAdditionalCost.Pay` routes through `MoveCardTracked` and stages `CardDiscardedEvent`, so discard payoffs and zone-dependent statics see a cost payment exactly as they see a discard effect.

`SacrificeAdditionalCost.Pay` had the same hole and it went unnoticed far longer. It used a bare
`MoveObject` and announced only `PermanentLeftBattlefieldEvent`, so **a sacrificed creature never
died as far as the trigger feed was concerned**: `OnAnyCreatureDies` and `OnSelfDies` both
no-opped, `CardEnteredGraveyardEvent` never fired so graveyard-active statics stayed unregistered,
and marked damage rode into the graveyard. Sacrifice outlet plus death payoff is an entire
archetype and none of it worked, with nothing erroring. It now routes through `MoveCardTracked`
and stages `CreatureDestroyedEvent` for creatures, matching `DestroyCreatureAction`.

**The general rule both bugs are instances of: a cost payment is a real game event.** Any new
`AdditionalCost` that moves a card must use `MoveCardTracked` and stage the same events the
equivalent *effect* would.

## Activated Abilities

- Modelled as `ActivatedAbilityComponent` on a card. Fields: `Name`, `ManaCost`, `AdditionalCosts`, `CardEffect`, `MaxActivationsPerTurn`, `ActivationCount`.
- A card may have multiple `ActivatedAbilityComponent` instances — one per ability, each independently tracked.
- `MaxActivationsPerTurn` controls how many times the ability may fire per turn. Default = 1 (once per turn). Set to 0 for unlimited (e.g. Arcbound Ravager's sacrifice ability).
- `ActivationCount` tracks uses this turn. Cleared to 0 by `StartTurnAction`.
- `ActivateAbilityAction` validates mana and additional costs, checks `ActivationCount < MaxActivationsPerTurn` (skipped when `MaxActivationsPerTurn == 0`), increments `ActivationCount`, pays all costs, resolves targets, and spawns the effect — using the same `CardEffect` and `TargetingStrategy` infrastructure as spells.
- Tap costs not yet implemented.

## Combat System

Hearthstone-style (turn-based, no blockers). The active player attacks; the opponent does not assign blockers.

**Attacker deduplication**: `AddAttackActions` generates only one representative attack action per (target, `AttackerSignature`) pair. **This is an AI search optimisation and must be off for a human** — it suppresses the duplicate's actions entirely, so a player holding two copies of the same creature finds the second one unclickable. `GetLegalActions` takes `deduplicateAttackers` (default true) and `MtgGameManager` passes false for the human player. `AttackerSignature` captures the fields that determine combat outcome: `Name`, effective `Power`/`Toughness` (from `GetEffectiveStats`), current `Damage`, `HasFlying`, `HasTrample`, `HasDoubleStrike`, `HasLifelink`. Two creatures with identical signatures attacking the same target produce strategically equivalent game states, so only one is offered to the AI. This prevents exponential action-count growth when many identical tokens (e.g. Goblin tokens from Krenko, Mob Boss) are on the battlefield.

- `HasSummoningSickness` — cannot attack the turn they enter the battlefield. Cleared by `HasHaste` on `CreatureComponent` — haste creatures enter with `HasSummoningSickness = false`.
- `HasDoubleStrike` — creature deals damage twice. vs player: two separate damage applications, two `CombatDamageDealtToPlayerEvent`s (triggers fire twice). vs creature: deals 2× power in one pass. **Implies first strike** — ask `CreatureStats.StrikesFirst`, never `HasFirstStrike` alone.
- `HasFirstStrike` — deals combat damage before creatures without it. See "First Strike" below.
- `HasIndestructible` — damage and "destroy" do not kill it. See "Indestructible" below.
- `IsExhausted` — cannot attack, cannot use a `RequiresTap` ability. See "Exhaust" below.
- `HasAttacked` — can only attack once per turn.
- `HasFlying` — **a creature with Flying can only be attacked by a creature with Flying or Reach**, and it bypasses Taunt from non-flying/non-reach creatures. Evaluated via `GetEffectiveFlying()`; the attack restriction lives in `AttackAction.CanReach`.
  - The attack restriction is what makes Flying worth anything. With no blockers there is no evasion to provide, so before it existed Flying's only function was bypassing Taunt — blank whenever the defender had no Taunt creature. Cards costed as though Flying were premium evasion were paying for nothing.
  - **Taunt only compels attacks the attacker could legally make.** Otherwise a Flying Taunt creature would forbid every ground creature from attacking at all: Taunt would compel an attack the Flying rule simultaneously forbids. `ValidateTauntConstraint` filters Taunt creatures through `CanReach` for exactly this reason.
  - Reach is the intended answer and is therefore worth real card text.
- `HasTaunt` — must be attacked before non-taunt targets. Enforced in `AttackAction.ValidateTauntConstraint`. Evaluated via `GetEffectiveTaunt()`.
- `HasReach` — intercepts flying attackers; flying does not bypass Taunt from Reach creatures. Evaluated via `GetEffectiveReach()`.
- `HasDeathtouch` — any nonzero damage this creature deals to another creature is lethal. Evaluated via `GetEffectiveDeathtouch()`; enforced in `CreatureEvaluator.IsLethalDamage`. See "Deathtouch" above.
- `HasLifelink` — when the creature deals combat damage, its controller gains that much life. Applies to damage to players and creatures (including trample excess, which is counted once as part of total power). Defender lifelink also triggers on counter-damage in creature vs creature combat.
- `HasTrample` — when attacking a creature, excess damage beyond the target's effective toughness carries over to the defending player. Applies per strike for double strike.
- All keywords can also be granted by `StaticGrantKeywordAbility` (same pattern as `GrantsHaste`).
- `HasSummoningSickness`, `HasAttacked` on `CreatureComponent` are cleared by `StartTurnAction`. `Damage` persists between turns — creatures can be chipped down across multiple turns. It does **not** survive a zone change: `MoveCardTracked` clears it, so a bounced or reanimated creature comes back undamaged.
- Creature attacks player: deals damage equal to effective Power; emits `CombatDamageDealtToPlayerEvent` (used by Goblin Lackey/Warren Instigator triggers).
- **Combat power is clamped at 0** where `AttackAction` reads it (attacker, defender, and the defender's lifelink amount). Effective power can genuinely go negative — Sensory Deprivation is -3/-0 — and a raw negative ran straight through the subtraction at every damage site: it healed the defending player, healed marked damage off the defending creature, and drained a lifelinker's controller. `DealDamageAction` needed no change; it already ignores amounts `<= 0`, which is what covers Fight.
- Creature attacks creature: both deal damage simultaneously. Dies if `Damage >= effective Toughness`; moves to owner's graveyard.

## Card Creation

`CreateCardAction` creates new `Card` objects and places them on a player's battlefield by spawning one `PutIntoBattlefieldAction` per card. The ETB ceremony is handled entirely by `PutIntoBattlefieldAction`, which is the single entry point for all battlefield placement regardless of whether a card is moving from another zone or being created fresh.

- `ControllerId`: if 0, reads from `InputContext[CastingPlayerId]`.
- `CountInputKey`: if set, reads the count from pipeline context (used by Krenko, Mob Boss via `CountCardsWithSubtypeAction`).
- `HasSummoningSickness` is stamped by `PutIntoBattlefieldAction.ApplyEtbCeremony` based on `HasHaste`.

## Game Startup

`MtgGameStateExtensions.BeginGame(state, gameId, player1Id, player2Id)` is the **single entry point** for all presentation layers.

- `BeginGame` dispatches `BeginGameAction` → spawns `SetupGameAction` → spawns `StartTurnAction` for Player 1 with `SkipDraw = true`.
- `SetupGameAction` shuffles both libraries (Fisher-Yates via `ShuffleLibrary` on `MtgGameStateExtensions`) and deals 7-card opening hands to both players.
- Presentation layers never construct `BeginGameAction`, `SetupGameAction`, or `StartTurnAction` directly. Turn transitions (`EndTurnAction` spawning `StartTurnAction`) are handled entirely within MtgCore.

## Turn Structure

Files: `Turns/BeginGameAction.cs`, `SetupGameAction.cs`, `StartTurnAction.cs`, `EndTurnAction.cs`, `TurnPhase.cs`

- **`StartTurnAction`**: refills `CurrentMana = MaxMana` (does NOT auto-increment MaxMana — mana comes from lands), resets `LandsPlayedThisTurn = 0`, optionally draws (`SkipDraw` flag), clears per-turn flags on all permanents the active player controls (`HasSummoningSickness`, `HasAttacked` on `CreatureComponent`; `HasActivated` on `ActivatedAbilityComponent`; `UntilEndOfTurn` P/T modifiers). `Damage` is NOT reset — it persists across turns.
- **`EndTurnAction`**: switches `ActivePlayerId`, increments `TurnNumber` when Player 2 ends their turn, spawns `StartTurnAction` for the next player.
- `MtgGame` tracks `ActivePlayerId` and `TurnNumber`. Phases within a turn are not yet modelled — the turn is a single phase.
- Win/loss conditions checked by `CheckStateBasedEffectsAction` as the `PostActionProcessor` after every action (life ≤ 0, empty library).

## Presentation Layer Rules

- `MtgConsole` and `MtgSimulator` never construct game actions directly for game flow — use `MtgGameStateExtensions` methods as the API boundary.
- Presentation layers never modify game state directly. All state changes go through `GameAction`s. Exceptions: explicit test setup and debug/cheat tooling (both must be clearly commented as such).
- `MtgActionGenerator.GetLegalActions(state, ids, playerId)` is the **single shared source** of legal action generation. Console and simulator both call this — never duplicate this logic.
- A UI that highlights legal attack targets must ask `AttackAction.ValidateAdd` per candidate (`MtgGameManager.GetLegalAttackTargets` does this) rather than re-deriving Taunt/Flying/Reach. A second copy of those rules in the presentation layer would drift, and the symptom is a click that silently does nothing.
- **The same rule applies to CONSTRUCTING an action, not just to validating one.** `MtgGameManager`
  hand-built `CastPermanentAction` with no `TargetIds` and `CastFromGraveyardAction` with no
  `AdditionalCostPayments`, so a human could not play **any Aura** and could not recur Despoiler
  of Souls — while the AI did both perfectly, because it goes through `MtgActionGenerator`, which
  enumerates aura targets and works out cost payments. **"The AI can do it and I can't" is the
  signature of this bug.** `MtgGameManager.PaymentsFor<T>` now reads the payments back off the
  generator rather than recomputing them.
- A UI that prints a card's mana cost **in hand** must ask `CostEngine.ComputeEffectiveCost`, never `Card.ManaCost`. The cast actions all pay the effective cost, so a printed cost disagrees with what the game charges — Stormwing Entity read 5 in hand while costing 2, and the hand also greyed it out as unaffordable. On the battlefield the printed cost is correct: nothing is being paid.
- `CastSpellAction.TargetIds` / `CastFromGraveyardAction.TargetIds` are keyed by **effect index**, not by 0. `ValidateAdd` looks targets up under the index of the effect that needs them, so keying them anywhere else makes the spell silently uncastable rather than throwing.
- `MtgGameFactory.CreateForTesting()` gives both players `MaxMana = 99` / `CurrentMana = 99`. Use in all unit tests not specifically testing the land/mana system. Use `MtgGameFactory.Create()` with manual land plays for land-specific tests.
- The simulator detects potential infinite loops via per-turn action counts (warning at 50, cutoff at 100) and flags unusual games.

## Debugging / Error Handling

When a logic error is found: write an NUnit test to isolate it first. Do not assume the cause — verify the assumption before proceeding.

## Deferred / Future Work

These are designed but not yet implemented. Do not re-implement or work around these planned patterns:

| Step | Feature | Notes |
|------|---------|-------|
| 4 | Keyword abilities as components | Lifelink, Deathtouch, Trample etc. as individual components checked by relevant actions. Rules-engine keywords that do not use the stack. All keywords are currently flags on `CreatureComponent` — migrate when the full keyword system is built. Note the six-site rule under "First Strike" until then. |
| — | Vigilance | Deliberately unimplemented, not merely missing — with no blocking it has nothing to do. Eight Core Set Cube cards are printed with it and go without. Two costed options in `DesignNotes.md`; `IsExhausted` was kept separate from `HasAttacked` so either stays cheap. |
| — | Structural replacement effects | `ReplacementModifierComponent` covers numeric replacement only. "Enters tapped" / "exile it instead" needs an action-rewrite hook in the `ImmutableGameObjects` action loop. Not built speculatively — see `DesignNotes.md`. |
| — | Colour | Cards have no colour at all, so protection-from-a-colour, "black or red" targeting, and multicolour matter are all unreachable. Protection from a creature TYPE is implemented. |
| — | Conditional static abilities | `StaticAbilityEngine` is a push model that only re-stamps on ETB/LTB, so an anthem gated on a changing value (Path of Bravery's "while your life is at or above your starting total") would go stale the moment anyone took damage. Live-evaluated `PowerToughnessModifier`s dodge this for a single creature; a conditional TEAM anthem has no equivalent yet. |
| — | Card-type counting | `CardType` now exists, so Delirium is finally expressible — nothing counts distinct types in a graveyard yet, but the blocker is gone. |
| — | Double-faced planeswalkers | Kytheon's flip needs `TransformComponent` to swap a creature into a planeswalker, which crosses the creature/permanent routing split. |
| — | Delirium | Needs "N+ card types in your graveyard". There is no card-type system — Artifact/Enchantment/Land are strings in `Subtypes` and instants/sorceries carry no type marker at all. Deliberately cut in favour of Threshold, which covers the same design space at zero cost. |
| — | Real Madness | Casting a discarded card requires a priority window; the engine has a stack but no priority. Modelled instead as a graveyard-active `CardDiscardedEvent` trigger — see "Discard Triggers" below. |
| — | Deathtouch from effect damage | `DealDamageAction` always passes `fromDeathtouch: false`. Only combat can deal deathtouch damage today. Add a `SourceHasDeathtouch` field when a card needs a deathtouch ping ability. |

**Completed since this table was written:** Step 3 (zone-dependent statics). Also, from the Core
Set Cube white pass: tap costs (see "Exhaust" — `RequiresTap` now exhausts), first strike,
indestructible, exalted, subtype protection, numeric replacement effects, activation conditions,
and cast restrictions.

**+1/+1 counters are a "won't do", not a "not yet".** A permanent `AddModifierAction` *is* the
counter. `HasPermanentPowerBonusSpecification` answers "did it have a counter on it". A dedicated
counter system only becomes necessary for a card that counts counters ("for each +1/+1 counter"),
and no card yet does.

## Impulse Draw

"Exile the top card of your library. You may play it this turn" — Abbot of Keral Keep, Chandra
Pyromaster, Chandra Heart of Fire, Glint-Horn Buccaneer, Soul of Shandalar.

`ExileTopCardPlayableAction` moves the card to exile and stamps `ExiledPlayableComponent`.
`EndTurnAction` strips the marker from the **ending** player's exile zone — not the starting
player's, for the same reason the `UntilEndOfTurn` replacement cleanup already lives there rather
than in `StartTurnAction`, which only touches the active player.

**`GameState.IsInCastableZone(cardId, playerId)` is the single "can you play this from where it
is" check**, and all four play actions call it — the three cast actions plus `PlayLandAction`.
Each previously hardcoded `zone == your hand`. Adding a fifth copy of that rule per action is how
the "the AI can do it and I can't" class of bug gets made; one predicate cannot disagree with
itself. `MtgActionGenerator` walks the exile zone for marked cards and routes them through the same
`AddCastableCardAction` a hand card takes, so an impulse-drawn land, creature, spell or aura all
behave exactly as they would from hand.

The marker is deliberately **per card, not per player**: an unrelated card already sitting in exile
must not become playable because you impulse-drew something else.

## Red Section Mechanics (Core Set Cube)

- **Damage to planeswalkers did not work at all.** See "Planeswalkers" — two independent holes,
  both silent, both predating red.
- **Divided damage is sprayed, not split.** "Deals N damage divided as you choose among any number
  of targets" (Cone of Flame, Flames of the Firebrand, Chandra's Outrage, Thundermaw Hellkite,
  Inferno Titan, Drakuseth) has no home here: targeting is single-target or all-valid, and there is
  no shape for "pick K targets and apportion N among them". Modelled as N independent 1-damage
  effects each with `Random()` targeting — Hearthstone's Arcane Missiles. **These cards cost one
  less than printed** to pay for the loss of aim. This needed no engine code: a card already
  carries a list of effects and each resolves its own targeting.
- **`DiscardAdditionalCost.Filter`** gives Magmatic Insight and Molten Vortex their real
  "discard a land card" cost. Faithful rather than reskinned — a land IS a card in hand here, and
  only becomes `MaxMana` when played.
- **`HasFlyingSpecification`** for Earthquake's "each creature without flying".
- **`CreatureCountComponent.Subtype`** — "+2/+0 for each other Goblin you control" (Goblin
  Piledriver, Goblin Rabblemaster). A field on the existing component rather than a parallel type:
  the counting, controller check, self-exclusion and live-evaluation rationale are identical, and a
  second copy is one more place to forget `Duration = Permanent`.
- **`CreatureDamagedEvent` never reached `PendingGameEvents`** — from both `DealDamageAction` and
  `AttackAction — so **no "whenever this creature is dealt damage" trigger had ever fired**. The
  fifth instance of that bug and the best disguised: the event already had an `EventTypeNames`
  constant, an `ExtractSubjectId` entry AND `TriggerAmountOf` support, so every downstream piece
  was ready for a trigger that could never arrive. Brash Taunter reflected nothing.

**Rules text caught four more silent failures, none visible from the card definitions.** Dumping
every red card's rendered text (the `MtgCardMapper` pass the "rules text is not cosmetic" rule
demands) found: Boggart Brute rendering a completely blank text box once menace was cut;
`ExileTopCardPlayableAction` missing from the mapper entirely, blanking both impulse-draw cards; a
context-driven `DealDamageAction` printing **"Deal 0 damage"** on Brash Taunter and Volley Veteran,
in two separate switches; `SpellCast` hardcoded to "Whenever you cast a spell", so Scab-Clan
Berserker described the opposite of the card it is. **Confidently wrong text is worse than blank
text — nothing looks broken.**

**Ogre Battledriver is the reason `ContextKeys.TriggerSubjectId` exists.** "Whenever another
creature you control enters, IT gets +2/+0 and haste" must land on the creature that entered; a
targeting strategy cannot see the event, so `Random()` buffs some other creature and the new
arrival misses the haste that is the card's whole point. Same failure as Wall of Frost.

### "Can't be countered" is kept, unlike most "can't be X" clauses

`CannotBeCounteredComponent`, checked by `CounterTrapEngine.TryCounterCast` **before a trap is
chosen**, so an uncounterable spell does not even SPEND the opponent's counterspell — a trap that
cannot counter its target was never a legal response to it.

This is worth implementing rather than cutting precisely because counterspells genuinely exist
here (blue's traps fire from hand off unspent mana), so the clause protects against something real
rather than describing a mechanic the game lacks. `Condition` reuses `ActivationCondition`, so
Exquisite Firecraft's spell mastery works with no new type.

**Banefire's is unconditional rather than "if X is 5 or more".** The chosen X lives on
`CastSpellAction`, not on the card — that is what lets two copies be cast for different X — so a
component on the card cannot see it. Threading `XValue` into the counter engine for one clause on
one card is not worth it.

### The rules-text pass found three more, all in the second half

`Earthquake` rendered **"deal X damage to each creature"** — the flying exemption is the entire
card, and `DescribeSpecification`'s walker had no `NotSpecification` case, so the wrapper was
walked straight past. `DiscardAdditionalCost`'s filter was ignored, so "discard a land card" read
as "discard a card" — understating it (only a land will do) and overstating it (a landless hand
cannot pay) at the same time. And `CannotBeCounteredComponent` rendered nothing at all.

**Two rules-text passes, eight silent failures, zero of them visible from the card definitions or
catchable by a cast-and-resolve test.** Dump and read the rendered text of every new card.

**Cut clauses, all with existing precedent.** Menace and "can't block"/"can't be blocked" (no
blocking — Boggart Brute, Frenzied Goblin, Goblin Glory Chaser, Stormblood Berserker); colour-based
targeting (Fry); Chandra, Fire of Kaladesh's flip to a planeswalker (as Kytheon, Jace and Liliana);
"if it would die, exile it instead" (Scorching Dragonfire — structural replacement, see
`DesignNotes.md`); "attacks each turn if able" (Borderland Marauder, Goblin Rabblemaster — no
forced-attack concept, and dropping it only ever helps the player, like vigilance).

**Goblin count-based buffs read the BOARD, not the attack.** Goblin Piledriver and Goblin
Rabblemaster are printed as "for each other attacking Goblin"; attacking is a fleeting state here
(one attack per turn, resolving immediately), so they count Goblins you control instead. Costed
down accordingly, since not having to commit the attack is a real upgrade.

## Black Section Mechanics (Core Set Cube)

**Four bugs were found building this, all of the same shape: a card that builds, casts and
resolves without erroring while doing nothing, or doing the wrong amount.** None were visible
without a test that asserted the consequence.

1. **`SacrificeAdditionalCost` never announced a death** — see "Additional Costs" above.
2. **`TriggerConditions.OnYourUpkeep()` had no filter.** `TurnStartedEvent`'s subject is the
   player whose turn began, so every upkeep trigger in the engine fired on **both** turns, at
   double the printed rate. Six existing white/blue/Hollowmere cards were affected.
   `OnOpponentUpkeep()` is its counterpart (Stab Wound).
3. **`DrawCardsAction` ignored `AmountContextKey`.** It looped on the raw `Amount` field, so any
   context-driven draw silently drew the default 1.
4. **`MtgActionGenerator.BuildAdditionalCostPayments` only ever offered one payment**, while
   `Validate` demands an exact count — so a cost of 2 made the card permanently uncastable with
   no error. `AdditionalCost.RequiredPaymentCount` fixes it; **any selection cost with a `Count`
   must override it.**

### What was added

- **`ContextKeys.TriggerAmount`** — the numeric payload of the event that fired a trigger,
  injected by `ResolveEffectAction` from `CheckStateBasedEffectsAction.TriggerAmountOf`. This is
  what makes "whenever you lose life, draw **that many** cards" (Vilis) expressible; every such
  clause had previously been flattened to a constant. Read it with `EffectAction.AmountContextKey`.
  `TriggerAmountOf` is a deliberately closed list of events — an event that gains an unrelated
  numeric field later must not start silently feeding these effects.
- **`MtgPlayer.LifeLostThisTurn`** — mirror of `LifeGainedThisTurn`, accumulating in
  `LoseLifeAction`, `DrainLifeAction` and the player-damage path of `DealDamageAction`.
  **It resets for BOTH players in `StartTurnAction`, unlike every other per-turn counter.** Life
  loss overwhelmingly happens to the non-active player, so an active-player-only reset would let
  the defender's tally span two turns. Read by `LifeLostThisTurnCondition` (which asks about ANY
  player by default) and `OpponentLostLifeThisTurnCondition` (bloodthirst).
- **`SetLifeTotalAction`** — absolute set, deliberately NOT routed through `ReplacementEngine`
  (a life-gain bonus must not turn "becomes 10" into "becomes 11"). **`Amount = 0` is how "you
  lose the game" is expressed**: the state-based loss check already owns `HasLost`,
  `PlayerLostEvent` and winner determination, so there is no second way to lose.
- **`ChosenModesComponent` + `SelectModeAction.ExcludeAlreadyChosen` +
  `ApplyChosenModeAction.RecordChoice`** — "choose one that hasn't been chosen" (Demonic Pact).
  State lives on the CARD because the exclusion must survive between resolutions turn after
  turn; `ActivationCount` is reset every turn and pipeline context dies with the resolution.
  The two flags must move together, so `WithModes(onceEach: true, …)` sets both.
- **`FlashbackComponent.AdditionalCosts`** + **`ExileFromGraveyardAdditionalCost`** — a
  repeatable graveyard recursion bounded only by mana just returns forever; a cost that eats the
  graveyard gives it a hard floor and makes graveyard hate live. Paid **before** the card leaves
  the graveyard, so it cannot select the card paying for itself.
- **`WithSymmetricEdict(excludeSubtype)`** — "each player sacrifices a creature". Takes the
  **cheapest** on each side, not the biggest: a real edict lets each player choose and each keeps
  their bomb, so taking the biggest would make a symmetric effect one-sided.
- **`WithTutor()`** with no subtype, backed by `SelectCardFromLibraryAction.SelectBestByManaCost`.
  Library order is random, so a first-match search with no subtype is just "draw the top card" —
  Grim Tutor would have been a strictly worse Sign in Blood.
- **`WithChosenDiscard(n)`** — "you choose a card" via most-expensive-non-land, as distinct from
  `WithOpponentDiscard`'s random. The gap is real: random discard off a full hand is a coin flip.
- **`WithDrain(n)`** — hand-rolled ~10× in Hollowmere before this. Must be `DrainLifeAction`
  rather than `WithLoseLife` + `WithLifeGain`, because inside a trigger `NoTarget()` overwrites
  hardcoded `TargetIds` and the loss half hits nobody.
- **`WithReanimate(fromAnyGraveyard: true)`** / `IsCreatureInAnyGraveyardSpecification`,
  `CreatureCostBuilder.PayLife(n)` / `SpellCardBuilder.WithLifeCost(n)`,
  `HasAttackedThisTurnSpecification`, `DifferentPowerAndToughnessSpecification`, and the trigger
  helpers `OnCreatureYouControlDies(subtype)`, `OnOpponentCreatureDies()`,
  `OnOpponentCreatureEnters()`, `OnCreatureAttacksYou()`, `OnCardDiscarded()`.

**Dark Tutelage needed no new code** — `RevealTopCardAction` already outputs
`ContextKeys.RevealedCardManaCost` (built for Dark Confidant) and `LoseLifeAction` already reads
`AmountContextKey`.

## Battlefield State Does Not Follow the Card

`MoveCardTracked` strips the four components an EFFECT stamps onto a permanent whenever the card
leaves the battlefield: `AppliedStaticPTBoost`, `AppliedKeywordComponent`,
`StaticPowerToughnessModifier`, `EquippedBoostComponent`. Marked damage was already cleared here
for exactly the same reason — it belonged to the permanent, not to the card.

Nothing removed them before, and `StaticAbilityEngine.ProcessPermanentLeft` only maintained the
SOURCE's index. Three separate QA reports were one bug:

- A creature bounced under Glorious Anthem kept the +1/+1 in hand and **collected a second one**
  when it was replayed.
- A creature killed by -4/-4 sat in the graveyard still printing the -4/-4 in its text box.
- A bounced creature "went to the graveyard for no reason" — replaying it put a creature with
  negative toughness onto the battlefield and the zero-toughness check killed it on arrival.

**The strip is a closed list of four types, not "every `PowerToughnessModifier`."** Several
subclasses are PRINTED on the card and define what it is — `GraveyardCountComponent` (Tarmogoyf),
`ThresholdComponent`, `CreatureCountComponent`, `LifeTotalComponent`, `LandsPlayedCountComponent`.
Stripping by base type would delete the card's own rules text on the way to the graveyard and
reanimate it as a vanilla creature.

## Reading the Triggering Event

Two context keys carry the event that fired a trigger into its effect, both injected by
`ResolveEffectAction` and populated in `CheckStateBasedEffectsAction`:

- `ContextKeys.TriggerAmount` — the event's number ("draw THAT MANY cards").
- `ContextKeys.TriggerSubjectId` — the card or player the event was ABOUT ("tap THAT CREATURE").
  Taken from `EventTriggerCondition.ExtractSubjectId`, the same extraction a trigger's `Filter`
  runs against, so what a trigger filters on and what its effect acts on cannot disagree.

Without the subject key a trigger could only act on a target chosen by a targeting strategy, and a
strategy cannot see the event. That is why **Wall of Frost froze every creature an opponent
controlled whenever anything attacked at all** — it had no way to name the creature that attacked
it. `CreatureAttackedEvent` also gained `TargetId` (what was attacked) for the same card;
`AttackedThisCardCondition` is the "whenever a creature attacks this" trigger built on it.

## Zone-Dependent Statics

A `StaticAbilityComponent` applies only while its source card sits in `ActiveInZone`
(default `Battlefield`). Setting it to `Graveyard` gives Wonder-style effects:
"while this is in your graveyard, creatures you control have Flying."

- `StaticAbilityEngine.ProcessZoneSourceEntered` / `ProcessZoneSourceLeft` register and
  unregister graveyard sources in `MtgGame.StaticSourceIds` — the same set battlefield
  sources use, so no type change was needed.
- `CheckStateBasedEffectsAction.ProcessStaticAbilityUpdates` runs graveyard events in a
  **second pass**, after the battlefield pass. This ordering matters: a permanent that dies
  must have its battlefield statics stripped before its graveyard statics are stamped, or
  the strip undoes the stamp.
- `ApplySourceToTarget` checks each ability's `ActiveInZone` against the source card's
  *actual current zone*, so one card can carry both a battlefield static and a graveyard
  static with exactly one live at a time.
- The events that drive all of this come from `MoveCardTracked`, not from the individual
  actions — see the `Zones/` entry in the source map.

## First Strike

`CreatureComponent.HasFirstStrike`, plus `Grants*` on all four keyword-grant types. Read it as
`CreatureStats.StrikesFirst`, which folds in double strike — asking `HasFirstStrike` alone means a
double striker trades evenly with a creature it should kill outright.

Combat lives in `AttackAction.ApplyCreatureVsCreature`. Damage is normally simultaneous; first
strike breaks that. The striking side's damage lands first, and **if it kills the other creature,
no damage comes back**. Symmetric — a defending first-striker punishes the attacker the same way.
If both sides strike first, neither gains anything and damage is simultaneous again.

Balance note: with no blockers this is a premium keyword. Every attack into a creature it can kill
is a free trade, which is much stronger than in real MTG where the defender chooses the fight.

**`HasDoubleStrike` was half-implemented before this.** It sat on `CreatureComponent` but was
absent from `CreatureStats` and all three `Grants*` lists, and `AttackAction` read the raw
component — so it could never be granted and ignored keyword grants. Both are now complete. If you
add another combat keyword, mirror all six sites (`CreatureComponent`, `CreatureStats`,
`StaticGrantKeywordAbility`, `AppliedKeywordComponent`, `ThresholdComponent`, `GrantKeywordAction`)
or you will reproduce the same hole.

## Indestructible

`CreatureComponent.HasIndestructible`, plus the four `Grants*` lists.

Enforced in exactly two places: `CreatureEvaluator.IsLethalDamage` returns false for it (which
covers combat AND effect damage in one edit, per that method's single-lethality-rule invariant),
and `DestroyCreatureAction` skips it. Deathtouch does not beat it — `IsLethalDamage` checks
indestructible first, matching the real rule.

**Zero effective toughness still kills it.** `CheckStateBasedEffectsAction.DestroyZeroToughnessCreatures`
is deliberately unchanged: indestructible answers damage and destruction, not a `-X/-X` shrink.

## Exhaust

`CreatureComponent.IsExhausted` — this engine's tapped state. **All player-facing text says
"exhaust", never "tap"** — the engine has no tapping, and printing MTG's word for a mechanic this
game does not have invites the player to expect untap steps, vigilance and mana abilities that do
not exist. `MtgCardMapper` is the only place that wording lives. An exhausted creature cannot attack
(`AttackAction.ValidateAttacker`) and cannot activate a `RequiresTap` ability
(`ActivatedAbilityAction`). Set by `ExhaustCreatureAction` and by paying a tap cost. Cleared by
`StartTurnAction` **for the active player only** — that is the untap step, and it is what makes
exhausting an opponent's creature on your turn cost them exactly one attack.

**`IsExhausted` is deliberately SEPARATE from `HasAttacked`.** Attacking does not set it, so
nothing here decides the open vigilance question. Merging the two is a small refactor if that is
the decision — see `DesignNotes.md`.

Before this existed, `ActivatedAbilityComponent.RequiresTap` only blocked activation under
summoning sickness; the ability was effectively free to repeat within a turn. It now costs the tap.

`CreatureExhaustedEvent` goes into `PendingGameEvents` so tapper payoffs (Gideon's Avenger) fire.
`ExhaustCreatureAction` no-ops on an already-exhausted creature so payoffs cannot double-count.

## Exalted

`ExaltedComponent { Count }` on the creature, plus `GrantsExalted` on `StaticGrantKeywordAbility`
and `AppliedKeywordComponent`.

Counted, not merely tested: Sublime Archangel grants exalted to every other creature you control
and each instance triggers separately, so a boolean would lose the scaling.

Resolved in `AttackAction.CountExaltedIfAttackingAlone`, **before** `HasAttacked` is set on the
attacker — "attacks alone" means no OTHER creature its controller owns has attacked this turn.
The bonus is stamped as an `UntilEndOfTurn` `StaticPowerToughnessModifier`, added not replaced, so
a combat trick already on the creature survives.

## Replacement Effects

`ReplacementModifierComponent` (abstract) + the `ReplaceableEvent` enum. Modifies the AMOUNT of an
event before it happens: "if you would gain life, gain that much plus 1" (Angel of Vitality).

Same shape as `PowerToughnessModifier` and `TriggerCondition` — abstract serializable record,
virtual method, scanned live at the point of use by `ReplacementEngine.ApplyReplacements`. No
delegates, no `ImmutableGameObjects` change.

**It is a replacement, not a trigger, and that is the whole point.** A trigger that gains life in
response to gaining life is an infinite loop. Because the modifier applies *inside* the originating
action, exactly one event is emitted, carrying the already-modified amount, and nothing can feed
itself. `CoresetCubeWhiteTests` asserts Angel of Vitality has no `TriggeredAbilityComponent`.

Ordering rule: **all multipliers apply first, then all additions**, clamped at 0. Real MTG lets the
affected player choose; a deterministic engine must fix one order, and this one stops a doubler
from also doubling someone else's flat bonus.

Call sites: `GainLifeAction`, `LoseLifeAction`, `DrainLifeAction`, and both damage paths in
`DealDamageAction`. Adding a new replaceable event is an enum value plus one line at the action.

**Scope:** numeric only. Structural replacement ("enters tapped", "if it would die, exile it
instead") rewrites an action rather than a number and is not covered — see `DesignNotes.md`.

## Protection

`ProtectionFromSubtypeComponent { Subtypes }`. Protection from a **colour is impossible** — cards
have no colour in this engine at all. Protection from a creature **type** is what exists.

Two of MTG's four clauses apply: cannot be targeted by a source of that type (enforced in
`IsCreatureSpecification.IsSatisfiedBy`, beside Shroud and Hexproof) and cannot be dealt damage by
one (`AttackAction.ApplyDamageToCreature`, `DealDamageAction.ApplyToCreature`). "Can't be blocked"
needs blocking; "can't be enchanted or equipped" waits for a card that needs it.

`GameState.IsProtectedFrom(cardId, sourceCardId)` is the single entry point so targeting and damage
cannot disagree about what protection means.

## Events That Must Reach PendingGameEvents

`ActionResult.Events` is the caller-visible log. `GameState.PendingGameEvents` is the **trigger
feed**. Adding an event only to the former is silently inert — the trigger never fires and nothing
errors.

This bug has now been found **four** separate times: `CardDiscardedEvent`, then
`PlayerGainedLifeEvent` (so *no* "whenever you gain life" trigger had ever fired), then
`TurnEndedEvent` (so no end-of-turn trigger could fire), then `CardDrawnEvent` (so no "whenever
you draw a card" trigger had ever fired — Teferi's Tutelage never milled anything). All four are
fixed. **Any new action that emits an event a card might trigger on must add it to both.**

`SetupGameAction` is the one deliberate exception: it emits `CardDrawnEvent` for the opening hand
and does NOT stage it, because dealing an opening hand is not drawing.

A grep that finds the next instance: an action file containing `new …Event` but no mention of
`PendingGameEvents` is either inert or deliberate, and nothing else.

A second, quieter version of the same failure: an event with no `EventTypeNames` constant, or no
entry in `EventTriggerCondition.ExtractSubjectId`, cannot be filtered even though it fires.
`PermanentLeftBattlefieldEvent` had neither until Oblivion Ring needed it. **Adding an event means
three places: the record, the constant, and `ExtractSubjectId`.**

## Counterspell Traps

This engine has a stack but **no priority** — the non-active player never acts during your turn —
so a counterspell cannot be cast in response to anything. Rather than bolt on a priority system,
a counterspell is a **trap that fires from hand**.

`CounterTrapComponent` on the card; `CounterTrapEngine.TryCounterCast` does the work, called from
**all three cast actions** after the card reaches the stack and mana is paid, **before** the
resolve action is spawned.

Firing rule, in order:
1. Only the **non-active** player's hand is scanned, so your own traps never hit your own spells.
2. Their hand is walked in **zone order** — insertion order, i.e. draw order. That is the
   documented "first card in your hand wins" tiebreak when two traps could both fire.
3. First card whose `TargetTypes`/`ExcludeTypes` match and whose `ManaCost <= CurrentMana`.
4. The trap's cost is paid and the trap goes to the graveyard — **whether or not the counter
   sticks**, exactly as a real counterspell that resolves and does nothing still gets used up.
5. `ManaTax` ("unless its controller pays {3}") is auto-paid by the caster if they can afford it.
   Neither side chooses, which keeps the mechanic symmetric and deterministic.

**Leaving mana up already works with no engine change.** `StartTurnAction` refills `CurrentMana`
only for the active player, so a non-active player's unspent mana carries into your turn. That
unspent mana is exactly the resource this mechanic costs.

**Ordering matters and is deliberate**: the trap fires *after* `SpellCastEvent` and the
`SpellsCastThisTurn` increment, because a countered spell was still cast. Prowess and storm see it,
matching the real rule. `CounterTrapTests.CounteredSpell_StillCountsAsCast` locks this in.

A trap has a `SpellComponent` with no effects, so **both** `CastSpellAction.ValidateAdd` and
`MtgActionGenerator.AddHandActions` must refuse to offer it — otherwise it is a blank spell at full
price and the AI will happily cast it.

`TaxAllRemaining` handles Clash of Wills' `{X}`: a trap is never cast, so there is no moment to
choose X, and the tax becomes whatever the trapper had left after paying for the trap.

## Freeze

`CreatureComponent.FrozenTurns` and `FrozenBySourceId`, layered on the Exhaust mechanic.

- `FrozenTurns` — extra untap steps to sit out. `StartTurnAction` decrements it and keeps
  `IsExhausted` set while it is above zero. 1 is "doesn't untap during its controller's next
  untap step"; 0 is a plain tapper.
- `FrozenBySourceId` — an indefinite lock tied to another permanent (Dungeon Geists,
  Claustrophobia). A turn count cannot express "for as long as you control this".
  `CheckStateBasedEffectsAction.ReleaseFreezeFromLeavingCard` clears it when the source leaves,
  next to the equipment-detach pass. The creature stays exhausted until its own next untap step —
  killing the source frees it, it does not immediately untap it.

`ExhaustCreatureAction` gained `FreezeTurns` and `FreezeWhileSourceRemains`;
`SpellCardBuilder.WithFreeze(turns, whileSourceRemains)`.

## Walls and Conditional Taunt

Walls are pure blockers and this engine has no blocking, so **Taunt is their substitute** — it
forces attackers through them, which is what a blocker does.

That breaks for Fog Bank, which also prevents all combat damage to itself: a damage-immune Taunt
creature is an unremovable roadblock that every attack is compelled into forever, and with no way
to go wide the opponent has no answer at all.

`TauntUntilAttackedComponent` fixes it. `CreatureComponent.WasAttackedThisTurn` is stamped by
`AttackAction` on the **target** (before damage, so a wall that dies to the attack still counts as
having soaked one) and cleared by `StartTurnAction`. `GetEffectiveStats` suppresses Taunt once both
are true, so Fog Bank absorbs exactly one attack per turn and then steps aside.

`PreventsCombatDamageComponent` is a marker on the creature, checked in
`AttackAction.ApplyDamageToCreature` in **both** directions. Deliberately does not stop effect
damage — Fog Bank still dies to a burn spell, which is what keeps it answerable.

## Clone

`CopyOnEnterComponent`, applied by `PutIntoBattlefieldAction`'s ETB ceremony — the single path
every creature takes onto the battlefield, so a cast Clone and a reanimated one behave alike.

The copy takes the target's `Name`, `Components`, `Subtypes` and `Types` but keeps its own `Id`,
`OwnerId`, `ControllerId` and `ManaCost`: a Clone of the opponent's creature is still yours.
`CopyOnEnterComponent` itself is stripped, so a reanimated Clone does not re-copy.

**Which creature is copied is decided by the engine** — highest effective power, ties broken on
lowest id for determinism. A real `ChoiceAction` would be more faithful but would have to pause the
pipeline mid-ETB, and "copy the biggest thing" is what the choice almost always is.

## Extra Turns

`MtgGame.ExtraTurnsQueued`. `EndTurnAction` spends one instead of passing play: the same player
starts again and `TurnNumber` does **not** advance, because no round completed.

`TakeExtraTurnAction.MaxQueued` is a hard cap of 2. The simulator warns at 50 actions per turn and
cuts a game off at 100; without a cap an AI that rates extra turns highly could chain them until it
trips that, and a game lost to the loop detector is indistinguishable from a bug.

## Gain Control

`GainControlAction` sets `ControllerId` **and physically moves the card** to the new controller's
battlefield. The move is not optional: every battlefield scan in the engine works from the zone,
so a stolen permanent left in place would be invisible to its new controller's anthems, targeting
and attack generation.

`OwnerId` is untouched — that is what `ControlsStolenPermanentsCondition` counts, and it is where
the card returns when it dies. The stolen creature is stamped summoning-sick so it cannot be stolen
and swung with on the same turn.

## Card Types

`CardType` is a `[Flags]` enum; `Card.Types` holds what a card declares and `Card.EffectiveTypes`
falls back to deriving them. **Always ask `card.HasType(...)`, never `Types` directly.**

Before this, type lived in two unrelated places — `CreatureComponent` meant "creature", and magic
strings in `Subtypes` meant "Artifact"/"Enchantment" — while instants and sorceries carried no
marker at all. "Noncreature spell", "nonland permanent" and Delirium were all unexpressible.

The derivation fallback exists so the type system could land without editing every hand-built card
in `CardLibrary`. It reads `CreatureComponent` and the subtype strings. It **cannot** tell an
instant from a sorcery, so it reports `Instant|Sorcery` — "a spell, kind unknown". Code that needs
the distinction must test for one flag and not the other; `MtgCardMapper.GetTypeLine` shows the
pattern.

`CardFactory.Instant(...)` / `.Sorcery(...)` declare it properly. `.Spell(...)` remains for the
several hundred existing cards and leaves it undeclared.

Specs: `IsCardTypeSpecification { Types }` (any-of) and `IsNotCardTypeSpecification { Types }`
(none-of). The negative form is its own type rather than `NotSpecification`-wrapping the positive
one, because the negation must still require the candidate to *be* a card — a plain `Not` matches
players too.

## Planeswalkers

`PlaneswalkerComponent { StartingLoyalty, Loyalty, HasActivatedThisTurn }` sits alongside
`PermanentComponent` and never alongside `CreatureComponent`, so a walker routes through
`CastPermanentAction` like any other non-creature permanent.

**Loyalty abilities are `ActivatedAbilityComponent` with `IsLoyaltyAbility = true` and a
`LoyaltyCost`** (positive for `+1`, negative for `-3`, zero for `0`). `IsLoyaltyAbility` is an
explicit flag rather than "LoyaltyCost != 0" because a 0-cost loyalty ability is a real card
(Gideon Jura) and would otherwise read as a normal free ability.

**The once-per-turn limit is per WALKER, not per ability** — it lives on `PlaneswalkerComponent`,
not on `ActivatedAbilityComponent.ActivationCount`. Tracking it per-ability would let a walker use
its `+1` and its `-3` on the same turn. `StartTurnAction` clears the flag; loyalty itself never
resets.

- **Entering play**: `GameState.StampPlaneswalkerEntry` sets loyalty to `StartingLoyalty`. Called
  from BOTH `ResolvePermanentAction` (cast) and `PutIntoBattlefieldAction` (reanimate), so a
  reanimated walker comes back whole rather than at 0.
- **Combat**: `AttackAction` accepts a planeswalker target; damage reduces loyalty, nothing strikes
  back, and lifelink still applies. Taunt is enforced — a Taunt creature cannot be ignored in
  favour of the walker behind it. `MtgActionGenerator` offers walkers as attack targets.
- **Effect damage**: `DealDamageAction` has a planeswalker arm routing to `DamagePlaneswalker`, and
  `TargetSpecification.PlayersOrCreatures()` — the "any target" helper every burn spell uses —
  includes `IsPlaneswalkerSpecification`. **Both halves were missing until red.** Every targeting
  helper was built from players and creatures, so no spell could NAME a walker; and the damage
  switch had no walker arm, so one handed the damage anyway took zero, silently. A walker was
  therefore unanswerable by anything except attacking it, from the moment white shipped the first
  one. Red is simply where it became impossible to miss.
- **Death**: `CheckStateBasedEffectsAction.DestroyZeroLoyaltyPlaneswalkers`. It emits
  `PermanentLeftBattlefieldEvent` but **not** `CreatureDestroyedEvent` — a walker is not a
  creature, and firing that would make every "whenever a creature dies" payoff trigger on it.
- **Ultimates**: `GrantEmblemAction` adds an `Emblem` to a player. Emblems already existed but were
  only reachable by playing a land with `GrantEmblemComponent`, so no effect could grant one.

## Attachments: Equipment and Auras

One mechanism, not two. `EquipmentComponent.IsAura` is the only difference, and it changes exactly
two behaviours:
- an aura attaches once on entering the battlefield (an ETB trigger) instead of via a repeatable
  equip ability;
- when the enchanted permanent leaves, the aura goes to the graveyard rather than detaching and
  staying put (`CheckStateBasedEffectsAction.DetachEquipmentFromLeavingCard`).

Real MTG picks an aura's target as the spell is cast. Nothing here can respond between cast and
resolution, so the ETB-trigger route is observationally identical and needs no new casting plumbing.

`EquippedBoostComponent` carries the keyword grants and `PreventsAttacking`. **Its keywords are read
in their own pass in `GetEffectiveStats`, not via `AppliedKeywordComponent`** — Permanent-duration
applied keywords are owned exclusively by `StaticAbilityEngine`, which would strip one stamped by
`AttachEquipmentAction`.

`PreventsAttacking` (Pacifism, Faith's Fetters) surfaces as `CreatureStats.CantAttack` and is
checked in `AttackAction.ValidateAttacker`. It is deliberately **not** permanent exhaustion:
`IsExhausted` is cleared every turn, so a Pacifism built on it would wear off after one turn.

Builder: `CardFactory.Enchantment(...).AsAura(power, toughness, flying:, firstStrike:, …)`.

**Detachment is two mirrored passes in `CheckStateBasedEffectsAction`, and both are needed.**
`DetachEquipmentFromLeavingCard` handles the enchanted permanent leaving; `RemoveBoostFromLeavingAttachment`
handles the ATTACHMENT leaving, stripping the `PowerToughnessModifier` it stamped and clearing its
own `EquippedToCardId`. Without the second one, a destroyed or bounced Sensory Deprivation left its
-3/-0 on the creature permanently — the boost lives on the creature as a component, so nothing
removes it just because the Aura is gone.

## Leaving the Battlefield

Everything that reacts to a permanent leaving play — static abilities, attachments, freeze locks,
Oblivion Ring's release — hangs off `PermanentLeftBattlefieldEvent`. Death, destruction and
sacrifice always announced it; **every other route off the battlefield did not**, so a bounced,
exiled or library-bound permanent silently left all its effects behind.

It is now emitted by `MoveCardTracked` (see `Zones/`), so any move off the battlefield gets it.

`CheckStateBasedEffectsAction.EvaluateDepartedCardTriggers` is the other half. The trigger passes
scan battlefields and graveyards; a permanent bounced to hand or exiled is in neither, so its own
departure trigger could never fire. Departed cards are given the graveyard pass — a
leave-the-battlefield ability is declared `ActiveInZone = Graveyard` because the graveyard is where
a permanent usually goes, but what it means is "after this left play".

## Cost Modification

Everything routes through `CostEngine.ComputeEffectiveCost(card, playerId, xValue)`. Order:
**X added first, then reductions (affinity, convoke), then taxes, floored at 0.** X is part of the
printed cost, so a convoked X-spell has its whole cost reduced.

- **X costs**: `XCostComponent` on the card; the chosen X lives on `CastSpellAction.XValue`, not on
  the card, so two copies can be cast for different X. `ResolveSpellAction` injects it into the
  effect's context as `ContextKeys.XValue`. `MtgActionGenerator` offers one action per affordable X,
  capped at 8 so a big mana pool cannot explode the action count.
- **Convoke**: `ConvokeComponent`. Costs {1} less per *ready* creature (not exhausted, not already
  attacked) and exhausts exactly that many on cast. Which creatures help is automatic — there is no
  colour to match, so a per-creature choice would reach the same board state with extra UI.
- **Spell tax**: `SpellTaxComponent`, scanned on **both** battlefields, since Vryn Wingmare taxes
  its own controller too.

## Modal Spells

`SelectModeAction` (a `ChoiceAction` over mode names) followed by `ApplyChosenModeAction`, which
indexes a list of actions with the chosen index. Modes are stored as data — a name and an action —
never as delegates, so a modal card stays serializable inside `GameState`.

## Multi-Effect Targeting

**A single chosen target is applied to EVERY effect whose strategy requires user selection.**
Both `MtgActionGenerator.AddTargetedSpellAction` and `ActivateAbilityAction` do this.

This is what cards like Feat of Resistance ("put a +1/+1 counter on target creature you control.
It gains hexproof") and Basri Ket's `+1` actually say: one target, several effects. Filling only
the first effect's index left the rest with empty target lists — the extra effects silently did
nothing, and for a spell the card was never offered as castable at all, because validation then
found no targets for the unfilled effect.

## Zero-Toughness Deaths

`CheckStateBasedEffectsAction.DestroyZeroToughnessCreatures` destroys any creature whose
**effective** toughness has fallen to zero or below.

Death by damage is decided at the damage site (`CreatureEvaluator.IsLethalDamage`), but a
creature shrunk by a `-X/-X` modifier takes no damage at all — without this pass a 2/2 hit
with -2/-2 would sit on the battlefield as a 2/0, and `-X/-X` could not work as removal.

It runs **after** `ProcessStaticAbilityUpdates`, so a lord leaving play and a `-X/-X` effect
are judged on the same pass, and appends its deaths to the pending event list so death
triggers still see them.

## Threshold

`ThresholdComponent` (a `PowerToughnessModifier`) grants P/T and keywords while the
controller's graveyard holds at least `Minimum` cards.

**It is deliberately not built on `StaticAbilityEngine`.** That engine is a push model: it
re-stamps only on `CreatureEnteredBattlefieldEvent` and `PermanentLeftBattlefieldEvent`. A
threshold keyed to graveyard size would go stale the instant a card was milled, discarded, or
exiled, because none of those fire a re-stamp. Instead the graveyard is counted live at read
time — `CreatureEvaluator` calls `GetPowerBonus`/`GetToughnessBonus` on every read, and
`GetEffectiveStats` reads the keyword half directly. Always correct, and no engine changes.

Set `Duration = ModifierDuration.Permanent` in card definitions or `StartTurnAction` strips it.

## Deathtouch

`CreatureComponent.HasDeathtouch` (also `StaticGrantKeywordAbility.GrantsDeathtouch`,
`AppliedKeywordComponent.GrantsDeathtouch`, `ThresholdComponent.GrantsDeathtouch`).

Lethality is decided in **one** place: `CreatureEvaluator.IsLethalDamage(cardId, totalDamage,
fromDeathtouch)`. Both `AttackAction` and `DealDamageAction` route through it, so deathtouch
cannot work in one and silently not the other. Deathtouch is a property of the damage *source*,
so callers pass it in; any nonzero deathtouch damage is lethal immediately, which is why no
"deathtouched" marker is persisted on the creature.

`ApplyCreatureVsCreature` reads both combatants' deathtouch **before** applying any damage, so
a creature that dies in the exchange still deals its deathtouch damage back.

Balance note: with no blockers, deathtouch makes every attack a favourable trade. Keep it rare.

## Discard Triggers (and the Madness reskin)

`CardDiscardedEvent` is now added to `PendingGameEvents` by both `DiscardCardsAction` and
`DiscardRandomCardAction`. **Before this, it was only placed on the returned `Events` list,
which is the caller-visible log and not the trigger feed — so no discard trigger had ever
fired.** Any new action that emits an event cards should trigger on must add it to
`PendingGameEvents`; adding it only to `ActionResult.Events` is silently inert.

Madness is modelled as a discard trigger rather than a cast window:

```csharp
new TriggeredAbilityComponent
{
    Name = "Madness",
    Condition = new EventTriggerCondition
    {
        EventTypeName = EventTypeNames.CardDiscarded,
        Filter = new IsSourceCardSpecification(),
    },
    Effect = /* ... */,
    ActiveInZone = ZoneType.Graveyard,   // REQUIRED — the card is already in the graveyard
}
```

The effect fires for **no mana cost** since the card is never cast — price madness effects as
free, not as a discount on the card's face cost. `CardMilledEvent` is a separate event
precisely so these payoffs do not fire on self-mill.

**Gotcha when writing trigger effects:** `TargetingStrategy.NoTarget()` resolves to an empty
target list, and `ResolveEffectAction` injects that over any hardcoded `TargetIds` on an
`ITargetedAction`. To affect the opponent from a trigger, use an action that derives them from
context (`DrainLifeAction` with `PlayerIdContextKey = ContextKeys.CastingPlayerId`) or a real
targeting strategy — not `NoTarget()` plus preset `TargetIds`.

## Creature Recursion from the Graveyard

`FlashbackComponent` on a creature means Gravecrawler/unearth/disturb: `CastFromGraveyardAction`
resolves it onto the battlefield via `ResolveCreatureAction` and does **not** exile it, so it can
be recurred every time it dies. Mana cost is the only limiter, which keeps the loop bounded.
On an instant or sorcery the component still means one-shot Flashback (resolve, then exile).

`MtgActionGenerator.AddGraveyardFlashbackActions` generates the creature case with no target
enumeration, since creatures carry no `SpellComponent`.

## Card Builder Additions for Hollowmere

`CreatureCardBuilder`: `WithDeathtouch()`, `WithThreshold(power, toughness, minimum, …)`,
`WithGraveyardRecursion(manaCost)`, and `WithDeathTrigger(name, effect)` — the last sets
`ActiveInZone = Graveyard` for you, which is mandatory and easy to forget.
`WithTriggeredAbility` now takes an optional `ActiveInZone` and keeps **all** effects the
builder produced, not just the first.

`SpellCardBuilder`: `WithMill(n)` (defaults to milling yourself; override with
`.WithTarget(Single().Opponent())`), `WithReanimate()`, `WithReturnCreatureFromGraveyard()`,
`WithReturnSpellFromGraveyard()`, `WithGrantKeyword(…)`, `WithExileFromGraveyard()`,
`WithOpponentDiscard(n)`, `WithDiscard(n)`, `WithProwessBuff()`, `WithCreateTokensPerCard(…)`.

**Discard comes in two flavours and they are not interchangeable:**

| | `WithDiscard(n)` | `WithDiscardCost(n)` |
|---|---|---|
| Mechanism | `SelectCardsFromHandAction` → `DiscardCardsAction` pipeline, no targeting | `DiscardAdditionalCost` on `AdditionalCastCosts` |
| Chosen | At resolution | Before the spell reaches the stack |
| Empty hand | Casts, discards nothing | Uncastable |
| Card text | "draw 4, then discard a card" | "as an additional cost, discard a card" |

A post-draw discard **must** be the choice form: cast-time selection happens before the draw, so a
targeted version could only ever pitch from the pre-draw hand.

Both are human-playable — cards in hand are clickable as targets and as cost payments (see
`MtgGameScene.OnHandCardClicked`). A hand card cannot be draggable and clickable at once:
starting a drag clears `CardUIManager.CurrentHoveredCard` on the same press, which is the
guard `CardUI2D._UnhandledInput` tests, so `Hand2D.DragEnabled` must be false whenever the
hand is a selection surface (`MtgGameScene.IsWaitingForSelection`).

Removal and selection verbs, added because the set was built from ~13 verbs and cards had
begun repeating each other at different mana costs: `WithWeaken(p, t)` (-X/-X, kills via the
zero-toughness rule), `WithBounce()`, `WithFight()`, `WithEdict()` (picks by mana cost, so it
answers Hexproof and Shroud), `WithTutor(subtype)`, `WithDig(n)`.

**Card design floors live in `MtgCore.Tests/HollowmereRateTests.cs`** — an ability-less
creature must meet a stats-plus-keywords rate floor, and no pure token-maker may be strictly
worse than another. Both exist because real cards failed them. Keep the comparisons to things
code can judge honestly; whether a card is *interesting* is a human review job.

`TargetBuilder`: `Players()`, `Opponent()`, `AllYourCreatures()`, `CreaturesInYourGraveyard()`,
`OtherCreaturesYouControl()`.

## Card Builder Additions for the Core Set Cube

`CreatureCardBuilder`: `WithFirstStrike()`, `WithIndestructible()`, `WithShroud()`,
`WithHexproof()`, `WithExalted(count)`, `WithProtectionFrom(params subtypes)`,
`WithLifeTotalBonus(p, t, minimum)`, `WithPowerEqualToCreatureCount()` (the `*/*` templating — build
the card at base 0/0), `WithSpellTax(amount)`, `WithLifeGainBonus(amount)`,
`WithCastRestriction(...)`, and `WithRenown(n)`.

`WithRenown` is worth using rather than hand-rolling: it sets `MaxTriggers = 1`, the **lifetime**
cap, which is what "if it isn't renowned" means. A per-turn cap makes the creature grow every turn.

`WithActivatedAbility` now takes `condition`, `requiresTap` and `maxPerTurn`.
`WithTriggeredAbility` now takes `maxTriggers` and `maxPerTurn`.

`SpellCardBuilder`: `WithExhaust()` (defaults to an opponent's creature) and `WithSelfBuff(p, t)`
— a permanent buff on the card running the effect, i.e. "put a +1/+1 counter on this creature".
`WithSelfBuff` sets `TargetContextKey = SourceCardId` rather than hardcoding `TargetIds`, which
matters because `ResolveEffectAction` overwrites hardcoded targets on a `NoTarget()` strategy.
`WithGrantKeyword` gained `firstStrike`, `doubleStrike`, `indestructible`, `exalted`, and the
previously-missing `reach`/`shroud`/`hexproof`.

`CreatureCostBuilder`: `SacrificeSelf()`, `Discard(count)`.

`SpellCardBuilder` (second pass): `WithScry(n)` — a real choice with `MinChoices = 0`, since an
unconditional bottom-the-top-card is strictly worse than doing nothing half the time;
`WithDamagePrevention(...)`, `WithConditionalAction(condition, action)`, `WithModes(...)`,
`WithXCost()`, `WithConvoke()`, `WithTypes(...)`.

Red pass: `WithImpulseDraw()` (see "Impulse Draw"), and `WithDiscardCost(count, subtype)` /
`CreatureCostBuilder.Discard(count, subtype)` for "discard a land card". Both discard-cost builders
route through one shared `DiscardCost` helper so the filter and its player-facing wording cannot
drift apart.

`PermanentCardBuilder` (new — `CardFactory.Enchantment` / `.Artifact` / `.Planeswalker`):
`WithLoyalty(n)`, `WithLoyaltyAbility(name, cost, effect)`, `AsAura(...)`, `WithStaticBoost(...)`,
plus the usual `WithEtbTrigger` / `WithTriggeredAbility` / `WithActivatedAbility` / `WithComponent`.
Its `WithEtbTrigger` uses `OnSelfEntersBattlefieldAsNonCreature` — a non-creature permanent never
fires `CreatureEnteredBattlefield`, so the creature version silently never triggers.

Deliberately a separate class from `CreatureCardBuilder` rather than a shared base: several hundred
existing cards depend on that builder, and re-parenting it to extract four small methods is a far
riskier change than duplicating them.

`SelectCardFromZoneAction.Filter` takes a `TargetSpecification`, because subtype alone cannot
express "a creature card" or "an instant or sorcery" — those are identified by components.

`ReturnToHandAction` is the targeted counterpart to `MoveCardToHandAction`, so
"return target creature card from your graveyard to your hand" works with ordinary targeting
instead of a pipeline.

## Card Creation Cookbook

Canonical recipes using verified working cards. All examples are in `Cards/CardLibrary.cs`.

### 1. Simple ETB trigger (no pipeline)

```csharp
CardFactory
    .Creature("Name", manaCost: N, power: P, toughness: T)
    .WithEtbTrigger("ETB Effect", eb => eb.WithCreateTokens(SomeToken()))
    .Build()
```

`WithEtbTrigger` is shorthand for `WithTriggeredAbility` using `TriggerConditions.OnSelfEntersBattlefield()`. Use `eb.WithDamage(3)`, `eb.WithDraw(1)`, etc. for non-token effects.

### 2. Death trigger (fires from graveyard)

```csharp
CardFactory
    .Creature("Mogg War Marshal", manaCost: 1, power: 1, toughness: 1)
    .WithComponent(new TriggeredAbilityComponent
    {
        Name = "Dies Token",
        Condition = TriggerConditions.OnSelfDies(),
        Effect = new CardEffect
        {
            TargetingStrategy = TargetingStrategy.NoTarget(),
            ActionTemplate = new CreateCardAction { CardTemplate = GoblinToken(), Count = 1 },
        },
        ActiveInZone = ZoneType.Graveyard,   // REQUIRED — card is already in graveyard when trigger fires
    })
    .Build()
```

**Key rule:** `ActiveInZone = ZoneType.Graveyard` is mandatory for death triggers. By the time `CheckStateBasedEffectsAction` scans for triggers, the card has already moved to the graveyard. `TriggerConditions.OnSelfDies()` wraps `EventTriggerCondition` + `IsSourceCardSpecification` so the trigger fires only for this specific card.

### 3. Pipeline ETB tutor (search library → put in hand)

```csharp
.WithEtbTrigger("ETB Tutor", eb => eb.WithAction(
    new PipelineAction
    {
        Steps = ImmutableList.Create<GameAction>(
            new SelectCardFromLibraryAction
            {
                Subtype = "Goblin",
                OutputKey = "tutor_target",
                PlayerIdContextKey = ContextKeys.CastingPlayerId,
            },
            new MoveCardToHandAction
            {
                CardIdContextKey = "tutor_target",
                PlayerIdContextKey = ContextKeys.CastingPlayerId,
            }
        ),
    },
    TargetingStrategy.NoTarget()
))
```

`SelectCardFromLibraryAction` writes a card ID to `OutputKey`. `MoveCardToHandAction` reads it via `CardIdContextKey`. `ContextKeys.CastingPlayerId` is injected by `ResolveEffectAction` before the pipeline runs — always use it to identify the card's controller. For multiple tutor steps (e.g. Ringleader fetching 3), chain pairs with unique keys (`ringleader_1`, `ringleader_2`, `ringleader_3`) — each step sees the updated game state so already-moved cards are skipped automatically.

### 4. Activated ability with pipeline and self-buff

```csharp
.WithActivatedAbility("Ability Name", manaCost: 0,
    effect: eb => eb.WithAction(
        new PipelineAction
        {
            Steps = ImmutableList.Create<GameAction>(
                new SelectCardFromZoneAction
                {
                    Zone = ZoneType.Graveyard,
                    TargetOpponent = true,                        // targets opponent's zone
                    PlayerIdContextKey = ContextKeys.CastingPlayerId,
                    OutputKey = "exile_target",
                },
                new MoveCardToExileAction { CardIdContextKey = "exile_target" },
                new GainLifeAction
                {
                    Amount = 1,
                    TargetContextKey = ContextKeys.CastingPlayerId,
                },
                new AddModifierAction
                {
                    PowerBonus = 1,
                    ToughnessBonus = 1,
                    Duration = ModifierDuration.Permanent,
                    TargetContextKey = ContextKeys.SourceCardId,   // buff the card itself
                }
            ),
        },
        TargetingStrategy.NoTarget()
    )
)
```

**`TargetContextKey = ContextKeys.SourceCardId`** on `AddModifierAction` buffs the card running the ability. `ContextKeys.SourceCardId` is injected by `ResolveEffectAction` alongside `CastingPlayerId`. **`TargetOpponent = true`** on `SelectCardFromZoneAction` derives the opponent via `GetOpponentId(gameState, castingPlayerId)` — same pattern as `DiscardRandomCardAction` and `SelectCardFromHandByManaCostAction`. Canonical example: Scavenging Ooze in `CardLibrary.cs`.

### 5. Context key quick-reference

| Key | What it holds | Who injects it |
|-----|--------------|----------------|
| `ContextKeys.CastingPlayerId` | Controller of the resolving card/ability | `ResolveEffectAction` |
| `ContextKeys.SourceCardId` | ID of the card whose effect is resolving | `ResolveEffectAction` |
| Custom key (e.g. `"tutor_target"`) | Output from a previous pipeline step | `SelectCardFromLibraryAction` / `SelectCardFromZoneAction` |

All `PlayerIdContextKey` and `CardIdContextKey` fields on actions read from the pipeline's `InputContext` — only set them when you want runtime lookup. Leave them empty and use the direct `int` fields for compile-time-known values.
