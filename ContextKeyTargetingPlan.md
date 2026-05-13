# Context Key Targeting — Design Plan

## Overview

All effect actions support two targeting mechanisms interchangeably:

1. **Targeting system** — `TargetingStrategy` on `CardEffect` resolves targets and injects them via
   `ITargetedAction.WithTargets()`. Used for standalone effects where targets are selected at cast time.

2. **Context injection** — `TargetContextKey` on the action reads `ImmutableList<int>` from pipeline
   context at execution time. Used for pipeline steps where targets are the output of a previous step.

`TargetContextKey` wins when set. Falls back to `TargetIds` otherwise.

The same pattern applies to amounts: `AmountContextKey` wins over the direct `Amount` field.

`CastingPlayerId` (scalar `int`) remains in context for non-effect actions that need to know who cast
the spell (data/query steps like `CountCardsWithSubtypeAction`). It can also be used as a
`TargetContextKey` value — the resolver coerces `int → [int]` automatically. This is particularly
important for pipeline steps, which do not have their own `TargetingStrategy` — `CastingPlayerId` is
the bridge when a step needs to target the player who initiated the effect.

---

## `EffectAction` Abstract Base Record

Only effect actions (those that change game state by affecting entities) get the new mechanism.
Infrastructure and data/query actions are unaffected. The separation is enforced by a new abstract
base record in `MtgCore`:

```csharp
public abstract record EffectAction : GameAction, ITargetedAction
{
    /// <summary>
    /// Context key for target ID injection. When set, ResolveTargetIds reads
    /// ImmutableList<int> from InputContext under this key instead of using TargetIds.
    /// int values in context are coerced to a single-element list automatically.
    /// </summary>
    public string TargetContextKey { get; init; } = "";

    /// <summary>
    /// Context key for amount injection. When set, ResolveAmount reads int from
    /// InputContext under this key instead of using the direct Amount field.
    /// </summary>
    public string AmountContextKey { get; init; } = "";

    public ImmutableList<int> TargetIds { get; init; } = ImmutableList<int>.Empty;

    public GameAction WithTargets(ImmutableList<int> targetIds) => this with { TargetIds = targetIds };

    protected ImmutableList<int> ResolveTargetIds()
    {
        if (string.IsNullOrEmpty(TargetContextKey)) return TargetIds;

        var raw = InputContext.GetValueOrDefault(TargetContextKey);
        return raw switch
        {
            ImmutableList<int> list => list,
            int id                  => ImmutableList.Create(id),
            _                       => TargetIds,
        };
    }

    protected int ResolveAmount(int directValue)
    {
        return string.IsNullOrEmpty(AmountContextKey)
            ? directValue
            : GetInput<int>(AmountContextKey, 0);
    }

    /// <summary>
    /// Default validation: all resolved target IDs must exist in the game state.
    /// Subclasses can override to add further restrictions.
    /// </summary>
    public override ValidationResult ValidateResolve(GameState gameState)
    {
        var missingId = ResolveTargetIds().FirstOrDefault(id => !gameState.HasObject(id));
        return missingId != 0
            ? ValidationResult.Invalid($"Target {missingId} no longer exists")
            : ValidationResult.Valid;
    }
}
```

`ITargetedAction` and `WithTargets` live here — subclasses no longer declare them individually.
`ResolveEffectAction`'s `is ITargetedAction` check continues to work since all `EffectAction`
subclasses satisfy the interface.

---

## Effect vs Data Actions

**Effect actions** inherit from `EffectAction`. Each drops its individual `PlayerIdContextKey`,
`AmountContextKey`, and `ITargetedAction` boilerplate in favour of the base.

| Action | Change |
|---|---|
| `DealDamageAction` | Inherit `EffectAction`; remove `ITargetedAction` boilerplate; call `ResolveTargetIds` / `ResolveAmount`; add `PlayerOutputKey` + `CreatureOutputKey` |
| `GainLifeAction` | Inherit `EffectAction`; remove `PlayerIdContextKey` / `AmountContextKey` / `ITargetedAction` boilerplate |
| `LoseLifeAction` | Inherit `EffectAction`; remove `PlayerIdContextKey` / `AmountContextKey`; add `ITargetedAction` via base |
| `DrawCardsAction` | Inherit `EffectAction`; remove `PlayerIdContextKey` / `ITargetedAction` boilerplate |
| `DiscardCardsAction` | Inherit `EffectAction`; remove `PlayerIdContextKey` / `ITargetedAction` boilerplate |
| `AddTemporaryManaAction` | Inherit `EffectAction`; remove `PlayerIdContextKey` / `ITargetedAction` boilerplate |
| `ExileAction` | Inherit `EffectAction`; remove `ITargetedAction` boilerplate |
| `DestroyCreatureAction` | Inherit `EffectAction`; remove `ITargetedAction` boilerplate (if present) |
| `AddModifierAction` | Inherit `EffectAction`; remove `ITargetedAction` boilerplate |

**Data / query actions** are not effect actions and do not inherit from `EffectAction`. They continue
using `PlayerIdContextKey` (scalar) where needed.

| Action | No change |
|---|---|
| `CountCardsWithSubtypeAction` | Reads player's battlefield — scalar player ID is correct |
| `CountCardsWithNameAction` | Same |
| `RevealTopCardAction` | Reads player's library — scalar player ID is correct |
| `LookAtTopCardsAction` | Same |
| `SelectCardFromLibraryAction` | Same |

---

## Target Validation

There are two distinct validation points with different rules:

**`ValidateAdd` — at play time (strict)**
All required targets must be valid before the card can be played. If a target doesn't exist or
doesn't satisfy the targeting specification, the card cannot be played at all. This is enforced by
`MtgActionGenerator` when building the legal action list — invalid targets mean the card simply
doesn't appear as a legal play.

**`ValidateResolve` — at resolution time (lenient)**
By the time a spell resolves, some targets may have become invalid (e.g. a creature was destroyed in
response). In this case, invalid targets are silently skipped — `Execute` already uses `continue` for
missing targets. The action only fully fizzles (returns early as a no-op) if every target is gone.

The `EffectAction` base default `ValidateResolve` reflects this: it returns `Valid` unless all
resolved targets are missing. Subclasses override only when additional checks are needed (e.g.
`DestroyCreatureAction` may check that any surviving target is still a creature).

Both paths — targets from the targeting system (`TargetIds`) and targets from pipeline context
(`TargetContextKey`) — go through `ResolveTargetIds()`, so the same rules apply regardless of how
targets were injected.

---

## Multiple Output Keys

An action may need to output more than one set of IDs for downstream pipeline steps. For example,
`DealDamageAction` may want to expose damaged players and damaged creatures under separate keys so
downstream steps can consume whichever is relevant. This is done via optional output key properties
on the action:

```csharp
public record DealDamageAction : EffectAction
{
    public int Amount { get; init; }
    public string PlayerOutputKey { get; init; } = "";    // outputs IDs of players hit
    public string CreatureOutputKey { get; init; } = "";  // outputs IDs of creatures hit
    // ...
}
```

Each non-empty key is written to `ActionResult` via `WithOutput(key, value)`. Downstream steps read
them via `TargetContextKey`. An action can have as many output keys as needed — the pipeline context
accumulates all of them.

---

## Resolution Flow

### Path 1 — Standalone effect (targeting system)

```
CardEffect {
    TargetingStrategy = SingleTarget(PlayersOrCreatures)
    ActionTemplate    = DealDamageAction { Amount = 3 }
}

ResolveEffectAction:
  1. Evaluates TargetingStrategy → resolvedTargets = [opponentId]
  2. Calls DealDamageAction.WithTargets([opponentId]) → TargetIds = [opponentId]
  3. Seeds CastingPlayerId into InputContext
  4. Spawns the action

DealDamageAction.Execute:
  var targets = ResolveTargetIds()      // TargetContextKey empty → returns [opponentId]
  var amount  = ResolveAmount(Amount)   // AmountContextKey empty → returns 3
  // deals 3 damage to opponent
```

### Path 2 — Pipeline step (context injection, pipeline-to-pipeline)

```
PipelineAction {
    Steps = [
        DealDamageAction {
            Amount          = 3
            PlayerOutputKey = "damaged_player_ids"   // outputs damaged player IDs
        },
        DrawCardsAction {
            TargetContextKey = "damaged_player_ids"  // reads who was hit
            Amount           = 1
        }
    ]
}

DrawCardsAction.Execute:
  var targets = ResolveTargetIds()
  // TargetContextKey = "damaged_player_ids" → reads [opponentId] from InputContext
  // draws 1 card for each player in the list
```

### Path 3 — Pipeline step targeting the casting player

Pipeline steps do not have their own `TargetingStrategy`. When a step needs to affect the player who
cast the spell, it reads `CastingPlayerId` from context — which `ResolveEffectAction` always seeds —
via `TargetContextKey`. The int→list coercion handles the type difference.

```
DrawCardsAction {
    TargetContextKey = ContextKeys.CastingPlayerId  // int in context → coerced to [castingPlayerId]
    Amount           = 1
}

DrawCardsAction.Execute:
  var targets = ResolveTargetIds()
  // reads int from context → [castingPlayerId]
  // draws 1 card for the caster
```

---

## Example Cards (Before / After)

### Lightning Bolt
No change — standalone targeted damage has always been correct.

```csharp
new CardEffect {
    TargetingStrategy = TargetingStrategy.SingleTarget(TargetSpecification.PlayersOrCreatures()),
    ActionTemplate    = new DealDamageAction { Amount = 3 },
}
```

---

### Healing Salve (gain 10 life)
No change — `TargetingStrategy.Self()` injects the casting player into `TargetIds` via the targeting
system. `GainLifeAction` inherits `EffectAction` so the boilerplate goes away, but the card
definition is identical.

```csharp
new CardEffect {
    TargetingStrategy = TargetingStrategy.Self(),
    ActionTemplate    = new GainLifeAction { Amount = 10 },
}
```

---

### Ancestral Recall (draw 3 cards)
`DrawCardsAction` becomes an `EffectAction`. Standalone use switches from
`TargetingStrategy.NoTarget() + PlayerIdContextKey` to `TargetingStrategy.Self()`.

```csharp
// Before
new CardEffect {
    TargetingStrategy = TargetingStrategy.NoTarget(),
    ActionTemplate    = new DrawCardsAction {
        Amount             = 3,
        PlayerIdContextKey = ContextKeys.CastingPlayerId,
    },
}

// After
new CardEffect {
    TargetingStrategy = TargetingStrategy.Self(),
    ActionTemplate    = new DrawCardsAction { Amount = 3 },
}
```

---

### Phyrexian Arena (draw 1, lose 1 life — pipeline)
Pipeline steps use `TargetContextKey = ContextKeys.CastingPlayerId` instead of `PlayerIdContextKey`.

```csharp
// Before
Steps = [
    new DrawCardsAction { Amount = 1, PlayerIdContextKey = ContextKeys.CastingPlayerId },
    new LoseLifeAction  { Amount = 1, PlayerIdContextKey = ContextKeys.CastingPlayerId },
]

// After
Steps = [
    new DrawCardsAction { Amount = 1, TargetContextKey = ContextKeys.CastingPlayerId },
    new LoseLifeAction  { Amount = 1, TargetContextKey = ContextKeys.CastingPlayerId },
]
```

---

### Dark Confidant (reveal top card, lose life equal to mana cost — pipeline)
Dynamic amount still uses `AmountContextKey`. Player targeting switches to `TargetContextKey`.
Note: the current implementation incorrectly uses `GainLifeAction` here — it should be `LoseLifeAction`.

```csharp
// After (corrected)
Steps = [
    new RevealTopCardAction {
        PlayerIdContextKey = ContextKeys.CastingPlayerId,  // data action — unchanged
    },
    new LoseLifeAction {
        TargetContextKey = ContextKeys.CastingPlayerId,
        AmountContextKey = ContextKeys.RevealedCardManaCost,
    },
    new DrawCardsAction {
        Amount           = 1,
        TargetContextKey = ContextKeys.CastingPlayerId,
    },
]
```

---

### New card: "Deal 3 damage to target player, that player draws 3 cards"
`DealDamageAction` outputs the damaged player ID. `DrawCardsAction` reads it via `TargetContextKey`.

```csharp
// CardEffect.TargetingStrategy = SingleTarget(Players) — selects target at cast time
// DealDamageAction receives target via ITargetedAction.WithTargets (EffectAction base)

Steps = [
    new DealDamageAction {
        Amount          = 3,
        PlayerOutputKey = "damaged_player_ids",
    },
    new DrawCardsAction {
        Amount           = 3,
        TargetContextKey = "damaged_player_ids",
    },
]
```

---

## Implementation Steps

1. **Add `EffectAction` abstract base record** to `MtgCore/Actions/` — inherits `GameAction`,
   implements `ITargetedAction`, contains `TargetContextKey`, `AmountContextKey`, `TargetIds`,
   `WithTargets()`, `ResolveTargetIds()`, `ResolveAmount()`, and default `ValidateResolve`.

2. **Migrate effect actions** to inherit from `EffectAction` and remove their individual boilerplate:
   `DealDamageAction`, `GainLifeAction`, `LoseLifeAction`, `DrawCardsAction`, `DiscardCardsAction`,
   `AddTemporaryManaAction`, `ExileAction`, `DestroyCreatureAction`, `AddModifierAction`.

3. **Add output key properties** to `DealDamageAction`: `PlayerOutputKey`, `CreatureOutputKey`.
   Write resolved target IDs to `ActionResult` under those keys after execution.

4. **Update card definitions** in `CardLibrary` and `CardPool`:
   - Standalone draw/mana effects: replace `NoTarget() + PlayerIdContextKey` with `Self()`
   - Pipeline steps: replace `PlayerIdContextKey` with `TargetContextKey`
   - Fix Dark Confidant: replace `GainLifeAction` with `LoseLifeAction`

5. **Update tests** — any test constructing affected actions will need the `PlayerIdContextKey`
   references replaced.

6. **Verify `TargetingStrategy.Self()`** maps to `TargetSelectionMode.CastingPlayer` in
   `ResolveEffectAction` (already present — confirm no rename needed).
