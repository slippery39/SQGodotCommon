# ImmutableGameObjects Library

## Architecture

Immutable, ID-based flat object store with separate relationship maps (`ParentToChildren`, `ChildToParent`). All state changes go through `GameState` methods — never mutate directly. `GameState` is the sole executor; actions never process other actions themselves, which prevents action loops.

## Key Files

| File | Purpose |
|------|---------|
| `GameState.cs` | Central state container and action executor |
| `GameAction.cs` | Base class for all actions |
| `PipelineAction.cs` | Chains steps that share runtime context |
| `ActionResult.cs` | Wraps execute output; carries events via `.WithEvent()` |
| `ChoiceActions/ChoiceAction.cs` | Player choice mid-resolution |
| `GameEvents/GameEvent.cs` | Base event type |
| `GameStateExtensions.cs` | Extension helpers on GameState |

## Actions

Actions are pure data records with an `Execute` method — no delegates or lambdas anywhere.

- `ValidateAdd(GameState)` — checked before the action is added to the stack. `AddAction` throws on failure; `TryAddAction` returns `(GameState, bool)` for graceful failure.
- `ValidateResolve(GameState)` — checked at resolution time. On failure, the action is dropped and an `ActionValidationFailedEvent` is emitted. The game layer decides what happens next (graveyard, exile, etc.).
- Actions spawn follow-up work via `state.SpawnAction()` / `state.SpawnActions()` inside `Execute()`. Spawned actions are staged in `GameState.SpawnQueue` and flushed onto the `ActionStack` after every action or pipeline step completes. This works safely for both standalone actions and pipeline steps.

## Pipelines

`PipelineAction` sequences steps that need to share runtime context (e.g. effect values only known at resolution time). Use it for dynamic effects; simple fixed-value effects don't need a pipeline.

- Context keys must be defined as constants (in MtgCore: `Actions/ContextKeys.cs`) to prevent silent failures from typos.
- Actions support a fixed `Amount` property and an `InputKey` property — `InputKey` reads from pipeline context and takes precedence over `Amount`.
- Pipeline steps cannot insert new steps mid-pipeline. Use spawned actions for conditional follow-up after the pipeline completes.
- Nested pipelines work correctly; contexts are isolated between inner and outer pipelines.

## Events

`ProcessNextAction`, `ProcessAllActions`, and `ResolveChoice` all return `(GameState State, ImmutableList<GameEvent> Events)` tuples. Events are **transient** — never stored in `GameState`, only in returned tuples. Actions emit events via `ActionResult.WithEvent()`.

## GameObjects

- **Component system**: `ImmutableList<GameComponent>` for strongly typed attachments. Multiple of the same component type are allowed.
- **Metadata store**: `ImmutableDictionary<string, object>` for sparse one-off values.
- Both are mutated on the object and then applied back via `GameState.UpdateObject`.

## PostActionProcessor

A `GameAction` template set on `GameState` that is automatically queued after every non-post-processor standalone action and completed pipeline. Set `IsPostProcessor = true` on an action to prevent recursion.

## ChoiceAction

Used for player decisions that occur mid-resolution (triggered abilities, "choose on resolve" effects). When playing a spell or activating an ability, targets are chosen upfront — `ChoiceAction` is not used for targeting.
