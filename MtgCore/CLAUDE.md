# MtgCore — MTG Card Game Engine

## Source Map

```
MtgCore/
├── Abilities/Activated/     # ActivatedAbilityComponent, ActivatedAbilityAction
├── Actions/                 # All GameAction subclasses; ContextKeys; MtgActionGenerator
│                            # Includes: ExileAction
├── Cards/                   # Card (GameObject subclass, has Subtypes + HasSubtype()), CardLibrary
│   └── Components/          # CreatureComponent, SpellComponent, GraveyardCountComponent
├── Effects/                 # CardEffect (data-only effect descriptor)
├── Events/                  # EventTypeNames, MtgEvents (includes CardExiledEvent)
├── Extensions/              # CreatureEvaluator (P/T aggregation extension methods)
├── Modifiers/               # PowerToughnessModifier (abstract base), StaticPowerToughnessModifier
├── Players/                 # MtgPlayer (GameObject subclass)
├── Targeting/               # TargetSpecification, TargetingContext, TargetingStrategy
│                            # Includes: IsSubtypeSpecification
├── Triggers/                # TriggeredAbilityComponent, EventTriggerCondition, TriggerCondition
├── Turns/                   # BeginGameAction, SetupGameAction, StartTurnAction, EndTurnAction, TurnPhase
├── Zones/                   # Zone, ZoneType
├── MtgGame.cs               # Core game state object (ActivePlayerId, TurnNumber)
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

`CreatureEvaluator` (`Extensions/CreatureEvaluator.cs`) aggregates P/T from multiple sources in order:

1. Base values on `CreatureComponent`
2. `PowerToughnessModifier` components on the card (from spells like Giant Growth)
3. Static ability bonuses from `StaticAbilityComponent` on battlefield permanents *(Step 2 — not yet implemented)*

Always use the extension methods `GetEffectivePower`, `GetEffectiveToughness`, and `HasLethalDamage` on `GameState`. `AttackAction` and `DealDamageAction` must never read base values directly.

### PowerToughnessModifier

`PowerToughnessModifier` is an abstract `GameComponent` base with `Duration` (`UntilEndOfTurn` or `Permanent`) and `SourceCardId`. Subclasses implement `GetPowerBonus(GameState, int cardId)` and `GetToughnessBonus(GameState, int cardId)`. `CreatureEvaluator` calls these methods — no type switching.

- `StaticPowerToughnessModifier` — fixed `PowerBonus` / `ToughnessBonus` values. Used by `AddModifierAction` for spells like Giant Growth and Unholy Strength.
- `GraveyardCountComponent` — dynamic modifier; both bonus methods return the total card count across all graveyards. Used by Tarmogoyf (base Power = 0, base Toughness = 1).

Applied via `AddModifierAction`. `UntilEndOfTurn` modifiers are cleared by `StartTurnAction`.

## Mana System

Hearthstone-style. Both players start at `MaxMana = 0`, `CurrentMana = 0`.

- `StartTurnAction` increments `MaxMana` by 1 (cap 10) and refills `CurrentMana` to `MaxMana` for both players every turn. Starting at 0 gives both players 1 mana on turn 1 with no special casing.
- `PlayCreatureAction` and `CastSpellAction` validate sufficient mana in `ValidateAdd` and deduct `ManaCost` in `Execute`.
- All mana generation logic lives in `StartTurnAction` only. Future mana systems (lands, flat grants) swap in by changing `StartTurnAction` only.

## Activated Abilities

- Modelled as `ActivatedAbilityComponent` on a card. Fields: `Name`, `ManaCost`, `CardEffect`, `HasActivated`.
- A card may have multiple `ActivatedAbilityComponent` instances — one per ability, each independently tracked.
- Each ability can be activated once per turn. `HasActivated` is cleared by `StartTurnAction`.
- `ActivateAbilityAction` validates mana, checks `HasActivated`, marks the ability used, spends mana, resolves targets, and spawns the effect — using the same `CardEffect` and `TargetingStrategy` infrastructure as spells.
- Cost is mana only for now; tap costs and other cost types may be added later.

## Combat System

Hearthstone-style (turn-based, no blockers). The active player attacks; the opponent does not assign blockers.

- `HasSummoningSickness` — cannot attack the turn they enter the battlefield.
- `HasAttacked` — can only attack once per turn.
- Both flags and `Damage` on `CreatureComponent` are cleared by `StartTurnAction` at the start of the controller's turn.
- Creature attacks player: deals damage equal to effective Power; attacker takes no damage.
- Creature attacks creature: both deal damage simultaneously. Dies if `Damage >= effective Toughness`; moves to owner's graveyard.
- Damage resets each turn — creatures cannot be chipped down over multiple turns.

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
| 2 | Static P/T modifiers | `StaticAbilityComponent` on permanents for anthem/lord effects. `CreatureEvaluator` adds a second pass scanning battlefield permanents for applicable bonuses. |
| 3 | Zone-dependent statics | Wonder-style abilities active only in specific zones. `ActiveInZone` property already designed on `StaticAbilityComponent`. |
| 4 | Keyword abilities as components | Lifelink, Deathtouch, Trample etc. as individual components checked by relevant actions. Rules-engine keywords that do not use the stack. |
