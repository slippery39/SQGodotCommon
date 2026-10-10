# MagicCore — real Magic rules (branch `mtg-real`)

Its own thing, like KIN: real phases, priority, the stack, blocking, lands that tap for mana.
**Arena-accurate, Bo1 only** — comprehensive-rules corners wait until a card needs them.
First set: **Reality Fracture (FRA)**. Play it: `dotnet run --project MagicConsole` (Commands.md).

`MtgCore` is the simplified deckbuilder engine and is **not modified** for this work. Copy what fits
from it and own the copy — the two rule sets diverge on combat, mana and the stack.
**Grep MtgCore FIRST for every mechanic a batch needs** (flashback, mill, library search, discard,
exhaust … are already there) — copy and adapt what fits, and say what was lifted in the batch report.

**Building cards: `/card-batch`.** What exists: **`AbilityCatalog.md`** (generated — read it instead of
the engine source). How to script: **`Cookbook.md`**. Every card's script beside its printed text:
**`FraCardSheet.md`** (generated). Regenerate both with `python tools/magic_docs.py` after any change.

## Source map

```
MagicCore/
├── MagicRules.cs        THE API: NewGame, DecidingPlayerId, LegalActions, Act, Choose, AutoPass,
│                        DistinctMoves (+ IsMoveFor, CardOf: which card a move is for;
│                        OptionsAreObjects: every choice type, classified — a test enforces it;
│                        TapOptions: manual tapping for a front end)
├── MagicGame.cs / MagicPlayer.cs / MagicState.cs   root object, player, queries + MoveCard/EnterBattlefield
├── Describe.cs          plain-text actions, choices and cards for every front end and log
├── Stops.cs             Arena priority stops + full control for a human seat (front-end convenience)
├── Sandbox.cs           playtest tool: put a fresh card in any zone; onto the battlefield it ENTERS
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
├── FraCoverage.md       what FRA is scripted, what waits and why — status, not rules
├── AbilityCatalog.md    GENERATED: every effect, trigger, ref, count, condition, component — one line each
├── FraCardSheet.md      GENERATED: each FRA card's printed text, its script, the pieces it uses, its tests
├── Cookbook.md          how to script a card, by pattern; the tiers of checking
├── Rules/Attachments.cs THE Aura/Equipment rules: attach, what's on a permanent, fall-off (state-based)
├── Rules/Playable.cs    THE "may it be cast/played from here": hand, flashback, prepared copy, impulse exile
├── Rules/Prepare.cs     THE prepare rules: become prepared (a copy of the spell in exile), castable, unprepare
├── Rules/Life.cs        THE life-gain path — raises the event "whenever you gain life" reads
├── Rules/Replacements.cs  THE numeric replacements ("that much plus one instead") — damage, +1/+1 counters
├── Cards/Card.cs        Card (computed P/T + keywords, permanent/combat state, Fresh()), CardFactory
├── Cards/CardData.cs    printed data (Scryfall) + CardDataSet.Load("FRA")
├── Mana/                ManaPool, ManaCost (parse, WithGeneric), ManaPayment (solver), ManaAbilities
├── Sets/Fra/FraCards.cs hand-written FRA abilities by name; IsImplemented
├── Sets/Fra/EmpowerJace.cs  the keyword action and the Jace token
├── Sets/Fra/FraDecks.cs 40-card decks of implemented cards (console, fuzz tests)
├── Sets/Fra/ArenaDeck.cs  an Arena deck export → FRA cards, every problem named
├── Ai/GreedyAi.cs       one-ply simulate-and-evaluate AI
├── Zones/Zone.cs        per-player zones (incl. Command: emblems) + shared Stack; per-CONTROLLER battlefield
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
  stack choices die there. Only `Shuffle` and `RevealUntilOntoBattlefield` use `MoveObject`, to
  reorder within one zone.
- **One computation each, never a second copy:** `s.Power(card)`/`s.Toughness`/`s.Has` (Rules/
  Characteristics.cs — the card's own numbers plus every static ability on the battlefield; rules
  never read `Card.OwnPower`/`HasOwn` or `BasePower`, which ignore every anthem on the board),
  `s.AbilitiesOf<T>`/`HasAbility<T>` (a permanent's own abilities — never `GetComponents`, or "loses all
  abilities" misses that rule), `Damage.Deal` (players, creatures, planeswalker
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
- **Static abilities are never stamped onto what they affect.** A `StaticEffectComponent` on its source
  is read live by Characteristics, so it stops the instant the source leaves — MtgCore's stamped
  statics went stale exactly there. "Can't be blocked" is a keyword-like grant (`Combat.CantBeBlocked`).
  ONE exception: an Aura's type change (Puppet Crafting) is stamped, because every rule reads
  `card.Is(Creature)` — put right by the state-based check before anyone gets priority.
- **Every engine piece carries a `///` summary that names a card and quotes its text.** The catalog
  is generated from those summaries — a piece without one is invisible to the next session.
- **Each mechanic's rules** — planeswalkers, modes, step triggers and "this turn", flashback, prepare,
  abilities from the hand and graveyard, hexproof and indestructible, Auras and Equipment, countering
  and ward, reflexive and intervening triggers — are in `.claude/rules/magic-mechanics.md`, which
  loads with any MagicCore file. Add a new mechanic's rule there, not here.
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
- `AutoPass`, `DistinctMoves` and `Stops` are conveniences for front ends and the AI, never rules:
  `LegalActions` stays complete.
- **AI evaluation terms are MONOTONIC** — more of a good thing always scores more, tapering not
  capping. A cap makes "more" tie with "less", and a tie silently picks the passive move: a life cap
  at 20 stopped every attack past tapped blockers; a land cap at 7 made the AI hoard lands.
- Same serialization rule as the root `CLAUDE.md`: nothing with a delegate lives in `GameState`.
- **CSharpier 0.29 misreads `x is SomeType ? [a] : b`** — it takes `SomeType?` for a nullable type.
  Valid C#, but the commit hook then skips formatting the whole file. Write a `switch` expression.
- **Test trap:** helpers that replace a `GameState` field (`Hand`, `Field`) must run on their own line —
  in `s.Act(new … { CardId = Hand(…) })` C# reads `s` before `Hand` replaces it.

## Known ceilings

Each is marked `ponytail:` in the code, with its upgrade path; the list is in `DesignNotes.md` under
**MagicCore known ceilings**. Add a new one there in the same step as its comment.

## FRA cards

**Read each card's FULL rules text before building it** (a listing truncates; Yoshimaru's hidden
ability was found that way). Coverage — what is scripted, what waits and why — is in `FraCoverage.md`.
