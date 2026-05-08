# Implementation Plan

## Plan 1 — Architecture Cleanup

### Step 1: Investigate HasSummoningSickness / Haste inconsistency

The main path looks correct (`CreatureEvaluator.cs`, `AttackAction.cs`, `StartTurnAction.cs`). The inconsistency flagged in the ideas doc may be more subtle — possibly specific cards, ETB scenarios, or equipment granting Haste. **Before doing anything else here, read those three files together and identify exactly what's wrong.** This step is purely investigative.

### Step 2: Normalize LoseLife / DealDamage / GainLife

Current state:
- `LoseLifeAction` — non-targeted, ContextKey-driven
- `DealDamageAction` — `ITargetedAction`, no ContextKey support for amount
- `GainLifeAction` — `ITargetedAction` *and* ContextKey override (both, conflicting)

Target outcome: `DealDamageAction` and `GainLifeAction` both use `ITargetedAction` cleanly (TargetIds only). `LoseLifeAction` stays non-targeted but gets `AmountContextKey` parity. Remove the `PlayerIdContextKey` path from `GainLifeAction` — callers should inject via TargetIds.

### Step 3: Clarify targeting consolidation

The canonical pattern is `ITargetedAction` with `ImmutableList<int> TargetIds` (loop through targets). **This step is documentation only** — add a short note in `ITargetedAction.cs` (or equivalent) that the loop-through-targets pattern is canonical, so the question doesn't come up again.

### Step 4: Fix EndTurnAction commented-out compensation

Lines 55-56 in `EndTurnAction.cs` (Player 2 first-turn bonus mana/draws) are disabled. Read those lines and decide: re-enable with a test, or add a comment explaining why they're off.

### Step 5: ContextKey interface enforcement

Add a marker interface `IContextKeyConsumer` (or similar) that actions opt into, so callers know at a glance whether an action respects runtime context injection.

### Step 6: Draw investigation

10 draws in 100,000 simulator games with no flagging. Add draw-detection logging to `SimulatorRunner.cs` (log the final game state when result is Draw), then run and inspect. Likely a Blood Artist loop or similar.

---

## Plan 2 — Foundational & Keyword Mechanics

**Prerequisite from Plan 1:** Steps 2 and 3 (action normalization and targeting clarity) should be done first.

### Step 1: Lands

Lands are permanent cards that skip the stack — placed directly onto the battlefield with no resolution step.
- Add `IsLand` flag to card data (or a `LandCardType` variant)
- `PlayLandAction` — bypasses `ResolveSpellAction`, goes straight to battlefield
- One-land-per-turn rule: add `LandsPlayedThisTurn` counter to `MtgGame`, reset in `StartTurnAction`
- Lands tap for mana (activated ability: tap → add mana). This means mana is no longer automatic — this is a significant break from the current mana model.

> **Open question:** Do we want to fully replace the automatic mana system with land-based mana, or keep both and make it deck-configurable? This decision significantly affects scope.

### Step 2: Zone-aware triggers

Currently `CheckStateBasedEffectsAction.cs` only evaluates triggers on battlefield permanents. Needed for Flashback (graveyard), Cycling (hand), and Wonder-style static effects.
- Add a `TriggerZone` property to `TriggeredAbilityComponent` (enum: Battlefield, Hand, Graveyard, Any)
- Update `EvaluateTriggeredAbilities` to scan the appropriate zones based on that property
- Default: `Battlefield` — no behavior change for existing cards

### Step 3: Hexproof / Shroud

Only applies when a **player selects** a target, not when the game auto-applies an effect (e.g. Pyroclasm, Wrath of God).
- Add `HasHexproof` / `HasShroud` to `CreatureComponent` (and on players)
- Add `IsPlayerInitiated` flag to `TargetingContext`
- Hexproof/Shroud checks in `IsSatisfiedBy` only fire when `IsPlayerInitiated = true`

### Step 4: Lifelink / Trample

Both are combat keywords that hook into damage resolution.
- **Lifelink**: after damage is dealt, attacker's controller gains equal life. Hook into `DealDamageAction.ApplyToCreature/ApplyToPlayer`, check if source creature has Lifelink.
- **Trample**: if total power exceeds blocking creature's toughness, excess deals to player. Logic lives in combat resolution.

Depends on Plan 1 Step 2 (normalized damage/life actions).

### Step 5: X Spells

- Add `UsesXCost` flag to card mana cost data
- When `UsesXCost = true`, the cast action sets X = all available current mana
- Mana cost paid = base cost + X
- X is injected into `InputContext` via ContextKey for effect resolution

### Step 6: Extra Turns

- Add `ExtraTurnQueue: ImmutableQueue<int>` (player IDs) to `MtgGame`
- In `EndTurnAction`, before switching `ActivePlayerId`, dequeue from `ExtraTurnQueue` if non-empty
- Extra turn cards push to the queue
- Skip-opponent-turn variant is out of scope (treat as extra turn for simplicity)

### Step 7: Scripted Keyword Framework

Keywords like Cycling, Cascade, and Storm are named triggered/activated abilities.
- Add `KeywordDisplayName` (string) to `TriggeredAbilityComponent` and `ActivatedAbilityComponent`
- If set, the UI/console renderer shows the keyword name instead of the full ability text
- No engine changes needed — display concern only

**Cycling** (activated ability: discard → draw) is straightforward once the framework exists.
**Cascade** (triggered on cast: reveal + play first card with lower CMC) requires zone-aware triggers (Step 2) and some library manipulation.

### Step 8: Flashback

Activated ability from the graveyard: pay flashback cost → cast the card.
- Depends on zone-aware triggers (Step 2)
- Requires a "cast from graveyard" variant of `PlayCardAction`
- Card is exiled after resolution rather than returning to the graveyard

---

## Plan 3 — AI / Simulator

### Step 1: AI "end turn" as a voluntary move

Currently `SimulatorRunner.cs` auto-fires `EndTurnAction` when no legal actions remain — the AI cannot choose to end early. Add `EndTurnAction` to the legal action list in `MtgActionGenerator.cs` (always available during main phase). `BeamSearchAiStrategy` will naturally consider it and pick it when ending scores higher than other moves.

### Step 2: Draw investigation & flagging

Add draw-detection logging to `SimulatorRunner.cs` (log the final game state when result is Draw), then run and inspect. (See also Plan 1 Step 6.)

### Step 3: Value Over Time evaluator

Sandbox simulation approach:
- Add `TheoreticalValueEvaluator` class
- For a given card, run a contained mini-simulation: place the card on an otherwise-empty board (or a board with dummy opponent creatures), end turn N times, score the resulting board delta
- Cache result per card definition ID — lazy, compute once per card type per game session
- Combine with the existing `StateEvaluator` score via a configurable weight
- Add a manual `DangerScore` override field on card data as an escape hatch for edge cases

Limitation: does not account for runtime modifications (equipment, enchantments). Baseline theoretical value without modifications is acceptable for a first pass. Runtime modifications could recalculate lazily when a modification event fires.

---

## Suggested sequencing

```
Phase 1 (Architecture):  Steps 1 → 6
Phase 2 (Mechanics):     Lands → Zone triggers → Hexproof/Shroud → Lifelink/Trample → X Spells → Extra Turns → Scripted Keywords → Flashback
Phase 3 (AI):            End turn move → Draw investigation → Value Over Time
```
