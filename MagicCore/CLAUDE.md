# MagicCore — real Magic rules (branch `mtg-real`)

Its own thing, like KIN: real phases, priority, the stack, blocking, lands that tap for mana.
**Arena-accurate, Bo1 only** — comprehensive-rules corners wait until a card needs them.
First set: **Reality Fracture (FRA)**. Play it: `dotnet run --project MagicConsole` (Commands.md).

`MtgCore` is the simplified deckbuilder engine and is **not modified** for this work. Copy what fits
from it and own the copy — the two rule sets diverge on combat, mana and the stack.

## Source map

```
MagicCore/
├── MagicRules.cs        THE API: NewGame, DecidingPlayerId, LegalActions, Act, Choose, AutoPass,
│                        DistinctMoves
├── MagicGame.cs / MagicPlayer.cs / MagicState.cs   root object, player, queries + MoveCard/EnterBattlefield
├── Describe.cs          plain-text actions, choices and cards for every front end and log
├── Turns/TurnStructure.cs  EnterStep/EndStep/GivePriority + StateBasedActions (incl. legend rule, tokens)
├── Turns/Combat.cs      who may attack/block, declarations, simultaneous damage, end of combat
├── Turns/Triggers.cs    trigger events, Collect (APNAP), PutOnStack with target choices
├── Actions/             PassPriority, PlayLand, TapForMana; CastSpell, ActivateAbility, StackAbility,
│                        StackResolution (fizzle); mulligan; hand, library, combat and target choices
├── Actions/LoyaltyActions.cs  Loyalty.AbilitiesOf (own + granted) and ActivateLoyaltyAction
├── Actions/ModeChoices.cs     ChooseModesChoice — a modal trigger's modes, chosen as it goes on the stack
├── Effects/Effects.cs   ContextKeys, Ref (you / opponent / target i / source / event card …), primitives
├── Effects/Abilities.cs spell, triggered, activated, mana and static ability components
├── Effects/Modes.cs     Mode, Modal.Compose (chosen modes → one target list + steps), ScopeTargetsAction
├── Targeting/TargetSpec.cs  one legality test for choosing AND resolving; Combinations
├── Rules/Damage.cs      THE damage path (lifelink, deathtouch) — combat and effects alike
├── Rules/Costs.cs       THE effective cost (taxes, reductions)
├── Rules/Life.cs        THE life-gain path — raises the event "whenever you gain life" reads
├── Cards/Card.cs        Card (computed P/T + keywords, permanent/combat state, Fresh()), CardFactory
├── Cards/CardData.cs    printed data (Scryfall) + CardDataSet.Load("FRA")
├── Mana/                ManaPool, ManaCost (parse, WithGeneric), ManaPayment (solver), ManaAbilities
├── Sets/Fra/FraCards.cs hand-written FRA abilities by name; IsImplemented
├── Sets/Fra/EmpowerJace.cs  the keyword action and the Jace token
├── Sets/Fra/FraDecks.cs 40-card decks of implemented cards (console, fuzz tests)
├── Ai/GreedyAi.cs       one-ply simulate-and-evaluate AI
├── Zones/Zone.cs        per-player zones + shared Stack; per-CONTROLLER battlefield
└── Sets/FRA.json        imported by tools/import_scryfall_set.py — embedded resource, never hand-edit
```

A front end's loop: `AutoPass` → `DecidingPlayerId` → if `GetPendingChoice()` answer with `Choose`,
else pick from `LegalActions` and `Act` — until `IsOver`.

## Rules that hold everywhere

- **Legality lives in `ValidateAdd` only.** `LegalActions` enumerates candidates and keeps those that
  validate — never re-derive a rule in the generator.
- **`TurnStructure` is the only place a step changes or priority is given.** `GivePriority` collects
  triggers, runs state-based actions, then puts the triggers on the stack (CR 117.5). Triggers are
  collected BEFORE the check, so a creature the check kills still saw its own entry.
- **`PriorityPlayerId` is 0 while nobody holds priority** (pregame, untap, cleanup, pending choice,
  game over). `Priority.Holds` rejects a 0 caller — it once matched, and "player 0" could pass.
- **Child index 0 is the TOP** of the library and of the stack; `MoveObject` appends (= bottom).
- **Every zone change goes through `MagicState.MoveCard`** (or `EnterBattlefield`), which calls
  `Card.Fresh()` — CR 400.7. Damage, tapped, counters, until-end-of-turn effects, combat state and
  stack choices die there. Only `Shuffle` uses `MoveObject`, to reorder within one zone.
- **One computation each, never a second copy:** `Card.Power`/`Toughness`/`Has` (printed + counters +
  until-end-of-turn; rules never read `BasePower`), `Damage.Deal` (players, creatures, planeswalker
  loyalty), `Life.Gain`, `Costs.Of`, `ManaPayment.Find`, `ManaAbilities.CanTap`/`CanPayTapCost`,
  `TargetSpec.IsLegal` (on choosing AND resolving), `Loyalty.AbilitiesOf` (own + granted),
  `Loyalty.Change` (costs, damage, effects; never below 0), `Ref` (what "creatures you control"
  means — `Count` counts a `Ref`, it does not filter again).
- **`EnterBattlefield` is the one way onto the battlefield** and owns the entry: controller,
  summoning-sickness stamp, starting loyalty, entering tapped (`EntersTappedComponent`), the event.
  The exception is test setup: `TestGames.Put(…, Battlefield)` places a card with NONE of that —
  untapped, unsick, 0 loyalty. Use `EnterBattlefield` in a test when the entry itself matters.
- **Planeswalkers:** enter with printed loyalty (`EnterBattlefield`), loyalty abilities are sorcery
  speed and once per turn per permanent, damage removes loyalty, 0 loyalty is a state-based death,
  and the defending player's planeswalkers are attack options. Empower Jace creates and loads the
  token in ONE resolution, so a fresh token never meets the check at 0.
- **Modes (CR 700.2):** a modal text is its `Modes` + `Choose`. `Modal.Compose` is the ONE place the
  chosen modes become target words and steps, in printed order; each mode's effects see only its own
  targets (`Target(0)` is that mode's first). A spell's modes are chosen when cast (`CastSpellAction.
  Modes`), a trigger's as it goes on the stack. A mode with an unchoosable required target can't be
  chosen. The scripted mode count must match the printed bullets — `FraCards` throws otherwise.
- **Base P/T effects** ("has base power and toughness 4/5") sit under counters and pumps, latest
  wins (`UntilEndOfTurn.SetBase`, read only in `Card.Power`/`Toughness`).
- **A spell or ability whose targets are ALL illegal on resolution does nothing** (CR 608.2b) — the
  drain on Extended Absence included. Partially illegal: those targets read as 0 to the effects.
- **`MagicRules.Choose` checks WHICH ids, `ResolveChoice` only how many.** Distinct and offered.
- **Combat declarations are one choice per creature**, not one pick among subsets. Option id 0 is
  "don't". Damage assignment ORDER is gone from the rules (Foundations, Nov 2024); the split among
  blockers is automatic for now (`Combat.DealDamage`).
- Mana abilities are not in `LegalActions` (pure AI branching); casting taps what it needs.
- **A card plays only if `FraCards.IsImplemented`** — scripted, a basic land, or text that is only
  engine keywords. Anything else plays silently vanilla. **Every scripted card has a test in
  `FraCardTests` that its ability FIRES**, through real casting and the real stack.
- Card abilities are hand-written as data (`FraCards`); **name, cost, type, P/T and keywords come from
  `CardData`**, never retyped. Test cards are inline (`TestGames.Creature/Inline`).
- **A card test asserts the PRINTED text's numbers** (Last Gasp is -3/-3) — that is the root rule's
  exception, not a breach: printed cards get no balance passes, and reading the number back from the
  script would make the test check the script against itself and miss a transcription error.
- `AutoPass` and `DistinctMoves` are conveniences for front ends and the AI, never rules:
  `LegalActions` stays complete.
- **AI evaluation terms are MONOTONIC** — more of a good thing always scores more, tapering not
  capping. A cap makes "more" tie with "less", and a tie silently picks the passive move: a life cap
  at 20 stopped every attack past tapped blockers; a land cap at 7 made the AI hoard lands.
- Same serialization rule as the root `CLAUDE.md`: nothing with a delegate lives in `GameState`.
- **Test trap:** helpers that replace a `GameState` field (`Hand`, `Field`) must run on their own line —
  in `s.Act(new … { CardId = Hand(…) })` C# reads `s` before `Hand` replaces it.

## Known ceilings (each marked `ponytail:` in the code)

Damage split among blockers is automatic · the legend rule keeps the newest · triggers read sources on
the battlefield NOW (dies/leaves triggers need a look-back) · state-based actions run one pass ·
`GreedyAi` is one ply with a passive opponent — no turn planning, no holding up mana, no "do I need
this mana now" when choosing a tapped dual, and it loses slow races to evasive fliers
(`docs/findings/magic-ai.md`) · menace drops a
lone blocker instead of refusing the declaration · the auto-tapper ignores what the rest of the hand
needs · Empower Jace with two Jace tokens loads the first · surveil keeps the kept cards' order.

## What FRA demands (read the rules text before building each)

Keywords by card count: Surveil 43, **Empower Jace 35** (creates/loads a Jace *planeswalker token*),
**Prepared 24** (21 cards use the two-face `prepare` layout), Flying 28. Modal "choose one", flashback,
cycling/landcycling, ward, equipment and +1/+1 counters are common. Implemented so far: 63 cards
(`FraDecks`), including 15 Empower Jace cards, the 15 dual lands and 14 modal cards. **The 8 real planeswalkers are
not**: each needs an emblem, an "until your next turn" effect, impulse draw or a −X cost first.
Coverage plan, by cards unblocked: 7a lands ✓ · 7b-1 modal ✓ · 7b-2 sacrifice, dies triggers, exile-until-leaves · 7c continuous
effects, step triggers, prowess · 7d prepare, flashback, cycling, graveyard abilities. The Bo1 hand smoother is Arena's published description; its weighting and whether
limited Bo1 uses it are unconfirmed — hence `handSmoothing`.
