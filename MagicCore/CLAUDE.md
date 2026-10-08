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
├── Turns/Triggers.cs    trigger events, Collect (APNAP, intervening "if"), PutOnStack with target choices
├── Actions/             PassPriority, PlayLand, TapForMana; CastSpell, ActivateAbility, StackAbility,
│                        StackResolution (fizzle); mulligan; hand, library, combat and target choices
├── Actions/LoyaltyActions.cs  Loyalty.AbilitiesOf (own + granted) and ActivateLoyaltyAction
├── Actions/ModeChoices.cs     ChooseModesChoice — a modal trigger's modes, chosen as it goes on the stack
├── Actions/PermanentChoices.cs  "you may sacrifice …": ChoosePermanentChoice, SacrificeChosen, IfChosen
├── Effects/Refs.cs      ContextKeys, Ref (you / opponent / target i / source / event card …), Count
├── Effects/Effects.cs   the Effect base; damage, life, counters, pumps, mana; If, May, PayMana
├── Effects/ZoneEffects.cs  effects that move cards: destroy, exile, sacrifice, return, discard, surveil, tokens
├── Effects/Abilities.cs spell, triggered, activated, mana and static ability components
├── Effects/Conditions.cs  "if …": Condition, AtLeast(Count, N), Not, target/event tests
├── Effects/Modes.cs     Mode, Modal.Compose (chosen modes → one target list + steps), ScopeTargetsAction
├── Targeting/TargetSpec.cs  one legality test for choosing AND resolving; Combinations
├── Rules/Damage.cs      THE damage path (lifelink, deathtouch) — combat and effects alike
├── Rules/Costs.cs       THE effective cost (taxes, reductions)
├── Rules/Characteristics.cs  THE power, toughness and keywords: own + every static on the battlefield
├── Rules/Prepare.cs     THE prepare rules: become prepared (a copy of the spell in exile), castable, unprepare
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
  triggers, runs state-based actions, and repeats while the check raised new events — so a creature
  the check kills has its death seen in the same pass — then puts the triggers on the stack (CR
  117.5). Triggers are collected BEFORE the check, so a creature it kills still saw its own entry.
- **`PriorityPlayerId` is 0 while nobody holds priority** (pregame, untap, cleanup, pending choice,
  game over). `Priority.Holds` rejects a 0 caller — it once matched, and "player 0" could pass.
- **Child index 0 is the TOP** of the library and of the stack; `MoveObject` appends (= bottom).
- **Every zone change goes through `MagicState.MoveCard`** (or `EnterBattlefield`), which calls
  `Card.Fresh()` — CR 400.7. Damage, tapped, counters, until-end-of-turn effects, combat state and
  stack choices die there. Only `Shuffle` uses `MoveObject`, to reorder within one zone.
- **One computation each, never a second copy:** `s.Power(card)`/`s.Toughness`/`s.Has` (Rules/
  Characteristics.cs — the card's own numbers plus every static ability on the battlefield; rules
  never read `Card.OwnPower`/`HasOwn` or `BasePower`, which ignore every anthem on the board), `Damage.Deal` (players, creatures, planeswalker
  loyalty), `Life.Gain`, `Costs.Of`, `ManaPayment.Find`, `ManaAbilities.CanTap`/`CanPayTapCost`,
  `TargetSpec.IsLegal` (on choosing AND resolving), `Loyalty.AbilitiesOf` (own + granted),
  `Loyalty.Change` (costs, damage, effects; never below 0), `Ref` (what "creatures you control"
  means — `Count` counts a `Ref`, it does not filter again), `MagicState.Sacrifice`,
  `Priority.AtSorcerySpeed` (sorceries, lands, loyalty, "activate only as a sorcery").
- **Same id is not same object (CR 400.7).** Ids survive zone changes; `Card.Incarnation` does not.
  "Is it still that permanent?" compares Incarnation — `Ref.Source` resolves to nothing once the
  source left, even if it came back, and an exile-until link names the incarnation that exiled.
- **`EnterBattlefield` is the one way onto the battlefield** and owns the entry: controller,
  summoning-sickness stamp, starting loyalty, entering tapped (`EntersTappedComponent`), entering
  with counters, the event.
- **`MoveCard` owns leaving it:** a creature or planeswalker going to a graveyard raises `DiedEvent`
  with the card AS IT LAST WAS (its counters, its controller — the card in the graveyard is a new
  object), and whatever it exiled "until it leaves" comes back at once (CR 610.3).
- **Sacrifice is a move to the owner's graveyard** — it dies. As a cost it is part of the action
  (`ActivateAbilityAction`/`CastSpellAction.SacrificeId`), chosen like a target, one legal action per
  choice; `StackAbility.Source` keeps the source as it was if the cost sacrificed it.
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
- **"At the beginning of [step]"** triggers on a `StepBeganEvent`, raised by `EnterStep` for upkeep,
  draw, beginning of combat and end. **"This turn" lives in `MagicPlayer.ThisTurn`** (a `TurnTally`:
  lands, spells, cards drawn, life gained, surveils), REPLACED whole for BOTH players as each turn
  begins — "this turn" is every turn, not only yours, and a new count cannot miss the reset. Update
  it with `s.Tally(…)`. The finished turn's tally becomes `LastTurn` ("last turn" = the turn before
  this one, whoever's). Creatures that died this turn (anyone's) are on `MagicGame`.
- **"This ability triggers only once each turn"** is `TriggeredAbilityComponent.OncePerTurn`, stamped
  on the card as it goes on the stack (`PutOnStack`) — a new object (CR 400.7) may trigger again.
- **"Entered this turn"** is `Card.EnteredOnTurn`, stamped by `EnterBattlefield` — not
  `ControlledSinceTurn`, which a control change would also move.
- **Where triggers are read from:** permanents on the battlefield; a dying card's own "when this
  dies"; a graveyard card's abilities marked `FromGraveyard` (Darklight Phoenix) — those, and only
  those, trigger from there; and a trigger granted until end of turn (`UntilEndOfTurn.Trigger`,
  Lyra), which ends at cleanup or a zone change like any other until-end-of-turn effect.
- **"Becomes the target"** is raised when a spell or ability goes on the stack with its targets —
  `CastSpellAction` and `StackResolution.Push` (every ability), the one place each.
- **Casting from the graveyard (flashback):** `Costs.Flashback` is the ONE answer to "may this card
  be cast from its graveyard, and for what" (its own flashback, or its mana cost when it gained
  flashback this turn); `Costs.Of` starts from it, so taxes still apply. The stack records where a
  spell came from (`OnStackComponent.CastFrom`), and `MoveCard` exiles a flashback spell whenever it
  would leave the stack — resolved, fizzled or bounced (CR 702.34a).
- **Prepare** (Secrets of Strixhaven's mechanic; FRA reuses it) lives in `Rules/Prepare.cs`. The card is
  a creature in every zone; its spell half (`PrepareSpellComponent`, the second face) is never cast
  from hand. Becoming prepared puts a real COPY of the spell in exile (`Card.IsCopy`, linked to the
  creature's id AND incarnation); its controller may cast it at the spell's own timing while that
  same creature is still prepared, and casting it unprepares the creature. A copy anywhere but the
  stack ceases to exist, unless it is a live prepared copy in exile (state-based, CR 704.5e).
- **Targets in a graveyard** are `TargetKind.CardInYourGraveyard` — the same legality test on choosing
  and resolving. "One or two target creatures" is two words, the second `Optional` and `NotThePrevious`.
- **"You may … If you do"** is `May` (a `YesNoChoice`, asked only when it can be done) or
  `MaySacrifice`; both clear the shared "chosen" key so a "no" cannot read a stale "yes".
- **An intervening "if" (CR 603.4) is `TriggeredAbilityComponent.If`**, never a bare `If` effect:
  checked as it triggers (`Collect`) AND again on resolution (`QueueTargets` wraps the effects).
  Edgar with a spell cast in response to his trigger gets nothing. A condition on the EVENT itself
  can't change before resolution, so it may be a `TriggerEvent` instead (Way of the Mind Sculptor's
  "removed two or more"). `If` runs its steps as a child pipeline — so a surveil inside it finishes
  before the next step — and its steps' outputs do not reach the steps after it.
- **Prowess is a scripted trigger** (`FraCards.Prowess`), not an engine keyword — each instance
  triggers on its own (Ruric Thar has two).
- **Static abilities are never stamped onto what they affect.** A `StaticEffectComponent` on its source
  is read live by Characteristics, so it stops the instant the source leaves — MtgCore's stamped
  statics went stale exactly there. "Can't be blocked" is a keyword-like grant (`Combat.CantBeBlocked`).
- **Base P/T effects** ("has base power and toughness 4/5") sit under counters and pumps, latest
  wins (`UntilEndOfTurn.SetBase`, read in `Card.OwnPower`/`OwnToughness` under `s.Power`/`Toughness`).
- **A spell or ability whose targets are ALL illegal on resolution does nothing** (CR 608.2b) — the
  drain on Extended Absence included. Partially illegal: those targets read as 0 to the effects.
- **`MagicRules.Choose` checks WHICH ids, `ResolveChoice` only how many.** Distinct and offered.
- **Combat declarations are one choice per creature**, not one pick among subsets. Option id 0 is
  "don't". Damage assignment ORDER is gone from the rules (Foundations, Nov 2024); the split among
  blockers is automatic for now (`Combat.DealDamage`).
- Mana abilities are not in `LegalActions` (pure AI branching); casting taps what it needs.
- **A card plays only if `FraCards.IsImplemented`** — scripted, a basic land, or text that is only
  engine keywords. Anything else plays silently vanilla. **Every scripted card has a test in
  a `Sets/Fra*Tests` file that its ability FIRES** (`FraCardTests`, or the batch's own file —
  `FraTriggerTests`, `FraStaticTests` …), through real casting and the real stack.
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
- **CSharpier 0.29 misreads `x is SomeType ? [a] : b`** — it takes `SomeType?` for a nullable type.
  Valid C#, but the commit hook then skips formatting the whole file. Write a `switch` expression.
- **Test trap:** helpers that replace a `GameState` field (`Hand`, `Field`) must run on their own line —
  in `s.Act(new … { CardId = Hand(…) })` C# reads `s` before `Hand` replaces it.

## Known ceilings (each marked `ponytail:` in the code)

Damage split among blockers is automatic · triggers are matched against the battlefield at the next priority, so a
source, or an entering creature, that is gone by then — destroyed later in the same resolution — is
missed (one the state-based check kills is still seen) · the legend rule keeps the newest · last-known information keeps a card's own characteristics, not what statics gave it · a dying card's own
"when this dies" looks back, but other leaves-the-battlefield triggers read the battlefield NOW (Edgar
dying alongside a creature misses its death) · a "sacrifice the greatest" tie takes the first ·
`GreedyAi` is one ply with a passive opponent — no turn planning, no holding up mana, no "do I need
this mana now" when choosing a tapped dual, and it loses slow races to evasive fliers
(`docs/findings/magic-ai.md`) · menace drops a
lone blocker instead of refusing the declaration · the auto-tapper ignores what the rest of the hand
needs · Empower Jace with two Jace tokens loads the first · surveil keeps the kept cards' order.

## What FRA demands (read the rules text before building each)

Keywords by card count: Surveil 43, **Empower Jace 35** (creates/loads a Jace *planeswalker token*),
**Prepared 24** (21 cards use the two-face `prepare` layout), Flying 28. Modal "choose one", flashback,
cycling/landcycling, ward, equipment and +1/+1 counters are common. Implemented so far: 130 cards scripted
in `FraCards` (every one played by `RandomGameTests.RandomPlay_EveryScriptedCard`), including 15 Empower Jace cards, the 15 dual lands, 14 modal cards and 11 sacrifice /
dies / exile-until cards, 25 step-trigger / per-turn / prowess / surveil-trigger cards, the 6 flashback cards, and 18 prepare cards (Hallway Heckler, Pyre Rhymer, Heartwood
Crafter wait: a discard cost on an ability, a mana-tap trigger, restricted mana). **Two real planeswalkers are
in** (The Theorist, Ajani Unrelenting); **the other 6 are not**: each needs an emblem, an "until your next turn" effect, impulse draw or a −X cost first.
Coverage plan, by cards unblocked: 7a lands ✓ · 7b-1 modal ✓ · 7b-2 sacrifice, dies triggers, exile-until-leaves ✓ · 7c-1 continuous effects ✓ · 7c-2 step triggers,
per-turn tracking, prowess ✓ · 7d-1 flashback ✓ · 7d-2 prepare ✓ · 7d-3 cycling and graveyard abilities. The Bo1 hand smoother is Arena's published description; its weighting and whether
limited Bo1 uses it are unconfirmed — hence `handSmoothing`.
