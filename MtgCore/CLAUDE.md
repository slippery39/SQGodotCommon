# MtgCore — MTG Card Game Engine

## Source Map

```
MtgCore/
├── Abilities/Activated/     # ActivatedAbilityComponent, ActivatedAbilityAction
│   └── Static/              # StaticAbilityComponent (abstract), StaticPTBoostAbility, StaticGrantKeywordAbility
├── Actions/                 # All GameAction subclasses; ContextKeys; MtgActionGenerator
│                            # EffectAction — abstract base for all state-changing actions (DealDamageAction,
│                            #   GainLifeAction, LoseLifeAction, DrawCardsAction, DiscardCardsAction,
│                            #   AddTemporaryManaAction, ExileAction, DestroyCreatureAction, AddModifierAction).
│                            #   Provides TargetContextKey, AmountContextKey, TargetIds, WithTargets(),
│                            #   ResolveTargetIds(), ResolveAmount(), and lenient ValidateResolve.
│                            # Data/query actions (CountCardsWithSubtypeAction, CountCardsWithNameAction,
│                            #   RevealTopCardAction, LookAtTopCardsAction, SelectCardFromLibraryAction)
│                            #   are NOT EffectActions — they still use PlayerIdContextKey (scalar).
│                            # Includes: PutIntoBattlefieldAction (CardIdContextKey for pipeline use), CastCreatureAction, ResolveCreatureAction
│                            #           CastPermanentAction, ResolvePermanentAction (non-creature permanents → battlefield)
│                            #           CreateTokenAction
│                            #           TransformAction (EffectAction; swaps card face in place via TransformComponent; carries creature state across)
│                            #           AttachEquipmentAction (ITargetedAction; reads equipment ID from ContextKeys.SourceCardId)
│                            #           PlayLandAction (play land from hand: MaxMana++, CurrentMana++, LandsPlayedThisTurn++, LandsPlayedTotal++, card → graveyard)
│                            #           PutLandIntoPlayAction (effect-sourced land: MaxMana++, CurrentMana++, LandsPlayedTotal++ only, card → graveyard; used by Rampant Growth/Primeval Titan)
│                            #           CastFromGraveyardAction (flashback: casts a spell from graveyard at FlashbackManaCost; card exiles after resolution via MoveCardToExileAction)
│                            #           MoveCardToExileAction (post-resolution cleanup for flashback; analogous to MoveCardToGraveyardAction but routes to exile)
│                            #           MoveCardToGraveyardAction (post-resolution cleanup for normal spells)
│                            #           ResolveSpellAction.ExileAfterResolution — when true, spawns MoveCardToExileAction instead of MoveCardToGraveyardAction
│                            #           GiveFlashbackAction (EffectAction; adds FlashbackComponent{FlashbackManaCost=card.ManaCost} to a target spell in the graveyard; no-ops if already present)
│                            # DealDamageAction has PlayerOutputKey and CreatureOutputKey for pipeline chaining.
├── Costs/                   # AdditionalCost (abstract), LifeAdditionalCost, SacrificeAdditionalCost, DiscardAdditionalCost
├── Cards/                   # Card (GameObject subclass, has Subtypes + HasSubtype()), CardLibrary
│                            # Card lookup: use CardLibrary.GetByName("Name") — do NOT add new static per-card accessor methods.
│                            # The existing static accessors (LightningBolt(), GrizzlyBears(), etc.) are legacy and are being phased out.
│   ├── Builders/            # Fluent card builder API: CardFactory (entry point), SpellCardBuilder, CreatureCardBuilder,
│   │                        # TargetBuilder (use via 'using static'), TriggerConditions (static helpers)
│   │                        # Usage: CardFactory.Spell("Name", manaCost).WithDamage(3).WithTarget(Single().PlayersOrCreatures()).Build()
│   │                        # SpellCardBuilder.WithFlashback(cost) adds FlashbackComponent — card becomes castable from graveyard at that cost
│   │                        # SpellCardBuilder.WithGiveFlashback() — effect that adds FlashbackComponent to a random instant/sorcery in your graveyard; use in ETB triggers
│   │                        # CreatureCardBuilder.WithEtbTrigger(name, effect) — shorthand for WithTriggeredAbility using OnSelfEntersBattlefield() condition
│   │                        # TargetBuilder.InstantOrSorceryInYourGraveyard() — targets an instant/sorcery in the caster's own graveyard
│   └── Components/          # PermanentComponent (battlefield marker), CreatureComponent (HasHaste, HasDoubleStrike, HasFlying, HasTaunt, HasReach), SpellComponent (HasStorm), GraveyardCountComponent
│                            # FlashbackComponent { FlashbackManaCost } — marks a spell castable from graveyard; MtgActionGenerator scans graveyard for these and generates CastFromGraveyardAction
│                            # EquipmentComponent (PowerBonus, ToughnessBonus, EquippedToCardId — tracks attachment state)
│                            # ExtraLandPerTurnComponent — marker; presence on a controlled battlefield permanent grants +1 land play per turn (used by Exploration)
│                            # TransformComponent (OtherFaceName, OtherFaceSubtypes, OtherFaceComponents) — stores the other face of a double-faced card; TransformAction swaps Name/Subtypes/Components in place, preserving the card's ID and carrying creature state across
├── Effects/                 # CardEffect (data-only effect descriptor)
├── Events/                  # EventTypeNames, MtgEvents (includes CreatureEnteredBattlefieldEvent, CombatDamageDealtToPlayerEvent,
│                            #   SpellCastEvent emitted by CastSpellAction, CreaturePlayedEvent emitted by CastCreatureAction
│                            #   PermanentEnteredBattlefieldEvent emitted by ResolvePermanentAction for non-creature permanents
│                            #   LandPlayedEvent { PlayerId, CardId } emitted by both PlayLandAction and PutLandIntoPlayAction; triggers Steppe Lynx landfall)
├── Extensions/              # CreatureEvaluator (P/T aggregation extension methods), StaticAbilityEngine (push-model ETB/LTB logic)
├── Modifiers/               # PowerToughnessModifier (abstract base), StaticPowerToughnessModifier, AppliedStaticPTBoost
│                            # EquippedBoostComponent (stamped on creature by AttachEquipmentAction; removed on detach/creature-death)
│                            # LandsPlayedCountComponent — dynamic P/T modifier; bonus = controller's LandsPlayedTotal. Used by Land Elemental. Must be stamped with Duration = Permanent in card definitions.
├── Players/                 # MtgPlayer (GameObject subclass) — fields: Life, MaxMana, CurrentMana, LandsPlayedThisTurn (resets each turn), LandsPlayedTotal (never resets; used by Land Elemental)
├── Targeting/               # TargetSpecification (base), ZoneSpecification (abstract base for zone specs), TargetingContext, TargetingStrategy
│                            # Zone specs: IsOnBattlefieldSpecification, IsInHandSpecification, IsInstantOrSorceryInOwnGraveyardSpecification
│                            # Other specs: IsCreatureSpecification, IsPlayerSpecification, IsSubtypeSpecification,
│                            #              IsControlledByYouSpecification, IsControlledByOpponentSpecification,
│                            #              IsSourceCardSpecification, IsNotSelfSpecification, AlwaysFalseSpecification
│                            # Composites: AndSpecification (zone-first candidate narrowing), OrSpecification, NotSpecification
├── Triggers/                # TriggeredAbilityComponent, EventTriggerCondition, TriggerCondition
├── Turns/                   # BeginGameAction, SetupGameAction, StartTurnAction, EndTurnAction, TurnPhase
├── Zones/                   # Zone, ZoneType
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

**Prefer `GetEffectiveStats(state, cardId) → CreatureStats`** when multiple properties are needed — it reads from the card's own components, returning power, toughness, and all keywords in one O(1) pass. The individual methods (`GetEffectivePower`, `GetEffectiveToughness`, `GetEffectiveHaste`, etc.) delegate to it.

`AttackAction` and `DealDamageAction` must never read base values directly. `AttackAction.ValidateTauntConstraint` calls `GetEffectiveStats` once per creature to cover Taunt, Flying, and Reach checks in a single pass.

**Static ability effects are pre-computed (push model):** `StaticAbilityEngine` stamps `AppliedStaticPTBoost` and `AppliedKeywordComponent` onto affected permanents via `CheckStateBasedEffectsAction` in response to `CreatureEnteredBattlefieldEvent` and `PermanentLeftBattlefieldEvent`. `GetEffectiveStats` reads those applied components directly — no board scan. `MtgGame.StaticSourceIds` caches the set of battlefield permanents with active static abilities for O(k) source lookup.

`GetEffectiveHaste(state, cardId)` returns true if the creature has intrinsic `HasHaste` on `CreatureComponent` OR an `AppliedKeywordComponent{GrantsHaste=true}` is stamped on it. `AttackAction.ValidateAdd` and `MtgActionGenerator` both call this instead of reading `HasHaste` directly.

### PowerToughnessModifier

`PowerToughnessModifier` is an abstract `GameComponent` base with `Duration` (`UntilEndOfTurn` or `Permanent`) and `SourceCardId`. Subclasses implement `GetPowerBonus(GameState, int cardId)` and `GetToughnessBonus(GameState, int cardId)`. `CreatureEvaluator` calls these methods — no type switching.

- `StaticPowerToughnessModifier` — fixed `PowerBonus` / `ToughnessBonus` values. Used by `AddModifierAction` for spells like Giant Growth and Unholy Strength.
- `GraveyardCountComponent` — dynamic modifier; both bonus methods return the total card count across all graveyards. Used by Tarmogoyf (base Power = 0, base Toughness = 1).

Applied via `AddModifierAction`. `UntilEndOfTurn` modifiers are cleared by `StartTurnAction`.

## Mana System

Land-based. Both players start at `MaxMana = 0`, `CurrentMana = 0`. All permanent mana comes from playing land cards.

- **Playing a land** (`PlayLandAction`): `MaxMana++`, `CurrentMana++`, `LandsPlayedThisTurn++`, `LandsPlayedTotal++`. Card moves Hand → Graveyard. Emits `LandPlayedEvent`.
- **Effect-sourced lands** (`PutLandIntoPlayAction`): same MaxMana/CurrentMana/LandsPlayedTotal increments, but does NOT increment `LandsPlayedThisTurn` (doesn't consume the land-per-turn). Used by Rampant Growth and Primeval Titan ETB.
- `StartTurnAction` refills `CurrentMana = MaxMana` and resets `LandsPlayedThisTurn = 0`. It does **not** auto-increment `MaxMana`.
- **Land limit**: one land play per turn. Each permanent with `ExtraLandPerTurnComponent` controlled by the player adds +1. Limit is computed dynamically in `PlayLandAction.ValidateAdd` — no stored `LandsAllowedThisTurn` field.
- `CastCreatureAction` and `CastSpellAction` validate sufficient mana in `ValidateAdd` and deduct `ManaCost` in `Execute`.
- **Fast mana** (`AddTemporaryManaAction`) adds to `CurrentMana` only — `MaxMana` is unchanged, so the bonus evaporates at the start of the next turn. Used by Rite of Flame, Seething Song, Lotus Bloom.
- `MtgGameFactory.CreateForTesting()` gives both players `MaxMana = 99` / `CurrentMana = 99` — use in all unit tests not specifically testing the land system.

## Storm Mechanic

`MtgGame.SpellsCastThisTurn` (int) counts every spell cast this turn by either player. It is:
- Incremented by `CastSpellAction.Execute()` and `CastCreatureAction.Execute()` after mana is spent.
- Reset to 0 by `StartTurnAction.Execute()` at the start of each turn.
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

`MtgActionGenerator` calls `GetValidPayments` and picks the first valid option per selection cost — sufficient for the AI. The console auto-selects the first valid payment (player choice UI is deferred).

## Activated Abilities

- Modelled as `ActivatedAbilityComponent` on a card. Fields: `Name`, `ManaCost`, `AdditionalCosts`, `CardEffect`, `HasActivated`.
- A card may have multiple `ActivatedAbilityComponent` instances — one per ability, each independently tracked.
- Each ability can be activated once per turn. `HasActivated` is cleared by `StartTurnAction`.
- `ActivateAbilityAction` validates mana and additional costs, checks `HasActivated`, marks the ability used, pays all costs, resolves targets, and spawns the effect — using the same `CardEffect` and `TargetingStrategy` infrastructure as spells.
- Tap costs not yet implemented.

## Combat System

Hearthstone-style (turn-based, no blockers). The active player attacks; the opponent does not assign blockers.

- `HasSummoningSickness` — cannot attack the turn they enter the battlefield. Cleared by `HasHaste` on `CreatureComponent` — haste creatures enter with `HasSummoningSickness = false`.
- `HasDoubleStrike` — creature deals damage twice. vs player: two separate damage applications, two `CombatDamageDealtToPlayerEvent`s (triggers fire twice). vs creature: deals 2× power in one pass.
- `HasAttacked` — can only attack once per turn.
- `HasFlying` — bypasses Taunt from non-flying/non-reach creatures. Evaluated via `GetEffectiveFlying()`.
- `HasTaunt` — must be attacked before non-taunt targets. Enforced in `AttackAction.ValidateTauntConstraint`. Evaluated via `GetEffectiveTaunt()`.
- `HasReach` — intercepts flying attackers; flying does not bypass Taunt from Reach creatures. Evaluated via `GetEffectiveReach()`.
- `HasLifelink` — when the creature deals combat damage, its controller gains that much life. Applies to damage to players and creatures (including trample excess, which is counted once as part of total power). Defender lifelink also triggers on counter-damage in creature vs creature combat.
- `HasTrample` — when attacking a creature, excess damage beyond the target's effective toughness carries over to the defending player. Applies per strike for double strike.
- All keywords can also be granted by `StaticGrantKeywordAbility` (same pattern as `GrantsHaste`).
- `HasSummoningSickness`, `HasAttacked` on `CreatureComponent` are cleared by `StartTurnAction`. `Damage` persists between turns — creatures can be chipped down across multiple turns.
- Creature attacks player: deals damage equal to effective Power; emits `CombatDamageDealtToPlayerEvent` (used by Goblin Lackey/Warren Instigator triggers).
- Creature attacks creature: both deal damage simultaneously. Dies if `Damage >= effective Toughness`; moves to owner's graveyard.
- Damage resets each turn — creatures cannot be chipped down over multiple turns.

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
- `MtgGameFactory.CreateForTesting()` gives both players `MaxMana = 99` / `CurrentMana = 99`. Use in all unit tests not specifically testing the land/mana system. Use `MtgGameFactory.Create()` with manual land plays for land-specific tests.
- The simulator detects potential infinite loops via per-turn action counts (warning at 50, cutoff at 100) and flags unusual games.

## Debugging / Error Handling

When a logic error is found: write an NUnit test to isolate it first. Do not assume the cause — verify the assumption before proceeding.

## Deferred / Future Work

These are designed but not yet implemented. Do not re-implement or work around these planned patterns:

| Step | Feature | Notes |
|------|---------|-------|
| 3 | Zone-dependent statics | Wonder-style abilities active only in specific zones. `ActiveInZone` property on `StaticAbilityComponent`. |
| 4 | Keyword abilities as components | Lifelink, Deathtouch, Trample etc. as individual components checked by relevant actions. Rules-engine keywords that do not use the stack. Note: `HasHaste` and `HasDoubleStrike` are currently implemented as flags on `CreatureComponent` — these should be migrated to individual components when the full keyword system is built. |
| — | Goblin Chieftain lord effect | "+1/+1 and haste to other Goblins" deferred until Step 2 static anthems. Currently a 2/2 haste for 3. |
| — | Tap costs | Krenko's activation is modelled as a free (0-mana) ability since tap costs are not yet implemented. |
