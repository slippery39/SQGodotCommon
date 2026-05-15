# Zone-Dependent Statics — Design Spec

**Target card:** Wonder (gives all your creatures flying while Wonder is in your graveyard)

---

## Overview

Static abilities are currently only active when the source is on the battlefield.
Zone-dependent statics let a card's static ability be active from a different zone — typically the graveyard (Wonder, Anger, etc.) or the hand (Future Sight-style effects).

---

## 1. `StaticAbilityComponent` — Add `ActiveInZone`

Add one property to the abstract base:

```csharp
public abstract record StaticAbilityComponent : GameComponent
{
    public TargetSpecification Filter { get; init; } = new AlwaysFalseSpecification();
    public ImmutableHashSet<int> AffectedIds { get; init; } = ImmutableHashSet<int>.Empty;

    // NEW — which zone this static is active in. Default keeps all existing cards unchanged.
    public ZoneType ActiveInZone { get; init; } = ZoneType.Battlefield;
}
```

Example Wonder definition:

```csharp
new StaticGrantKeywordAbility
{
    GrantsFlying = true,
    ActiveInZone = ZoneType.Graveyard,
    Filter = new AndSpecification(
        new IsOnBattlefieldSpecification(),
        new IsCreatureSpecification(),
        new IsControlledByYouSpecification()
    ),
}
```

---

## 2. New Event — `CardEnteredGraveyardEvent`

`PermanentLeftBattlefieldEvent` doesn't carry destination info — you can't tell from it whether a card went to the graveyard or was exiled. A distinct event is needed.

```csharp
public record CardEnteredGraveyardEvent : GameEvent
{
    public int CardId { get; init; }
    public int OwnerId { get; init; }
}
```

**Where to fire it** (add alongside existing graveyard moves):
- `AttackAction.ApplyDamageToCreature` — after `MoveObject(..., graveyardId)` when lethal damage
- `DestroyCreatureAction.Execute` — after move to graveyard
- `DiscardCardsAction.Execute` — after each card moved to graveyard

Do NOT fire it when a card is exiled (even if it came from the battlefield). The exile path should gain an `CardEnteredExileEvent` later if needed.

---

## 3. `StaticAbilityEngine` — Handle Graveyard Sources

### New entry points (parallel to existing battlefield ones)

```csharp
// Called from CheckStateBasedEffectsAction when CardEnteredGraveyardEvent is seen
public static GameState ProcessZoneSourceEntered(GameState state, int cardId, int gameId)
{
    var card = state.GetObject(cardId) as Card;
    if (card == null || !card.HasComponent<StaticAbilityComponent>())
        return state;

    // Only register the card as a source if any of its static abilities have ActiveInZone matching current zone
    // ... register in StaticSourceIds, stamp effects onto matching permanents
}

// Called from CheckStateBasedEffectsAction (if needed) when card leaves graveyard
public static GameState ProcessZoneSourceLeft(GameState state, int cardId, int gameId) { ... }
```

### `MtgGame.StaticSourceIds` — already works

`StaticSourceIds` is an `ImmutableHashSet<int>` of card IDs whose static abilities are currently active. It can hold graveyard sources too — no type change needed.

The existing `ProcessPermanentEntered` and `ProcessPermanentLeft` should be restricted to `ActiveInZone == ZoneType.Battlefield` abilities to avoid double-applying.

### `ApplySourceToTarget` — no changes needed

`ApplySourceToTarget` already reads the source card's `StaticAbilityComponents` and stamps effects. It doesn't care what zone the source is in — the caller is responsible for only calling it when the source is active.

---

## 4. `CheckStateBasedEffectsAction` — Wire the New Events

In `ProcessStaticAbilityUpdates`, add handling for `CardEnteredGraveyardEvent`:

```csharp
foreach (var evt in state.PendingGameEvents.OfType<CardEnteredGraveyardEvent>())
{
    state = StaticAbilityEngine.ProcessZoneSourceEntered(state, evt.CardId, gameId);
}
```

The existing `PermanentLeftBattlefieldEvent` handler also needs to remain for battlefield-source cleanup — a card with a graveyard static that died needs its battlefield-active static (if it had one) removed, AND its graveyard static registered. These are two separate concerns.

---

## 5. Design Gap — Battlefield Source Dying

When a card on the battlefield has **both** a battlefield-active static AND a graveyard-active static (unusual, but possible), the sequence is:
1. `PermanentLeftBattlefieldEvent` → `ProcessPermanentLeft` removes battlefield static effects
2. `CardEnteredGraveyardEvent` → `ProcessZoneSourceEntered` registers graveyard static effects

This ordering is safe as long as `CheckStateBasedEffectsAction` processes `PermanentLeftBattlefieldEvent` before `CardEnteredGraveyardEvent`.

---

## 6. Wonder Card Definition (target implementation)

```csharp
public static Card Wonder() => new Card
{
    Name = "Wonder",
    ManaCost = 4,
    Subtypes = ImmutableList.Create("Creature", "Illusion"),
    Components = ImmutableList.Create<GameComponent>(
        new PermanentComponent(),
        new CreatureComponent { Power = 2, Toughness = 2 },
        new StaticGrantKeywordAbility
        {
            GrantsFlying = true,
            ActiveInZone = ZoneType.Graveyard,
            Filter = new AndSpecification(
                new IsOnBattlefieldSpecification(),
                new IsCreatureSpecification(),
                new IsControlledByYouSpecification()
            ),
        }
    ),
};
```

---

## 7. Open Questions Before Implementing

- **Exile vs. graveyard** — `AttackAction.ApplyDamageToCreature` currently always moves to graveyard (no indestructible/exile effects). Fine for now; add `CardEnteredExileEvent` later when exile becomes a destination.
- **Card leaves graveyard** — if Wonder is shuffled back into the library or cast from the graveyard, its graveyard static must be removed. `ProcessZoneSourceLeft` needs to be called from wherever a card leaves the graveyard. (No current action does this — needs a new event or hook.)
- **Multiple graveyard statics** — the `AffectedIds` mechanism on `StaticAbilityComponent` works the same regardless of source zone, so there's no structural change needed there.
