# MtgCore — MTG Card Game Engine

## Where the detail lives

This file is the map and the always-true rules. Detail loads **only when you open a matching file**:

| Topic | File |
|---|---|
| Keywords, combat, mana, replacement effects, triggers, targeting, **annotated source map** | `.claude/rules/mtg-mechanics.md` |
| Card builder API, per-colour Core Set Cube mechanics, creation cookbook | `.claude/rules/mtg-cards.md` |
| Adding a card, step by step | the `add-card` skill (`/add-card`) |
| Godot rendering of cards and boards | `.claude/rules/mtg-presentation.md` |
| Colours, pips and the manabase tables | `docs/mtg/colours.md` |
| Card sets and the set menu | `docs/mtg/card-sets.md` |

**The root `CLAUDE.md` no longer describes MTG** — the repo's active project is DOOMJAM. Everything
MTG that used to live there is in `docs/mtg/`, listed above and in `MtgSimulator/CLAUDE.md`.

## Source Map

Directory level only. The annotated version — every non-obvious design decision in this tree — is
the "Source Map (detailed)" section of `.claude/rules/mtg-mechanics.md`.

```
MtgCore/
├── Abilities/Activated/  ActivatedAbilityComponent + its ActivationCondition gate
│   └── Static/           StaticAbilityComponent, ActiveInZone, AppliedKeywordComponent.Duration
├── Actions/              (81 files) every GameAction; ContextKeys; MtgActionGenerator
├── Cards/                Card, CardLibrary, CardType
│   ├── Builders/         fluent card builder API — CardFactory is the entry point
│   └── Components/       (32) PermanentComponent, CreatureComponent, EquipmentComponent, …
├── Costs/                AdditionalCost and its subclasses
├── Effects/              CardEffect (data-only effect descriptor)
├── Emblems/              player-owned persistent triggered abilities
├── Events/               EventTypeNames, MtgEvents
├── Extensions/           the four ENGINES — CostEngine, ManaEngine, ReplacementEngine,
│                         StaticAbilityEngine — plus CreatureEvaluator
├── Mana/                 ManaColor, ManaPool
├── Modifiers/            PowerToughnessModifier and the dynamic P/T components
├── Players/              MtgPlayer
├── Sets/                 CardSet, SetRegistry; CoresetCube/ (408 cards), ComboProving/
├── Targeting/            TargetSpecification and every spec
├── Triggers/             TriggeredAbilityComponent and every trigger condition
├── Turns/                BeginGame/Setup/StartTurn/EndTurn, TurnPhase
├── Zones/                Zone, ZoneType, ZoneTransitionExtensions.MoveCardTracked
├── MtgGame.cs            core state (ActivePlayerId, TurnNumber, SpellsCastThisTurn)
├── MtgGameFactory.cs     Create() and CreateForTesting()
└── MtgGameStateExtensions.cs   API boundary — BeginGame, ShuffleLibrary, …
```

**Each of the four engines is the SINGLE place its thing happens** — `CostEngine` for effective
mana cost, `ManaEngine` for a land's mana reaching a player, `ReplacementEngine` for any replaceable
number, `StaticAbilityEngine` for stamped static effects. Every one of them exists because private
copies had already drifted. Never compute those inline.

**`ZoneTransitionExtensions.MoveCardTracked` — not `MoveObject` — for every zone change.** It emits
the graveyard-crossing and battlefield-leaving events and clears marked damage. Bypassing it makes
zone-dependent statics silently stop updating.

## ImmutableGameObjects Usage

All state changes go through `GameAction.Execute()` — never mutate directly. No delegates, lambdas,
or `Func<>`/`Action<>` on any type stored in `GameState`. `MtgGame`, `MtgPlayer`, `Card`, all
`GameAction` subclasses and all `GameComponent` subclasses must remain fully serializable. Pipeline
context key constants live in `Actions/ContextKeys.cs`.

## Card Effects Pattern

Cards are `GameObject` subclasses. Effects are `GameAction` subclasses — pure data with execution
logic in `Execute()`. No interpreter layer.

- **Simple fixed-value effects** (deal 3 damage, gain 3 life): plain actions, no pipeline.
- **Variable effects** where values are known only at resolution (Dark Confidant, Cruel Edict): `PipelineAction` to chain steps and pass context.
- **Targeting**: chosen upfront when playing a spell or activating an ability — no `ChoiceAction` on the stack for targeting. Only triggered abilities and "choose on resolve" use `ChoiceAction` mid-pipeline.
- **Mass effects** (Pyroclasm, Wrath): query game objects directly, spawn one action per target.
- **Restriction-based effects** (Smother): enforce at resolution in `ValidateResolve` or `Execute`.

## Card Creation

`CreateCardAction` creates `Card` objects and places them by spawning one `PutIntoBattlefieldAction`
per card. **`PutIntoBattlefieldAction` is the single entry point for all battlefield placement**,
whether a card moves from another zone or is created fresh; it owns the whole ETB ceremony.

- `ControllerId`: 0 reads from `InputContext[CastingPlayerId]`.
- `CountInputKey`: reads the count from pipeline context (Krenko via `CountCardsWithSubtypeAction`).
- `HasSummoningSickness` is stamped by `ApplyEtbCeremony` based on `HasHaste`.

## Game Startup

`MtgGameStateExtensions.BeginGame(state, gameId, player1Id, player2Id)` is the **single entry point
for all presentation layers.**

`BeginGame` → `BeginGameAction` → `SetupGameAction` (shuffles both libraries Fisher-Yates, deals
7-card hands) → `StartTurnAction` for Player 1 with `SkipDraw = true`. Presentation layers never
construct `BeginGameAction`, `SetupGameAction` or `StartTurnAction` directly; turn transitions are
handled entirely within MtgCore.

## Turn Structure

- **`StartTurnAction`** refills `CurrentMana = MaxMana` (does NOT auto-increment `MaxMana` — mana comes from lands), resets `LandsPlayedThisTurn`, optionally draws (`SkipDraw`), clears per-turn flags on the active player's permanents (`HasSummoningSickness`, `HasAttacked`, `HasActivated`, `UntilEndOfTurn` P/T modifiers). **`Damage` is NOT reset** — it persists across turns.
- **`EndTurnAction`** switches `ActivePlayerId`, increments `TurnNumber` when Player 2 ends, spawns the next `StartTurnAction`.
- Phases within a turn are not modelled — the turn is a single phase.
- Win/loss is checked by `CheckStateBasedEffectsAction` as the `PostActionProcessor` after every action (life ≤ 0, empty library).

## Presentation Layer Rules

- `MtgConsole` and `MtgSimulator` never construct game actions directly for game flow — use `MtgGameStateExtensions` as the API boundary.
- Presentation layers never modify game state directly. Exceptions: explicit test setup and debug/cheat tooling, both clearly commented as such.
- `MtgActionGenerator.GetLegalActions(state, ids, playerId)` is the **single shared source** of legal action generation. Never duplicate it.
- A UI highlighting legal attack targets must ask `AttackAction.ValidateAdd` per candidate (`MtgGameManager.GetLegalAttackTargets` does) rather than re-deriving Taunt/Flying/Reach. A second copy drifts, and the symptom is a click that silently does nothing.
- **The same rule applies to CONSTRUCTING an action, not just validating one.** `MtgGameManager` hand-built `CastPermanentAction` with no `TargetIds` and `CastFromGraveyardAction` with no `AdditionalCostPayments`, so a human could not play **any Aura** and could not recur Despoiler of Souls — while the AI did both, because it goes through `MtgActionGenerator`. **"The AI can do it and I can't" is the signature of this bug.** `PaymentsFor<T>` now reads payments back off the generator.
- A UI printing a card's mana cost **in hand** must ask `CostEngine.ComputeEffectiveCost`, never `Card.ManaCost` — Stormwing Entity read 5 in hand while costing 2, and was greyed out as unaffordable. On the battlefield the printed cost is correct: nothing is being paid.
- `CastSpellAction.TargetIds` / `CastFromGraveyardAction.TargetIds` are keyed by **effect index**, not 0. Keying them elsewhere makes the spell silently uncastable rather than throwing.
- `MtgGameFactory.CreateForTesting()` gives both players 99 mana of every colour. Use it in all tests not specifically about land/mana or colour; use `Create()` with manual land plays for those.

## Debugging / Error Handling

When a logic error is found, write an NUnit test to isolate it first. Do not assume the cause —
verify the assumption before proceeding. **An inert card throws no error**: test the consequence,
not the construction.

## Deferred / Future Work

Designed but not implemented. **Do not re-implement or work around these planned patterns.**

| Feature | Notes |
|------|-------|
| Keyword abilities as components | Lifelink, Deathtouch, Trample etc. as components. All keywords are currently flags on `CreatureComponent` — note the six-site rule under "First Strike" until then. |
| Vigilance | Deliberately unimplemented, not missing — with no blocking it has nothing to do. Eight CSC cards printed with it go without. Costed options in `DesignNotes.md`; `IsExhausted` was kept separate from `HasAttacked` so either stays cheap. |
| Structural replacement effects | `ReplacementModifierComponent` covers numeric only. "Enters tapped" / "exile it instead" needs an action-rewrite hook in the `ImmutableGameObjects` loop. Not built speculatively. |
| Conditional static abilities | `StaticAbilityEngine` only re-stamps on ETB/LTB, so an anthem gated on a changing value goes stale. Live-evaluated `PowerToughnessModifier`s dodge this for a single creature; a conditional TEAM anthem has no equivalent. |
| Double-faced planeswalkers | Kytheon's flip needs `TransformComponent` to swap a creature into a planeswalker, crossing the creature/permanent routing split. |
| Delirium / card-type counting | `CardType` now exists so it is expressible, but nothing counts distinct types in a graveyard. Deliberately cut in favour of Threshold. |
| Real Madness | Needs a priority window; the engine has a stack but no priority. Modelled instead as a graveyard-active `CardDiscardedEvent` trigger. |
| Deathtouch from effect damage | `DealDamageAction` always passes `fromDeathtouch: false`. Only combat deals deathtouch damage. Add `SourceHasDeathtouch` when a card needs a deathtouch ping. |
| Chosen creature type / naming a card | Adaptive Automaton, Phyrexian Revoker. No runtime-choice state a spec can read. Both reskinned. |
| Two competing `{T}` abilities on one permanent | `RequiresTap` is inert on a non-creature. See `DesignNotes.md`. |

**Completed since this table was written:** zone-dependent statics; from the white pass — tap costs
(`RequiresTap` now exhausts), first strike, indestructible, exalted, subtype protection, numeric
replacement effects, activation conditions, cast restrictions; from the colourless pass —
`AnimateAction`, charge counters, `PlayFromLibraryTopComponent`, `CannotLoseComponent`. **Colour is
now implemented** — see `docs/mtg/colours.md`.

**+1/+1 counters WERE a "won't do", and green fired the stated trigger.** Three green cards do
arithmetic on them — Primordial Hydra doubles, Wildwood Scourge reacts, Barkhide Troll enters with
one — so the system now exists. A permanent `AddModifierAction` is still right for a plain permanent
buff, and `HasPermanentPowerBonusSpecification` answers "did it have a counter" for both. **Use
`PlusOneCounterComponent` for anything whose counters are read back**: only it can be doubled,
removed or counted, and only it survives into the graveyard for a death trigger.
