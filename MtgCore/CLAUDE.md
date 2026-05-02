# MtgCore — MTG Card Game Engine

## Source Map

```
MtgCore/
├── Abilities/Activated/     # ActivatedAbilityComponent, ActivatedAbilityAction
│   └── Static/              # StaticAbilityComponent (abstract), StaticPTBoostAbility, StaticGrantKeywordAbility
├── Actions/                 # All GameAction subclasses; ContextKeys; MtgActionGenerator
│                            # Includes: ExileAction, PutIntoBattlefieldAction (CardIdContextKey for pipeline use), CastCreatureAction, ResolveCreatureAction
│                            #           CastPermanentAction, ResolvePermanentAction (non-creature permanents → battlefield)
│                            #           CreateTokenAction, CountCardsWithSubtypeAction, CountCardsWithNameAction
│                            #           AddTemporaryManaAction, SelectCardFromLibraryAction
├── Costs/                   # AdditionalCost (abstract), LifeAdditionalCost, SacrificeAdditionalCost, DiscardAdditionalCost
├── Cards/                   # Card (GameObject subclass, has Subtypes + HasSubtype()), CardLibrary
│   └── Components/          # PermanentComponent (battlefield marker), CreatureComponent (HasHaste, HasDoubleStrike, HasFlying, HasTaunt, HasReach), SpellComponent (HasStorm), GraveyardCountComponent
├── Effects/                 # CardEffect (data-only effect descriptor)
├── Events/                  # EventTypeNames, MtgEvents (includes CreatureEnteredBattlefieldEvent, CombatDamageDealtToPlayerEvent,
│                            #   SpellCastEvent emitted by CastSpellAction, CreaturePlayedEvent emitted by CastCreatureAction
│                            #   PermanentEnteredBattlefieldEvent emitted by ResolvePermanentAction for non-creature permanents)
├── Extensions/              # CreatureEvaluator (P/T aggregation extension methods), StaticAbilityEngine (push-model ETB/LTB logic)
├── Modifiers/               # PowerToughnessModifier (abstract base), StaticPowerToughnessModifier, AppliedStaticPTBoost
├── Players/                 # MtgPlayer (GameObject subclass)
├── Targeting/               # TargetSpecification (base), ZoneSpecification (abstract base for zone specs), TargetingContext, TargetingStrategy
│                            # Zone specs: IsOnBattlefieldSpecification, IsInHandSpecification
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

Hearthstone-style. Both players start at `MaxMana = 0`, `CurrentMana = 0`.

- `StartTurnAction` increments `MaxMana` by 1 (cap 10) and refills `CurrentMana` to `MaxMana` for both players every turn. Starting at 0 gives both players 1 mana on turn 1 with no special casing.
- `CastCreatureAction` and `CastSpellAction` validate sufficient mana in `ValidateAdd` and deduct `ManaCost` in `Execute`.
- All mana generation logic lives in `StartTurnAction` only. Future mana systems (lands, flat grants) swap in by changing `StartTurnAction` only.
- **Fast mana** (`AddTemporaryManaAction`) adds to `CurrentMana` only — `MaxMana` is unchanged, so the bonus evaporates at the start of the next turn. Used by Rite of Flame, Seething Song, Lotus Bloom.

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
- All three keywords can also be granted by `StaticGrantKeywordAbility` (same pattern as `GrantsHaste`).
- Both flags and `Damage` on `CreatureComponent` are cleared by `StartTurnAction` at the start of the controller's turn.
- Creature attacks player: deals damage equal to effective Power; emits `CombatDamageDealtToPlayerEvent` (used by Goblin Lackey/Warren Instigator triggers).
- Creature attacks creature: both deal damage simultaneously. Dies if `Damage >= effective Toughness`; moves to owner's graveyard.
- Damage resets each turn — creatures cannot be chipped down over multiple turns.

## Token Creation

`CreateTokenAction` creates new `Card` objects directly on a player's battlefield. Tokens are not drawn from any zone — they are created fresh via `GameState.AddObject`. Each token emits `CreatureEnteredBattlefieldEvent` so ETB triggers fire normally.

- `ControllerId`: if 0, reads from `InputContext[CastingPlayerId]` (set by `ResolveEffectAction`).
- `CountInputKey`: if set, reads the count from pipeline context (used by Krenko, Mob Boss to count Goblins at resolution time via `CountCardsWithSubtypeAction`).
- `HasSummoningSickness` is stamped based on the token template's `HasHaste` flag.

## Game Startup

`MtgGameStateExtensions.BeginGame(state, gameId, player1Id, player2Id)` is the **single entry point** for all presentation layers.

- `BeginGame` dispatches `BeginGameAction` → spawns `SetupGameAction` → spawns `StartTurnAction` for Player 1 with `SkipDraw = true`.
- `SetupGameAction` shuffles both libraries (Fisher-Yates via `ShuffleLibrary` on `MtgGameStateExtensions`) and deals 7-card opening hands to both players.
- Presentation layers never construct `BeginGameAction`, `SetupGameAction`, or `StartTurnAction` directly. Turn transitions (`EndTurnAction` spawning `StartTurnAction`) are handled entirely within MtgCore.

## Turn Structure

Files: `Turns/BeginGameAction.cs`, `SetupGameAction.cs`, `StartTurnAction.cs`, `EndTurnAction.cs`, `TurnPhase.cs`

- **`StartTurnAction`**: increments `MaxMana`, refills `CurrentMana`, optionally draws (`SkipDraw` flag), clears per-turn flags on all permanents the active player controls (`HasSummoningSickness`, `HasAttacked`, `Damage` on `CreatureComponent`; `HasActivated` on `ActivatedAbilityComponent`; `UntilEndOfTurn` P/T modifiers).
- **`EndTurnAction`**: switches `ActivePlayerId`, increments `TurnNumber` when Player 2 ends their turn, spawns `StartTurnAction` for the next player.
- `MtgGame` tracks `ActivePlayerId` and `TurnNumber`. Phases within a turn are not yet modelled — the turn is a single phase.
- Win/loss conditions checked by `CheckStateBasedEffectsAction` as the `PostActionProcessor` after every action (life ≤ 0, empty library).

## Presentation Layer Rules

- `MtgConsole` and `MtgSimulator` never construct game actions directly for game flow — use `MtgGameStateExtensions` methods as the API boundary.
- Presentation layers never modify game state directly. All state changes go through `GameAction`s. Exceptions: explicit test setup and debug/cheat tooling (both must be clearly commented as such).
- `MtgActionGenerator.GetLegalActions(state, ids, playerId)` is the **single shared source** of legal action generation. Console and simulator both call this — never duplicate this logic.
- `MtgGameFactory.CreateForTesting()` gives both players `MaxMana = 99` / `CurrentMana = 99`. Use in all unit tests not specifically testing mana. Use `MtgGameFactory.Create()` with manual mana setup for mana-specific tests.
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
