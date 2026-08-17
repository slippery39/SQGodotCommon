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
│   │                        # WHITE IS COMPLETE — all 67 cards. CoresetCube.cs assembles the files:
│   │                        #   CoresetCubeWhite.cs           38 creatures
│   │                        #   CoresetCubeWhiteSpells.cs     10 instants + 6 sorceries
│   │                        #   CoresetCubeWhitePermanents.cs 8 enchantments + 1 equipment + 4 planeswalkers
│   │                        #   CoresetCubeTokens.cs          token templates, excluded from the card list
│   │                        # Read each file's header before adding cards — they list every divergence from
│   │                        # the printed card and why. Other colours not started.
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

`CreatureComponent.IsExhausted` — this engine's tapped state. An exhausted creature cannot attack
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

This bug has now been found three separate times: `CardDiscardedEvent`, then
`PlayerGainedLifeEvent` (so *no* "whenever you gain life" trigger had ever fired), then
`TurnEndedEvent` (so no end-of-turn trigger could fire). All three are fixed. **Any new action that
emits an event a card might trigger on must add it to both.**

A second, quieter version of the same failure: an event with no `EventTypeNames` constant, or no
entry in `EventTriggerCondition.ExtractSubjectId`, cannot be filtered even though it fires.
`PermanentLeftBattlefieldEvent` had neither until Oblivion Ring needed it. **Adding an event means
three places: the record, the constant, and `ExtractSubjectId`.**

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
