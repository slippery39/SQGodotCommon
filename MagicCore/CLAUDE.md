# MagicCore — real Magic rules (branch `mtg-real`)

Its own thing, like KIN: real phases, priority, the stack, blocking, lands that tap for mana.
**Arena-accurate, Bo1 only** — comprehensive-rules corners wait until a card needs them.
First set: **Reality Fracture (FRA)**.

`MtgCore` is the simplified deckbuilder engine and is **not modified** for this work. Copy what fits
from it (zones, targeting, triggers, card builder, AI search) and own the copy — the two rule sets
diverge on combat, mana and the stack, so shared code would fight both.

## Source map

```
MagicCore/
├── MagicRules.cs        THE API: NewGame (setup, coin flip, Bo1 hand smoother), DecidingPlayerId,
│                        LegalActions, Act, Choose, AutoPass
├── MagicGame.cs         root object (active player, turn, Step, priority, passes) + Step + MagicKeys
├── MagicPlayer.cs       life, mana pool, land drops, mulligan state, loss flags
├── MagicState.cs        queries + building-block moves: zones, Draw, Shuffle, seeded TakeRng
├── Turns/TurnStructure.cs  EnterStep/EndStep/GivePriority + StateBasedActions
├── Turns/Combat.cs      who may attack/block, declaration pipelines, simultaneous damage, end of combat
├── Actions/             PassPriority, PlayLand, TapForMana; CastSpell + StackResolution;
│                        Keep/Mulligan; hand choices (bottom, discard); per-creature attack/block choices
├── Effects/Effects.cs   SpellEffectsComponent + effect primitives (draw, gain life, opponent loses)
├── Cards/Card.cs        Card (types, cost, keywords, P/T, permanent + combat state, Fresh()),
│                        CardDefinition (data + ability components), CardFactory
├── Cards/CardData.cs    printed card data (Scryfall) + CardDataSet.Load("FRA")
├── Mana/ManaPool.cs     W U B R G C counts — ONE track, unlike MtgCore
├── Mana/ManaCost.cs     parsed "{2}{W/U}{2/G}{X}"; ManaValue
├── Mana/ManaPayment.cs  Arena-style auto-pay: floating first, then lands; backtracking search
├── Zones/Zone.cs        per-player zones + shared Stack; per-CONTROLLER battlefield
└── Sets/FRA.json        imported by tools/import_scryfall_set.py — embedded resource, never hand-edit
```

A front end's loop: `DecidingPlayerId` → if `GetPendingChoice()` answer with `Choose`, else pick from
`LegalActions` and `Act` — until `IsOver`.

## Rules that hold everywhere

- **Legality lives in `ValidateAdd` only.** `LegalActions` enumerates candidates and keeps those that
  validate — never re-derive a rule in the generator.
- **`TurnStructure` is the only place a step changes or priority is given.** `GivePriority` runs the
  state-based check first (CR 117.5); triggers will go on the stack at that same point.
- **`PriorityPlayerId` is 0 while nobody holds priority** (pregame, untap, cleanup, pending choice,
  game over). `Priority.Holds` rejects a 0 caller — it once matched, and "player 0" could pass.
- **Child index 0 is the TOP** of the library and of the stack; `MoveObject` appends (= bottom),
  `MoveObjectToFront` pushes.
- **Every zone change goes through `MagicState.MoveCard`** (or `EnterBattlefield`), which calls
  `Card.Fresh()` — CR 400.7, a card that moves is a new object. Damage, tapped, combat state and
  stack choices die there; a raw `MoveObject` off the battlefield keeps them. Only `Shuffle` uses
  `MoveObject`, to reorder within one zone.
- **`MagicRules.Choose` checks WHICH ids, `ResolveChoice` only how many.** Distinct and offered, or it
  throws — a duplicate once kept an extra card through a mulligan.
- **Combat declarations are one choice per creature**, not one pick among subsets — linear for the AI.
  Option id 0 is "don't". Damage assignment ORDER is gone from the rules (Foundations, Nov 2024); the
  split among blockers is automatic for now (`Combat.DealDamage`).
- **`ManaPayment.Find` is the only cost solver** — casting validates and pays through the same call.
- Mana abilities are not in `LegalActions` (pure AI branching); casting taps what it needs.
- **A `CardDefinition` with no components plays vanilla, silently.** Right for a vanilla creature,
  wrong for everything else — test that an ability FIRES, not that the card builds.
- `AutoPass` passes only while Pass is the sole legal action. It is a convenience for front ends and
  the AI, never part of the rules: `LegalActions` stays complete.
- Card abilities are hand-written with a builder; **name, cost, type, P/T and rules text come from
  `CardData`**, never retyped into code.
- Same serialization rule as the root `CLAUDE.md`: nothing with a delegate lives in `GameState`.
  `CardData` is static reference data and stays out of `GameState`.

## What FRA demands (read the rules text before building each)

Keywords by card count: Surveil 43, **Empower Jace 35** (creates/loads a Jace *planeswalker token* —
planeswalkers are needed early), **Prepared 24** (21 cards use the two-face `prepare` layout),
Flying 28. The Bo1 hand smoother is Arena's published description (two shuffles, closer land count wins);
the exact weighting and whether limited Bo1 uses it are unconfirmed — hence `handSmoothing`. Costs include hybrid `{W/U}`, `{2/W}` and `{X}`. 78 legendary creatures (legend rule).
