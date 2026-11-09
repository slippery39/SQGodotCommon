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
├── MagicRules.cs        THE API: NewGame (+ WithSideboard: cards outside the game), DecidingPlayerId, LegalActions, Act, Choose, AutoPass,
│                        DistinctMoves (+ IsMoveFor, CardOf: which card a move is for;
│                        OptionsAreObjects: every choice type, classified — a test enforces it;
│                        TapOptions: manual tapping for a front end)
├── MagicGame.cs / MagicPlayer.cs / MagicState.cs   root object, player, queries + MoveCard/EnterBattlefield
├── Describe.cs          plain-text actions, choices and cards for every front end and log
├── Stops.cs             Arena priority stops + full control for a human seat (front-end convenience)
├── Sandbox.cs           playtest tool: put a fresh card in any zone; onto the battlefield it ENTERS
├── Turns/TurnStructure.cs  EnterStep/EndStep/GivePriority + StateBasedActions (incl. legend rule, tokens)
├── Turns/Combat.cs      who may attack/block, declarations, simultaneous damage, end of combat
├── Turns/Triggers.cs    trigger events (MagicEvent: kind, subjects, who did it), the one matcher for a
│                        Trigger, Collect (APNAP, intervening "if"), PutOnStack with target choices
├── Actions/             PassPriority, PlayLand, TapForMana; CastSpell, ActivateAbility, StackAbility,
│                        StackResolution (fizzle); mulligan; hand, library, combat and target choices
├── Actions/LoyaltyActions.cs  Loyalty.AbilitiesOf (own + granted) and ActivateLoyaltyAction
├── Actions/ModeChoices.cs     ChooseModesChoice — a modal trigger's modes, chosen as it goes on the stack
├── Actions/PermanentChoices.cs  "you may sacrifice …": ChoosePermanentChoice, SacrificeChosen, IfChosen
├── Effects/Refs.cs      ContextKeys, Ref (you / opponent / target i / source / event card …, or
│                        All: a zone's objects a Filter matches), Count
├── Effects/Effects.cs   the Effect base; damage, life, counters, pumps, mana; If, May, PayMana
├── Effects/ZoneEffects.cs  effects that move cards: destroy, exile, sacrifice, return, discard, surveil, tokens
├── Effects/Abilities.cs spell, triggered, activated, mana and static ability components; Trigger, CostChange
├── Effects/AdditionalCosts.cs  costs beyond mana (tap, sacrifice, discard, exile, behold): one list for
│                        abilities, spells, flashback and ward; CostList (candidates, legality, or-pay)
├── Effects/Conditions.cs  "if …": Condition, AtLeast(Count, N), Not, Matches(Ref, Filter)
├── Effects/Modes.cs     Mode, Modal.Compose (chosen modes → one target list + steps), ScopeTargetsAction
├── Targeting/TargetSpec.cs  a target word = a zone (From) + a Filter + the targeting rules; one legality
│                        test for choosing AND resolving; Combinations
├── Filters/            "which objects": Filter (& | !), leaves, and `Is` — the vocabulary scripts read.
│                        Read by TargetSpec, Ref.All, statics' scopes, Matches conditions and the
│                        sacrifice/exile costs (FilterRefactorPlan.md); migration table:
│                        MagicCore.Tests/Filters/FilterMigrationTests.cs
├── Rules/Damage.cs      THE damage path (lifelink, deathtouch) — combat and effects alike
├── Rules/Costs.cs       THE effective cost (taxes, reductions: every CostChange)
├── Rules/Characteristics.cs  THE power, toughness and keywords: own + every static on the battlefield
├── FraCoverage.md / TrkCoverage.md   per set: what is scripted, what waits and why — status, not rules
├── AbilityCatalog.md    GENERATED: every effect, trigger, ref, count, condition, component — one line each
├── FraCardSheet.md      GENERATED: each FRA card's printed text, its script, the pieces it uses, its tests
├── Cookbook.md          how to script a card, by pattern; the tiers of checking
├── DesignAudit.md       2026-10-10: what was reinvented instead of reused from MtgCore, and MtgCore's own flaws
├── FilterRefactorPlan.md  DONE (story MC-2): one composable Filter replacing the closed enums — the design and why
├── Rules/Attachments.cs THE Aura/Equipment rules: attach, what's on a permanent, fall-off (state-based); Phasing.cs: THE phasing rules
├── Rules/Playable.cs    THE "may it be cast/played from here": hand, flashback, prepared copy, impulse exile
├── Rules/Prepare.cs     THE prepare rules: become prepared (a copy of the spell in exile), castable, unprepare
├── Rules/Life.cs        THE life-gain path ("whenever you gain life") and THE life-loss path (TurnTally.LifeLost)
├── Rules/Replacements.cs  THE numeric replacements ("that much plus one", "twice that many") — damage, counters
├── Cards/Card.cs        Card (computed P/T + keywords, permanent/combat state, Fresh()), CardFactory
├── Cards/CardData.cs    printed RULES data (Scryfall), one per name; CardPrinting: a set's rarity, number; Load(set)
├── Mana/                ManaPool, ManaCost (parse, WithGeneric), ManaPayment (solver), ManaAbilities
├── Sets/CardRegistry.cs EVERY set's cards by name: Get(name[, set]), IsImplemented, Printings(set); Tokens.cs: Treasure, Clue, Food; Scripting.cs: the helpers every set scripts with (When, Spell, M …)
├── Sets/Fra/, Sets/Trk/ <Set>Cards.cs: a set's abilities by name — a reprint scripted ONCE (MC-5.5); Fra/EmpowerJace.cs
├── Sets/Fra/FraDecks.cs 40-card decks of implemented cards (console, fuzz tests)
├── Sets/Fra/ArenaDeck.cs  an Arena deck export → FRA cards, every problem named; ReadPool: a sealed
│                        pool (every section) of any set (MC-5.4), what isn't that set's left out by name
├── Ai/GreedyAi.cs       one-ply simulate-and-evaluate AI
├── Ai/Evaluator.cs      THE position score every AI compares moves by (a change shows in GoldenAiReplay.txt);
│                        HeldValues: a held card priced by its measured value — the search's setting (AI-1)
├── Ai/Evaluator.Lifetime.cs  its planeswalker price: value over the walker's lifetime (AI-9.3) — and Effect,
│                        THE price of one effect read from its type (damage, removal, draws, mana, emblems …)
├── Ai/IAi.cs            what every AI is: Decide(state) for its player, returning the move too (AiDecision); RandomAi.cs is the floor
├── Ai/AiMatch.cs        THE strength harness: AI vs AI, every deck pairing, seats swapped (MagicConsole --match)
├── Ai/EarlyStop.cs      when a match has said enough: 5pp better / worse / neither, judged on mirror pairs (--until-clear)
├── Ai/PositionSuite.cs  THE quick check: recorded self-play positions, an AI decides again, what changed (--suite=A)
├── Ai/MoveTokens.cs     a move as a token (a3 · c12,15 · t41:2 tap · s5:Hand:Name sandbox) + Offers/Ending fingerprints — the suite's and the records'
├── Ai/GameRecord.cs     a played game, EVERY transition kept (you/ai/stop/rule/sandbox), replayed by token alone; stale says so; Transcript, Before(#N) (TBL-5) — kept as AI test cases in MagicCore.Tests/Records (KeptRecords)
├── Ai/RecordComparison.cs  a record's choices beside the AI's: the default search's pick and the oracle's rank of yours at each `you` decision (--compare, TBL-5.6)
├── Ai/Lookahead.cs      playing a simulation forward cheaply: Settle (see a move's result), QuickAnswer (chumps facing lethal: Chumps); WholeAttacks.cs: an attack as ONE decision — candidates, targets, the declaration answered whole (AI-17.3)
├── Ai/Rollout.cs        THE rollout the search scores by: to an absolute turn, cheap policies, terminal discount
├── Ai/BeamSearchAi.cs   the search AI: a beam over our decisions this turn, rollout-scored, plan committed;
│                        held cards priced by their measured value by default (AI-1); Settings.Interactive
│                        is the ~1 s wall-clock cap for the table and console — never in a sim run
├── Ai/BlunderLint.cs    AI-17.7: AI-vs-AI games, each decision checked for an obvious mistake and counted (a lint, never a weight)
├── Ai/LandUnlock.cs     AI-9.1: does a land unlock a card in hand, or more casts next turn? Prices own lands in the evaluator (Terms.LandUnlock)
├── Ai/Hidden.cs         what the search may know of the libraries and the opponent's hand: the real
│                        ones, or what isn't known (Card.KnownTo) sampled at its root — Hidden.Sample (AI-3)
├── Ai/CardValues.cs     the card-value sandbox: each card cast into a fixed board, measured to the turn's end; CastGains.cs: a held instant's offers in self-play, its option value (--cast-gains, AI-13.4)
├── Limited/Boosters.cs  a set's packs as Arena opens them (FRA: MTGJSON's collation; TRK: provisional; 4-colour commons) + a seeded sealed pool (LIM-1) — Draft.cs: Quick Draft, 8 seats, bots by rating + colour lean (LIM-2)
├── Limited/SealedBuilder.cs  pool → 40 in two colours, by any ratings table: the suggested deck AND the AI's own (LIM-1);
│                        Suggestions: + a splash and a greedy (up to 4 colours) build, every fixer counted (Fixes); an opponent plays one at random (LIM-3); ClosePairs/ForPair: the 4 closest pairs within 1.5 points, each one's builds (LIM-3.6)
├── Limited/Castability.cs  how often a deck's spells are castable on time, by simulated draws, and how often it landcycles in turns 1–4 — shown beside each suggestion (LIM-3.3, 3.7)
├── Limited/SealedBuildMatch.cs  candidate sealed builds (Arena exports, or a pool's whole offer) vs the same N AI sealed opponents, paired (MagicConsole --builds / --compare, LIM-1.8, 3.4)
├── Limited/CardRatings.cs  limited ratings: a set's own Sets/<SET>.ratings.json (TRK's INTERIM: rarity + CardValues rank, --interim-ratings), else 17lands GIH WR (Sealed, else Premier); NOT raw CardValues, which miss planeswalkers — SimulatedRatings.cs: DATA-2's games-in-hand WR from AI self-play (--sim-ratings)
├── Zones/Zone.cs        per-player zones (incl. Command: emblems, "until your next turn" and "this turn" effects; Sideboard only when given one) + shared Stack; per-CONTROLLER battlefield
├── Sets/FRA.values.json GENERATED card values (CardValues; MagicConsole --card-values) — embedded, a test fails while stale
├── Sets/FRA.positions.txt GENERATED position suite (PositionSuite; MagicConsole --suite=generate) — embedded, a test fails while stale
├── Sets/FRA.17lands.json  computed by tools/import_17lands.py from 17lands' PUBLIC game data (never its API): GIH WR + games
├── Sets/FRA.boosters.json  imported by tools/import_mtgjson_boosters.py — play-booster sheets by card name, never hand-edit; TRK's is PROVISIONAL, tools/build_trk_boosters.py (Wizards' odds, MC-5.4)
└── Sets/FRA.json, TRK.json   imported by tools/import_scryfall_set.py — embedded resources, never hand-edit
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
  stack choices die there. Only `Shuffle`, `RevealUntilOntoBattlefield`, `GainControl` and `Rules.Phasing` (no zone change) and the AI's `Hidden.Sample`
  use `MoveObject`, to reorder within one zone (`MoveCard` would reset `KnownTo`) — and
  `Hidden.Sample`'s hand modes, in a hypothetical position where a real zone change's events must
  not fire: swapping opponent cards between hand and library (AI-3.4), or replacing one in place
  with a guessed card that keeps its id (`UpdateObject`, AI-3.5).
- **One computation each, never a second copy:** `s.Power(card)`/`s.Toughness`/`s.Has` (Rules/
  Characteristics.cs — the card's own numbers plus every static ability on the battlefield; rules
  never read `Card.OwnPower`/`HasOwn` or `BasePower`, which ignore every anthem on the board),
  `s.AbilitiesOf<T>`/`HasAbility<T>` (a permanent's own abilities — never `GetComponents`, or "loses all
  abilities" misses that rule), `Damage.Deal` (players, creatures, planeswalker
  loyalty), `Life.Gain`/`Life.Lose`, `Costs.Of`, `ManaPayment.Find`, `ManaAbilities.CanTap`/`CanPayTapCost`,
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
  statics went stale exactly there. "Can't be blocked" and "can attack as though it didn't have defender" are keyword-like grants
  (`Combat.CantBeBlocked`, `Combat.AttacksDespiteDefender`) — a static's scope says who.
  ONE exception: a type change (`BecomesType`: Puppet Crafting, Mogis) is stamped, because every rule reads
  `card.Is(Creature)` — put right by the state-based check before anyone gets priority.
- **Every engine piece carries a `///` summary that names a card and quotes its text.** The catalog
  is generated from those summaries — a piece without one is invisible to the next session.
- **Compose, never a sibling per card phrase** (Shayne, story MC-6): a card differing by a number, filter,
  zone or event is an OPTION on the piece (`Count`, `Filter`, `Trigger`), never a new flag, enum case or
  one-card component. A new piece names the nearest one and why no option fits; generalising moves old cards.
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
- **A card plays only if `CardRegistry.IsImplemented`** — scripted, a basic land, or text that is only
  engine keywords. Anything else plays silently vanilla. **Every scripted card has a test in
  a `Sets/Fra*Tests` file that its ability FIRES** (`FraCardTests`, or the batch's own file —
  `FraTriggerTests`, `FraStaticTests` …), through real casting and the real stack.
- Card abilities are hand-written as data (a set's Cards file); **name, cost, type, P/T and keywords come from
  `CardData`**, never retyped. Test cards are inline (`TestGames.Creature/Inline`).
- **A card test asserts the PRINTED text's numbers** (Last Gasp is -3/-3) — that is the root rule's
  exception, not a breach: printed cards get no balance passes, and reading the number back from the
  script would make the test check the script against itself and miss a transcription error.
- `AutoPass`, `DistinctMoves` and `Stops` are conveniences for front ends and the AI, never rules:
  `LegalActions` stays complete.
- Same serialization rule as the root `CLAUDE.md`: nothing with a delegate lives in `GameState`.
- **CSharpier 0.29 misreads `x is SomeType ? [a] : b`** — it takes `SomeType?` for a nullable type.
  Valid C#, but the commit hook then skips formatting the whole file. Write a `switch` expression.
- **Test trap:** helpers that replace a `GameState` field (`Hand`, `Field`) must run on their own line —
  in `s.Act(new … { CardId = Hand(…) })` C# reads `s` before `Hand` replaces it.
- **Namespace trap in MagicCore.Tests:** `Ai.` and `Cards.` resolve to the test project's own folders.
  Use a `using`, or `global::MagicCore.…`.

## Known ceilings

Each is marked `ponytail:` in the code, with its upgrade path; the list is in `DesignNotes.md` under
**MagicCore known ceilings**. Add a new one there in the same step as its comment.

## FRA cards

**Read each card's FULL rules text before building it** (a listing truncates; Yoshimaru's hidden
ability was found that way). Coverage — what is scripted, what waits and why — is in `FraCoverage.md`.
