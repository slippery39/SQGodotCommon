# Design Notes

Patterns and decisions that work for the current scope but will need revisiting as the engine grows.
This is not authoritative architecture (that lives in CLAUDE.md) — it's a watchlist.

---

## Choice action resolution in the simulator

**Concern:** `DepthLimitedAiStrategy.ResolveChoice` only handles single-select choices well — it
evaluates each option individually and picks the best. For multi-select choices (`MinChoices > 1`,
e.g. "discard 2" from Careful Study) it falls back to random selection because evaluating all
combinations is too expensive at search time.

**Why it's fine now:** Multi-select choices are rare in the current card pool and random resolution
is good enough for the baseline AI.

**Watch for:** Cards with high-impact multi-select choices where random resolution produces clearly
wrong decisions (e.g. discarding your best card when better targets exist). At that point, consider
a greedy iterative approach: score each option individually, rank them, pick the top/bottom N. This
won't be optimal over all combinations but will outperform random in practice.

---

## GetEffectiveX proliferation

**Concern:** The keyword system currently uses per-keyword extension methods on `CreatureEvaluator`
(`GetEffectiveHaste`, and potentially `GetEffectiveDeathtouch`, `GetEffectiveLifelink`, etc.).
Each new keyword that can be granted by a static ability requires a new method, and every action
that checks that keyword must be updated to call the evaluator instead of reading the flag directly.

**Why it's fine now:** There are only a handful of keywords in play and the call sites are few.

**Watch for:** When keyword count grows past ~5–6 granted keywords, or when the same "read flag OR
scan statics" pattern is copy-pasted a third time, refactor toward a single
`GetEffectiveKeywords(state, cardId) → KeywordSet` that does the static scan once and returns
all active keywords in one pass. Actions then query the set rather than calling individual methods.

**Note on above**
Not entirely sure on the above solution actually... but we will need to revisit this when we get there

---

## Token explosion / symmetry reduction in the simulator

**Concern:** Cards like Krenko and Siege-Gang Commander create many identical tokens. Because each
token is a distinct game object, `GetLegalActions` generates a separate action per token
(attack with token #1, attack with token #2, …). The AI's depth-limited search evaluates all of
them even though they produce identical board states, causing exponential blowup that pushes games
past the time limit.

**Why it's fine now:** Excluded from the random card pool until resolved.

**Confirmed in the Core Set Cube, which does not exclude them.** With wall-clock termination gone,
11 games in 28 000 still needed more than 300 seconds to reach turn 12 and were dropped as broken
(0.04%). `DrawDiagnostics` named the cards without being asked: the permanents most over-
represented on the board when a game fails to finish are Generator Servant, Krenko Mob Boss,
Siege-Gang Commander and Goblin Piledriver, and those games end with a median of **24 permanents
against 7 for a decided game**.

**Half-fixed since.** The fingerprint deduplication this entry proposed already existed for
*attackers* (`AttackerSignature`) and has now been mirrored for *defenders*
(`DefenderSignature`) — see MtgCore/CLAUDE.md. Attack actions are a cross product, so collapsing
one dimension left the other multiplying. Worst-case legal actions fell 74 → 47 with the median
unchanged, and runs got 13% faster.

**Resolved, but not by deduplication.** The defender-side dedup made runs 13% faster and moved the
tail not at all (11 → 12 games per 28 000). What closed it was declining to *search* the
near-identical actions rather than proving them equivalent: `DefaultMaxBranching` in
`MultiTurnBeamSearchAiStrategy`. Over 28 000 games, cap versus no cap:

| | No cap | Cap 16 |
|---|---|---|
| Run time | 2 318s | 1 604s |
| Median game | ~8 000 ms | ~1 700 ms |
| Games over the 300 s net | 12 | **4** |

**The lesson worth keeping: dedup and capping are complements, and the cheap one is the cap.**
Dedup requires proving two actions produce identical outcomes, which needs a signature per action
type and is wrong the moment a card breaks the assumption. The cap needs no such proof — it ranks
cheaply and explores the best few, and because `SelectAction` re-runs after every action, a pruned
action returns from the next state rather than being lost. Measured cost in play strength: none
(48.2% over 224 games against an uncapped opponent, 1 SE 3.3pp).

Extending dedup to abilities and targeted spells is therefore **no longer worth doing on these
grounds**. It stays unbuilt.

**What remains:** 4 games per 28 000 still exceed 300 seconds, and their character has changed —
median turn **3**, not 11–17, with a median of 3 permanents. These are no longer wide-board games,
so a fifth signature type would not touch them. They are excluded from training data and cost
nothing but wall-clock. Diagnose one from a `flagged_games/` snapshot before assuming a cause.

**The original proposed fix, kept for the record and superseded.** It was action-level fingerprint
deduplication in the simulator AI (`Name + ManaCost + Components + Damage + active modifiers`,
with `HasActivated`, `UntilEndOfTurn` modifiers and summoning sickness in the fingerprint). Half of
it was built — in `MtgActionGenerator` rather than the AI, since that is where attack actions are
enumerated — and the branching cap then closed the problem it was aimed at. Do not build the other
half without a measurement showing it is needed; see the paragraphs above.

---

## Static ability scanning — AffectedIds cache

**Concern:** `CreatureEvaluator.GetApplicableStaticAbilities` scans all battlefield permanents on
every P/T or keyword read. At small board size this is negligible, but it grows with lord count
and board size, and is called constantly by the simulator's AI evaluation loop.

**Proposed design (defer until profiling confirms cost):**
Two complementary pieces:

1. **`AffectedIds = ImmutableHashSet<int>` on `StaticAbilityComponent`** (the index): lets the
   source quickly answer "who am I affecting?" without a board scan — used for cleanup when the
   source leaves and for any source-side queries.

2. **Applied modifier components on affected permanents** (the data): each affected permanent gets
   a modifier component (e.g. `AppliedStaticPTBoost { SourceId, PowerBonus, ToughnessBonus }`)
   pointing back to the source. This means a permanent can compute its own P/T purely from its
   own component list — no cross-referencing the board required.

These two work together: `AffectedIds` is the index that makes the push model manageable;
the applied components are what keep each permanent self-describing.

**What needs enforcing by game logic:**
- When a `StaticAbilityComponent` enters play: run filter across board, stamp applied components
  onto matching permanents and populate `AffectedIds` on the source
- When a permanent ETBs: check all active static abilities, apply components and add to
  `AffectedIds` if filter matches
- When a permanent leaves: remove applied components using `AffectedIds` (no scan needed —
  iterate the set, remove the component from each)
- When the source leaves: use `AffectedIds` to find affected permanents, remove applied
  components, clear the set

**Trigger:** Do this after profiling shows the scan is measurably slow, or when zone-dependent
statics (Wonder) require reliable zone-change tracking anyway.

---

## Spells have no type line worth reading

**Concern:** `MtgCardMapper.GetTypeLine` renders a flat `"Spell"` for every instant and sorcery in
the set. Two gaps cause this, both in the engine rather than the UI:

1. `SpellCardBuilder` has no `WithSubtype` and never sets `Subtypes` (`SpellCardBuilder.Build()`),
   so every spell carries an empty subtype set. Only creatures and tokens are tagged.
2. There is no Instant/Sorcery marker anywhere — see the Delirium row in `MtgCore/CLAUDE.md`, which
   was cut for the same reason.

**Why it's fine now:** Creatures are where tribal identity matters for drafting, and they are fully
tagged (Human 58, Zombie 31, Spirit 29, …). Spells are still differentiated by their frame tint
(`MtgCardTheme.FrameColor`) and by their rules text.

**Watch for:** A tribal or type-matters spell ("target Zombie gains…", "instants cost 1 less"), or a
drafter who cannot tell removal from a combat trick at a glance. The fix is a `WithSubtype` on
`SpellCardBuilder` plus a mechanical tagging pass over ~180 Hollowmere spell definitions — do it as
one deliberate pass, not card by card.

---

## Battlefield card size is set by the board's height budget

**Concern:** `BattlefieldZone.CardScale` (0.68) is a computed ceiling, not a taste call. `MainColumn`
gets 0.73 of a 1080 viewport (788px), and each row costs the card's height plus 28px of margin and
border. Whatever else lives in that vertical stack comes straight out of card size:

| Chrome in the column | Left for both rows | Max scale |
|---|---|---|
| Panels + buttons stacked vertically (original) | ~469px | 0.45 |
| Panels in the left rail (current) | ~713px | ~0.74 |

`CustomMinimumSize` is a hard floor, so exceeding the budget pushes the End Turn button off the
bottom rather than shrinking the rows — it fails by silently clipping the layout, not by erroring.

**Why it's fine now:** The left rail keeps the column nearly empty, so 0.68 fits with slack.

**Watch for:** Anything moved back into `MainColumn` — a phase bar, a stack display, a second
button row. Recompute the budget rather than eyeballing it, and remember the hand occupies the
remaining 27% of the screen, so raising the 0.73 anchor trades against hand space (the hand already
clips at the bottom edge).

---

## Vigilance is deliberately unimplemented

**Concern:** Eight white cards in the Core Set Cube are printed with vigilance
(Speaker of the Heavens, Serra Avenger, Topan Freeblade, Steadfast Sentry, Gallant Cavalry,
Basri's Lieutenant, Captain of the Watch, Sun Titan, and Resplendent Angel's token). None of them
have it. With no blocking and a one-attack-per-turn rule, "attacking doesn't cause this creature
to tap" has nothing to attach to.

**Why it's fine now:** The cards are still playable bodies; vigilance was simply the least
load-bearing line of text on each. Every one carries a comment saying so.

**Watch for:** The decision, when you make it. Two options were costed:
- *Merge into Exhaust* — delete `HasAttacked`, make `IsExhausted` the single attack limiter, and
  let vigilance mean "attacking doesn't exhaust you", i.e. attack twice per turn. One field
  instead of two, and it makes every tapper meaningfully better. Strong, and mono-white flagged.
  `IsExhausted` was deliberately added as a SEPARATE field so this refactor stays small: set it
  in `AttackAction.Execute` and delete `HasAttacked`.
- *Leave blank* — accept it as reminder text and compensate with stats.

Nothing in Hollowmere uses vigilance, so whichever way this goes, only the Core Set Cube rebalances.

---

## Replacement effects cover amounts, not structure

**Concern:** `ReplacementModifierComponent` (see `MtgCore/Modifiers/`) replaces the AMOUNT of an
event — "gain that much life plus 1", "prevent 2 damage", "draw two instead". It cannot express a
STRUCTURAL replacement: "enters the battlefield tapped", "if it would die, exile it instead",
"if you would draw, mill instead".

**Why it's fine now:** Every replacement in white is numeric (Angel of Vitality). Imposing
Sovereign's "creatures your opponents control enter tapped" is modelled as an ETB trigger that
exhausts the entering creature, which is observationally identical here because nothing can
respond between the two.

**Watch for:** A card where the difference is visible — an ETB trigger on the creature that
enters tapped and cares about being tapped, or a "dies -> exile instead" that must beat a death
trigger. At that point the answer is a pre-execute hook in the `ImmutableGameObjects` action loop
that lets a component rewrite a spawned `GameAction` before it runs, plus an already-replaced
marker so a replacement cannot replace its own output. Deliberately not built speculatively.

---

## Conditional team anthems have no home

**Concern:** Path of Bravery reads "as long as your life total is at or above your starting life
total, creatures you control get +1/+1". It is implemented as an UNCONDITIONAL anthem.

`StaticAbilityEngine` is a push model: it stamps `AppliedStaticPTBoost` onto affected permanents
and only re-stamps on `CreatureEnteredBattlefieldEvent` / `PermanentLeftBattlefieldEvent`. A team
anthem gated on a life total would go stale the instant anyone took damage, because nothing
re-stamps on a life change.

**Why it's fine now:** One card, and the unconditional version is a coherent card at the same rate.
A single creature CAN be conditionally buffed today — `LifeTotalComponent` and `ThresholdComponent`
are live-evaluated `PowerToughnessModifier`s and are always correct. Only the team-wide case is
missing.

**Watch for:** A second card wanting it. The fix is a live-evaluated team anthem: rather than
stamping, have `GetEffectiveStats` scan the controller's battlefield for
`ConditionalStaticBoost` sources. That is the O(k) board scan the push model was built to avoid,
so measure before adopting it — or accept staleness and re-stamp on a wider event set.

---

## Card types exist now, and Delirium is unblocked

**Note, not a concern.** `MtgCore/CLAUDE.md` previously recorded Delirium as permanently deferred
because "there is no card-type system — Artifact/Enchantment/Land are strings in `Subtypes` and
instants/sorceries carry no type marker at all". That is no longer true: `CardType` is a real flags
enum on `Card`.

Nothing counts distinct types in a graveyard yet, but the blocker named in that note is gone. If a
future set wants Delirium, it is now a counting helper rather than a subsystem.

**Watch for:** the derivation fallback in `Card.EffectiveTypes`. It reports `Instant|Sorcery` for
any card that never declared a type, which is honest ("a spell, kind unknown") but means
`HasType(CardType.Instant)` is true for every undeclared spell. Code needing the distinction must
test one flag and not the other. Migrating the older `CardFactory.Spell(...)` cards to
`.Instant(...)` / `.Sorcery(...)` would remove the ambiguity for good.

---

## The AI does not hold mana for counterspell traps

**Concern:** Counterspells fire from hand when the opponent casts a matching spell and you left the
mana unspent (see "Counterspell Traps" in `MtgCore/CLAUDE.md`). `DepthLimitedAiStrategy` has no
concept of value in unspent mana — it will spend down to zero every turn and its traps will
therefore almost never fire.

**Why it's fine now:** The mechanic is correct; only the AI's use of it is weak. Traps fire
properly for a human player, and against the AI they are simply a dead card rather than a broken
one.

**Watch for:** the moment blue starts losing badly in simulator benchmarks, or when black/red
counterparts arrive. The fix is in `StateEvaluator`, not the engine: score unspent mana as worth
something when a trap is in hand and it is about to become the opponent's turn. Do it once, across
all colours, rather than special-casing blue.

---

## Structural gaps blue could not close

**Note, not a concern.** Blue is complete at 67/67, but five clauses were cut because the concept
does not exist. Recorded so a later colour does not rediscover them:

- **Phasing** (Teferi, Master of Time) — no notion of a permanent that temporarily does not exist.
  Reskinned to a freeze.
- **Opponent-made partitions** (Sphinx of Uthuun's "an opponent separates those cards into two
  piles") — `ChoiceAction` can offer options to the active player only; there is no shape for a
  choice made by the other player mid-resolution.
- **Casting from another player's library** (Talent of the Telepath) — no path exists for one
  player to cast another's cards.
- **Keyword REMOVAL** (Mu Yanling's "loses flying") — every keyword path ORs abilities on;
  nothing subtracts one. `BecomesBaseCreatureComponent` strips them ALL, which is a different
  thing and only works because "loses all abilities" is what its cards say.
- **Instant speed** generally — flash, and Teferi's "activate loyalty abilities on any player's
  turn". Same priority gap the counterspell traps route around.

If a later colour needs keyword removal specifically, that is the cheapest of the five: a
`SuppressedKeywordsComponent` read at the end of `GetEffectiveStats`, mirroring how
`BecomesBaseCreatureComponent` already zeroes them.

---

## Divided damage is sprayed at random

**Concern:** Six red cards read "deals N damage divided as you choose among any number of targets"
(Cone of Flame, Flames of the Firebrand, Chandra's Outrage, Thundermaw Hellkite, Inferno Titan,
Drakuseth). Targeting here is single-target or all-valid; there is no shape for "choose K targets,
then apportion N among them". They are built as N independent 1-damage effects with `Random()`
targeting — Arcane Missiles — and cost one less than printed to pay for the loss of aim.

**Why it's fine now:** It needed no engine change at all, the cards stay playable and on-theme, and
random damage is a recognisable design rather than a broken one. Three of the six (Thundermaw,
Inferno Titan, Drakuseth) are near enough to sweepers that aim barely matters.

**Watch for:** the moment a card's whole point is the apportioning — "2 damage to one creature and
1 to another" as a deliberate two-for-one — or a player complaining that their removal spell hit
the wrong creature. The fix is a real multi-target strategy: `TargetSelectionMode.Divided` carrying
a total and a max target count, filled by the UI the way `TargetIds` already is, plus AI evaluation
that can score a partition. That last part is the expensive half, which is why this was deferred.

---

## A card that resolves is not a card that works

**Note, not a concern — the fixture already exists.** The first Core Set Cube playtest found nine
broken cards. Every one of them BUILT, cast and resolved without throwing, and every existing
smoke test passed on them. `CoresetCubeWhiteTests.EveryCard_CanBeCastAndResolve` asserts a card
reaches the battlefield; it says nothing about whether the card did anything.

Diagnosing it properly turned nine reported bugs into **twenty-seven** — the reports were a
sample, not the set. Three classes, all silent:

1. **User-select targeting inside a trigger** (19 cards). A trigger spawns `ResolveEffectAction`
   with no `TargetIds`, so a single-target strategy resolves to an EMPTY list and the effect does
   nothing. `HollowmereCardBugTests` had a test for exactly this — scoped to Hollowmere, so it
   never looked at the new set. Now fixed at the root in `TriggerTargeting`, which downgrades
   user-select to Random at build time so no card can reintroduce it.
2. **A targeting strategy on an action that cannot receive targets** (4 cards). `ResolveEffectAction`
   only injects into an `ITargetedAction`; anything else silently gets none.
3. **An aura attaching via an ETB trigger** (8 cards). A trigger cannot make a spell illegal, so
   the aura resolved with nothing to enchant and sat inert forever.

**The lesson for the next colour**: when a set introduces a mechanic, extend the structural bug
fixture in the same step. A set-scoped fixture is worth almost nothing to the set that comes after
it — copy it forward or make it iterate `SetRegistry.All`.

**Done, during the red pass.** `CoresetCubeCardBugTests` is now `AllSetsCardBugTests` and iterates
`SetRegistry.All`, so adding a set to the registry automatically subjects it to every rule. All
four rules passed on Hollowmere and Legacy unchanged, so nothing was hiding in the older sets —
the value is entirely forward-looking. Never narrow one of those rules back to a single set.

**And retrain after fixing cards.** The model had measured 19 cards while they did nothing, so
their learned values described blanks. That is the same staleness the Flying-restriction note
warns about, arriving by a different route.

---
## A self-feeding trigger hangs the engine, it does not merely slow it

**Note, not a concern — both halves are fixed.** Flameshadow Conjuring is "whenever a creature you
control enters, create a creature token". Nothing here can express the printed card's "**nontoken**
creature", so the token it makes is itself a creature entering, which re-triggers the ability
forever.

The failure mode is worse than it sounds. `GameState.ProcessAllActions` was an unbounded `while`
loop, and the per-turn action limit that would have caught this lives in `GameRunner`, not in the
engine — so the call never returned. It did not produce slow games or time-limit flags; it wedged
training threads permanently, and burned three hours of a 25-minute run before anyone noticed. In
Godot it would have frozen the UI outright and lost the player's game with nothing logged.

Two independent guards now exist, deliberately:
- **`GameState.MaxActionsPerResolution`** (10 000) throws rather than looping. Enormous compared
  to any legitimate resolution, so nothing real trips it. Both callers already handle exceptions —
  `GameRunner` flags the game and snapshots it, `MtgGameScene` writes a crash snapshot — so an
  exception is strictly better than a freeze.
- **`AllSetsCardBugTests.CreatureEtbTriggers_ThatMakeCreatures_AreCapped`** stops such a card
  shipping at all. It deliberately exempts triggers filtered by `IsSourceCardSpecification`:
  "when THIS enters, create tokens" cannot feed itself, which is most ETB token-makers in every
  set (Siege-Gang, Grave Titan, Captain of the Watch) and all of them are safe.

**Watch for:** the same shape on a different event. "Whenever you gain life, gain 1 life" and
"whenever a creature dies, create a creature" are the same bug wearing different clothes, and only
the generic `MaxActionsPerResolution` guard covers those. If a second instance appears, generalise
the test rather than adding a third special case.

---
## Safe Passage could not reach its own mechanism

**Note, not a concern — fixed, and the root cause was much bigger than the card.**

The prevention mechanism itself was correct and tested. The card could not reach it, for two
independent reasons, neither visible from the card definition:

1. **Combat damage never called `ReplacementEngine.ApplyReplacements` at all.** `AttackAction` has
   its own damage paths, separate from `DealDamageAction`, and they were simply never wired in —
   so prevention applied to burn spells but not to attacks, in a game where combat is nearly all
   of the damage. Fixed in both the player and creature paths.
2. **The shield expired before the damage arrived.** With no priority window a spell is castable
   only on your own turn, while combat damage to you arrives on the opponent's, and
   `EndTurnAction` stripped `UntilEndOfTurn` replacements in between.
   `ModifierDuration.UntilYourNextTurn` now survives that boundary and is cleared by
   `StartTurnAction` for the player whose turn is beginning.

The same edit exposed a third bug in the same method: combat damage did not update
`MtgPlayer.LifeLostThisTurn`, so being attacked — the most common life loss in the game — was
invisible to every payoff reading it (bloodthirst, Chandra's Phoenix, Knight of the Ebon Legion).
They worked when you burned the opponent and silently did nothing when you hit them.

**Watch for the general shape rather than the card.** Effect damage and combat damage are separate
code paths and only one was kept current. Any new numeric replacement, and anything that reads a
life-loss counter, must be checked against BOTH.

---

## Targeted modes need explicit targeting, and silently do nothing without it

**Note, not a concern — fixed.** `ApplyChosenModeAction` spawns the chosen mode directly, and
nothing else in the modal pipeline resolves a `TargetingStrategy`. That is correct for the modes
most modal cards use — Demonic Pact and Dread Presence drain, draw and discard via
`PlayerIdContextKey` and find their own subject — and silently wrong for a mode that needs targets.

Fortify offered "creatures you control get +2/+0" and buffed nobody in either mode. It measured
41.6% in the trained model, which is what a blank card scores.

`ApplyChosenModeAction.ModeTargeting` now carries an optional per-mode strategy; a mode that has
one is resolved through `ResolveEffectAction`, which is the only thing that turns a strategy into
real ids. `SpellCardBuilder.WithModes` gained the matching overload.

**Watch for:** a new modal card whose mode is an `EffectAction` with neither `ModeTargeting` nor a
`TargetContextKey`. It will build, cast, resolve and do nothing.

---

## Wall-clock decided game outcomes, and the model wore the results

**Note, not a concern — fixed.** `GameRunner` used to end a game as a draw at 20 000 ms of
wall-clock. That made a game's *result* a function of machine speed, and the consequence was not
subtle: over 28 000 games, 4 263 of 4 264 draws were `TimeLimitReached` and exactly one was a
real draw. The draw rate scaled with batch size (0.4% at 1 120 games, 2.8% at 8 400, 15.2% at
28 000) because a larger batch retains more finished games, which slows every game down.

Dropping the retained event logs — a change that cannot touch gameplay — moved 2 578 outcomes.
That is the whole argument: if holding memory changes who wins, the outcome was never about the
cards.

Fixed by removing wall-clock from the termination decision (turn and action limits already bound
a game deterministically), bounding AI cost with a rollout budget instead of a clock, and
excluding machine-decided games from training data. A 1 120-game run went from 0.4% draws to 0%,
base win rate exactly 50.0%.

**Watch for:** any new `Stopwatch`/`DateTime` reading that feeds a decision rather than a report.
The safety net at 300 s is the only wall-clock left in the game loop, and a game it ends is
excluded from training rather than scored. `MachineIndependenceTests` pins this under CPU load.

## Two runs, one seed, different results — determinism is not fully held

**Parked deliberately. Investigate as its own piece of work; it is not a side-effect of anything
above.**

Two 28 000-game training runs with the **same seed and the same gameplay code** produced different
outcomes: 10 draws vs 8, `TurnLimitReached` 6 vs 2, and an `UnhandledException` game in one run and
none in the other. Only the flagged-game snapshot saving was added between them, which touches no
game state.

The 300-second safety net is wall-clock and is a known, accepted source of variance for the handful
of games sitting near it — a game cut at 300s in one run may continue in another and end
differently. **That explains some of it and not the crash**, which happened 148 ms into its game.
A 148 ms crash is nowhere near any clock and should reproduce exactly.

Why it matters: the whole draw investigation concluded that a game's result must depend only on the
cards and the seed. If that is still not true, the training data carries noise nobody can see, and
every measured comparison in `MtgSimulator/CLAUDE.md` has an unquantified error bar.

Candidates not yet ruled out:
- Something in the engine reading ambient state (`Random.Shared`, a dictionary or set iteration
  order, `DateTime`) on a path only reached occasionally.
- A `Parallel.For` body in the AI whose result depends on completion order despite writing into a
  pre-allocated array.
- The safety net having second-order effects — a game cut early changes nothing for other games,
  but confirm rather than assume.

First step is cheap and does not need a full run: play one game twice in-process and diff the event
logs, then bisect. `MachineIndependenceTests` already has the harness shape for it.

## Drawing from an empty library does nothing at all

Confirmed from flagged-game snapshots: both `TurnLimitReached` games reached **turn 101 with both
libraries at 0 and both players alive** (life 9/8 and 11/10), having fired **332 and 148**
`LibraryEmptyEvent`s and zero `PlayerLostEvent`s. The games could not end.

`DrawCardsAction` adds `LibraryEmptyEvent` to the returned event list only — not to
`PendingGameEvents` — and nothing anywhere acts on it. The engine's only loss condition is
`"life total reached zero"` (`CheckStateBasedEffectsAction`). This is the **fifth** instance of the
log-versus-trigger-feed bug catalogued in `MtgCore/CLAUDE.md`, and it sits three lines above the
comment describing the fourth.

Consequences beyond the stalled games: **`GameEndReason.LibraryEmpty` is unreachable**.
`GameRunner` derives it from a `PlayerLostEvent` whose reason contains "library", which is never
constructed — so `SimulatorRunner`'s "library wins" count is structurally always zero, and
`DraftRunner` carries a comment telling you how to interpret that number.

**Fixed — decking now loses the game.** See "Decking" in `MtgCore/CLAUDE.md` for the shape.
Measured over 28 000 games: `TurnLimitReached` fell from 2–6 per run to **zero**, total draws from
8–10 to 5, and the project's **first genuine gameplay draw** appeared (simultaneous death at turn
13) — with decked games ending properly, the only draws left are ones that mean something.

The format consequences are now live and were the reason to think twice: mill is a win condition,
every long game has a clock, and card values shift enough that **CSC wants a retrain** before the
model is trusted again.

## Four games per 28 000 hang on an empty board — a choice that will not advance

**Open. The last known "dropped game" cause, and the smallest.**

After decking, dedup, the branching cap and the choice budget, 4 games per 28 000 still exceed the
300-second safety net. Their signature is now unambiguous and rules out everything already fixed:

| | Value |
|---|---|
| Turn | **2** |
| Both battlefields | **empty** |
| Actions taken in the whole game | 5–7 |
| Wall-clock | 320+ seconds |
| Stack at termination | empty |
| Last logged event | the turn-start draw |

Five minutes to choose a turn-2 play with nothing in play is not board complexity, not attack
combinatorics and not decking. The flagged indices are consecutive pairs (0015/0016, 0655/0656),
which in the schedule means **two deck-pairs each played twice** with the seats swapped — so it is
one interaction per pool and it reproduces.

**Leading candidate: `MultiTurnBeamSearchAiStrategy.ResolveAllChoices` spinning its safety cap.**
It loops up to `MaxChoiceResolutionIterations` (1000) while `IsWaitingForChoice`, and that cap
exists precisely because a choice can fail to clear its flag. It is called several times per
rollout, and a move runs tens of rollouts, so a choice that never advances costs
1000 x calls x rollouts of pure spin with no action ever recorded — which matches "5 actions, 5
minutes" exactly. Note this is a **different** path from `ResolveChoice`'s option enumeration,
which is now bounded by the rollout budget; `ResolveAllChoices` is the greedy in-rollout resolver
and is not budget-aware.

The real-game loop has no equivalent cap at all: `GameRunner.RunTurn` re-enters `ProcessChoice`
while `IsWaitingForChoice` with no iteration limit and without incrementing `TotalActions`.

Next step, cheap and deterministic: the runs reproduce, so re-run seed `deckcheck` (1000 drafts,
8 seats, CSC, Curve/Random) and instrument `ResolveAllChoices` to log the choice prompt when it
exceeds a few hundred iterations. The hands in the snapshots narrow the suspects — Fortify (modal),
Sarkhan Fireblood, Rain of Revelation and Send to Sleep all appear.

### Reproduced: the position, and what is not yet known

A 40-draft run with seed `deckcheck` replays the flagged games exactly — draft seeds are
`seed + d*1000` and game seeds `seed + 1_000_000 + index*5`, both independent of how many drafts
the run does, so game 15 of a 40-draft run **is** game 15 of a 28 000-game run. It takes ~64s.

Instrumented, one move took **30 seconds** with an empty board and five cards in hand:

```
hand: Chandra, Heart of Fire | Vampire Outcasts | Talrand's Invocation | Plains | Path of Bravery
board: (empty)
legal: CastCreature, CastSpell, PlayLand, CastPermanent, EndTurn
```

**The same move takes 30s alone and 300s+ inside a 28 000-game batch.** The hang is load-amplified,
which is why it only trips the safety net at scale — and why the 40-draft repro reports zero
dropped games while containing the identical position.

**No single card is confirmed.** Two candidates, with honestly unequal evidence:

- **Chandra, Heart of Fire** — her `+1` is `WithImpulseDraw()` twice. Every impulse-drawn card is
  offered as a castable action *and* `MtgActionGenerator.AddHandActions` scans the whole exile zone
  on every `GetLegalActions` call, so the action space grows each simulated turn of a rollout. She
  is in this hand and in flagged game 0015 of the full run.
- **Path of Bravery** — tops the "cards drawn in drawing games" table in *every* run, but at a 0.1%
  base rate that lift is one or two games, i.e. noise; its 22.8% figure dates from the wide-board
  era. Mechanically its `OnAnyCreatureAttacks` trigger fires on the **opponent's** attacks too, and
  `SimulateOpponentTurn` loops attacks until none remain, so each simulated attack costs a trigger
  plus a full state-based re-check.

**Settle it by bisection, not by argument.** The position reproduces in ~64s, so replay it with one
card removed at a time. Resist concluding from the lift tables alone — at this base rate they no
longer separate signal from noise, and that is exactly the trap this investigation has already
fallen into once.

### Tried and did not fix it: indexing the impulse-draw scan

`MtgActionGenerator.AddHandActions` scanned the **entire exile zone** on every legal-action
generation, and exile only grows — every played land is moved there — so the cost of impulse draw
was paid by every deck on every decision, rising with the turn number. Replaced with
`MtgGame.PlayableExiledIds` (the `StaticSourceIds` pattern; a hint, re-verified on read).

**Correct, kept, and not the cause.** Measured on the 40-draft repro: 64.1s → 67.9s (noise), and
the slowest single move went 30s → 17.5s with **5 moves still over 5 seconds**. The two runs used
different instrumentation so even that is not a clean comparison — the honest reading is that the
scan was not where the time went.

### Profiled — and DEFERRED here

The three phases were finally measured instead of guessed at, over 1 120 games:

| Phase | Time | Share |
|---|---|---|
| `ExpandNode` | 1 234 219 ms | **72%** |
| Level 0 | 471 330 ms | 27% |
| Choice resolution | 16 109 ms | **0.9%** |

(Summed per game across a parallel batch, so the absolute numbers exceed wall-clock; only the
ratios mean anything.) Rollouts were 672 649 at **18 executes each** — individually cheap, so the
cost is the *number* of rollouts, not their price.

**This retired the choice-resolution theory outright.** Two entries above, `ResolveAllChoices`
spinning its 1000-iteration cap was called the leading candidate on the strength of "5 actions, 5
minutes". It is 0.9% of the time. The reasoning was plausible and wrong, which is the whole reason
the profile had to come first.

It did produce a real fix — `DefaultExpandBranching`, worth **−21% run time at 28 000 games** with
no strength cost (see MtgSimulator/CLAUDE.md) — but **it did not fix the hang**: broken games went
4 → 5 (i.e. unchanged) and still sit at median turn 3 with ~305 seconds each.

**Deferred deliberately at 5 games per 28 000 (0.018%), excluded from training and costing only
wall-clock.** What the next session starts with, so none of this is re-derived:

- The position reproduces in **~64 seconds**: 40 drafts, 8 seats, CSC, Curve/Random, seed
  `deckcheck`. Games 15, 16, 655 and 656 are the same games as in a 28 000-game run.
- **Ruled out:** choice resolution (0.9%), board width (empty boards), decking, the exile scan,
  attack combinatorics.
- **Where to look:** `ExpandNode`, which holds 72% of search cost. The open question is why a
  *specific* position costs 17–30s there when a typical one costs milliseconds — a per-move
  histogram of rollout counts would show whether one move issues far more rollouts than the
  budget should allow, which would point at the budget being checked only between levels.
- **Do not conclude from the draw-lift tables.** At a 0.0% base rate they are noise, and they have
  already produced one confident wrong answer (Path of Bravery) in this investigation.

## Green's deferred clauses, and what each would cost

Four green cards reference concepts the engine has no form of at all. All four were cut and
recosted, with the cut commented on the card — the same call vigilance, menace, colours and
forced attacks all took. Listed here with prices so the next session does not re-derive them.

**Flash — Feral Invocation, Yeva, Nature's Herald.** There is no priority window; the non-active
player never acts during your turn, which is why blue's counterspells fire as traps from hand
rather than being cast in response. Building flash means building priority, which touches the
action loop, the AI's turn-based action generation and the beam search's assumption that a turn
is a closed sequence of the active player's moves. Its own project. Until then flash is worth
roughly one mana of discount on an Aura and nothing at all on a creature, because nothing here
can be responded to either.

**Delayed triggers — Hunter's Insight.** "Whenever that creature deals combat damage to a player
THIS TURN, draw that many cards." Nothing can register an effect to fire later in the same turn.
A `DelayedTriggerComponent` held on the player and cleared by `EndTurnAction` (where the
`UntilEndOfTurn` replacement cleanup already lives, for the same reason) is roughly 60 lines and
would serve exactly one card today. Rebuilt as an immediate draw off the creature's power, which
is the same card whenever the attack was going to connect, and costed one more because it no
longer requires the attack at all.

**Opening-hand permanents — Leyline of Vitality.** "If this is in your opening hand you may begin
the game with it on the battlefield." `SetupGameAction` deals seven cards and there is no mulligan
step, no pre-game window and no player decision anywhere before turn one. Clause dropped; the rest
is an ordinary enchantment. Adding it means giving the game a pre-game phase, which is a bigger
change than the card is worth.

**Divided damage among your OWN creatures — Master of the Wild Hunt.** The red section already
established that "damage divided as you choose" is sprayed rather than partitioned, but Master's
return half divides the target's damage among the Wolves that attacked it, which is a partition
over a set the card itself selects. Kept the aggressive half (damage scaled by Wolf count), cut the
return half, costed one more.

**Removing a +1/+1 counter as a cost — Barkhide Troll.** Counters became real in this section, but
`RemoveCounterAdditionalCost` did not. It is ~40 lines (`Validate`, `Pay`, `Describe`,
`RequiredPaymentCount`) for one card, and what the removal DOES is bound the hexproof, which
`MaxActivationsPerTurn = 1` already does for nothing. Build it when a second card wants to spend
counters — at that point it also needs to interact with `PlusOneCounterComponent`'s entry-strip
rule, which is the part that will not be obvious.

**Lands as battlefield permanents.** Six green cards want this (Gift of Paradise, Garruk
Wildspeaker, Nissa Worldwaker, plus the fetch-to-battlefield ramp). All reskinned onto mana per
the Knight of the White Orchid precedent. Making lands real permanents would touch
`PlayLandAction`, the entire mana system, every battlefield scan, the AI, and the trained draft
model — and the reskins play close enough that nothing in the section feels wrong. Not worth it
for this set; revisit only if a colourless or multicolour card makes lands genuinely load-bearing.

## The low win-rate band is a bug detector, not a balance signal

Ten green cards came back at 39–43% after the first training run. **Seven of them did literally
nothing.** A blank card and a genuinely weak card score identically in this metric — around 40% —
which is precisely what makes that band worth auditing card by card before touching a single cost.

The three root causes are written up in `MtgCore/CLAUDE.md` ("Auditing the bottom of the win-rate
table"). The point worth keeping here is the method:

- **Assert the consequence, not the resolution.** All seven cards cast, resolved, rendered correct
  rules text and threw nothing. `EveryCard_CanBeCastAndResolve` passed on every one of them. Only
  "did a card move / did the board change" caught them.
- **Two of the three causes were shared infrastructure, not card definitions.** `WithDig` broke
  seven cards across three colours; the battlefield-only creature spec broke four. Fixing the
  cards one at a time would have left both traps armed.
- **Check the harness before blaming the card.** `ProcessAllActions` stops at a `ChoiceAction`, so
  any card with a scry or a mode looks inert in a naive test. Three of the seven were partly this.

**A fourth bug came out of the same audit, and it was not a card bug at all.** Primordial Hydra
sat at 39.2% because the AI's chain replay matched cast actions on card id alone, so a planned
"cast for X=4" re-found the X=0 action — see `MtgSimulator/CLAUDE.md`. That one affects every `{X}`
card in the cube, not just green. **When a card looks inert, the AI's action handling is a
candidate cause alongside the card definition**; nothing in the card, its rules text or its
engine mechanics was wrong.

**What this implies for the remaining low band.** The non-green cards in the same 40–43% range —
Demonic Pact, Frost Breath, Sphinx of Uthuun, Titan's Strength, Dark Tutelage, Gods Willing,
Molten Vortex, Disenchant, Call to the Grave, Blood for Bones — have NOT been audited this way.
Two of them (Drawn from Dreams, Fateful Vision) were already fixed as collateral from the `WithDig`
repair, which is evidence the band still contains bugs rather than just weak cards. Audit before
rebalancing.

## Choices have owners; the non-active player can be asked to decide

**Decision: the engine supports a player answering a choice on the opponent's turn.** A
`ChoiceAction` carries an owner (`GetDecidingPlayerId`), and both front ends route the question to
that player rather than to whoever is taking a turn.

The alternative considered was restricting choice-raising triggers to their controller's turn.
Rejected on two grounds:

- It would not have fixed the bug that prompted the discussion. Avaricious Dragon's trigger is
  already controller-turn-only (`TurnEndedEvent` → `ExtractSubjectId` → the player whose turn
  ended). It looked cross-turn because `EndTurnAction` flips `ActivePlayerId` *before* staging the
  event, so the UI's turn-based guess read "human".
- It would silently gut death triggers. Shadows of the Past ("whenever any creature dies, scry 1")
  and Return to the Winds (scry 2 on death) resolve on whichever turn the creature died, usually
  the opponent's. Rules text would still promise the scry. Hollowmere is a graveyard set, so this
  is the mechanic that set is built on.

**This is not priority and does not open the door to it.** A mid-resolution choice is a question
asked while something resolves; priority is a response window. Flash and counterspells stay cut.

**If cross-turn choices play badly, constrain the CARDS, not the engine** — design triggers so it
does not arise. That is reversible; an engine that cannot express it is not.

**Watch:** the AI turn stops and waits on a human-owned choice (`MtgGameScene.RunAiTurn` returns;
`OnChoiceConfirmed` resumes it). If the player ignores the panel the opponent's turn is stalled —
waiting on input, not hung, but it has no timeout. `_aiSteps` is a field so an interrupted turn
keeps one step budget rather than restarting it on each resume.

## RequiresTap does nothing on a non-creature permanent

`ActivatedAbilityAction.ValidateAdd` gates the tap check on `ability.RequiresTap && creature != null`,
and the exhaust write in `Execute` scans for a `CreatureComponent` and finds none. So on an artifact,
`RequiresTap = true` is inert. `PermanentCardBuilder.WithActivatedAbility` does not even expose the
parameter, so nothing in the Core Set Cube can currently set it by accident.

**Left alone deliberately.** For a `{T}:` artifact ability, `MaxActivationsPerTurn = 1` produces the
same once-per-turn behaviour, and artifacts correctly have no summoning sickness, so the common case
is already right. `IsExhausted` lives on `CreatureComponent`; making it work here means either
hoisting it onto `PermanentComponent` (touching every creature read in the engine) or a parallel
exhausted flag for non-creatures (a second source of truth for one concept).

**What it actually costs:** a permanent with TWO `{T}` abilities can use both in one turn, where
real MTG locks the second. Three cards in the colourless section are printed that way — Dragon's
Hoard, Meteorite, Scuttlemutt — and all three lose their second tap ability to the colour and
counter cuts anyway, so nothing shipped depends on it.

**Watch:** the next card that wants two competing tap abilities. It will build, activate and resolve
without erroring, at twice the printed rate. The cheap fix if one arrives is a shared activation
group id on `ActivatedAbilityComponent` so two abilities share one per-turn counter — much smaller
than a general permanent-tapping system, and it covers the only case that has ever come up.

## Platinum Angel can stall a game past the turn cutoff

`CannotLoseComponent` suppresses the loss in `CheckStateBasedEffectsAction.CheckLossConditions`, the
one place a player can lose. Life still falls and libraries still empty; only the outcome is held
back, so killing the Angel collects the loss on the next state-based check rather than merely
stopping the bleeding.

**The accepted cost:** an unanswered Angel means neither life nor decking can end the game. The
simulator warns at 50 actions per turn and cuts off at 100, and `GameEndReason.TurnLimitReached`
went to zero when decking started killing — an Angel that never dies puts games back into that
bucket, where a stalled game is indistinguishable from a bug.

Judged acceptable: it is one card in 450, both players run removal, and the Angel is a 4/4 that
can simply be attacked. **Watch:** `TurnLimitReached` in a training run. If it correlates with
Angel being drafted, the honest fix is to keep the life-total protection and drop the decking
protection, so the game still ends — not to weaken the clause everywhere.

## The evaluator oscillates: both directions of one move score as an improvement

Found chasing the Swiftfoot Boots draw loop, and it is a bigger problem than the loop was.

With equip {0}, the AI moves the boots between two creatures forever. That looks like a scoring
tie broken toward acting, and **two AI-side tie-break fixes were built on that assumption before
anyone measured it. Both were wrong and both were reverted.** Instrumenting `PickBestNode` on a
board taken from a flagged game (Avaricious Dragon / Chasm Skulker / Sublime Archangel, all having
already attacked):

```
root=ActivateAbilityAction  baseline(EndTurn)=72.7000  equip=74.1000
```

The equip scores **+1.4 over ending the turn** — and moving the boots straight back scores +1.4
again. There is no tie. The evaluator rates both directions of the same oscillation as a genuine
improvement, so no "prefer to stop on a tie" rule can ever catch it, at any bias value.

> ### RESOLVED 2026-08-25 — and the diagnosis above was wrong
>
> **The evaluator was never at fault.** `ExecuteAction` was `TryAddAction` + `ProcessAllActions`,
> and `ProcessAllActions` stops at a pending `ChoiceAction` — so any candidate whose trigger asks a
> question came back HALF-APPLIED: executed, triggers unresolved, active player unchanged.
>
> `EndTurnAction` on this board fires Avaricious Dragon's Squander (discard 1 when your turn ends),
> which raises a discard choice. The candidate therefore returned with game time **unchanged** and
> `IsWaitingForChoice` true. The rollout read that state, saw it was still our turn, and **ended
> the turn a second time** — firing every end-of-turn trigger twice. The 1.4 is one extra discard
> at `CardsInHandWeight`.
>
> With `ExecuteAction` draining the choice, equip and EndTurn score **74.10 each — identical**. So
> there IS a tie, and the two reverted tie-break fixes were sound reasoning on a false premise.
>
> **Do not reintroduce a ties-to-EndTurn rule anyway.** It was tried after the fix and reverted:
> Swiftfoot Boots grants keywords the evaluator has no term for, so every equip scores exactly
> 0.00, and the rule made the AI never equip anything.
> `FreeAbilityLoopTests.Equip_IsStillTakenOncePerTurn` is the gate that caught it.
>
> **How this cost four diagnoses.** The symptom was that the preference INVERTED with search depth
> (End Turn wins at lookahead 0 and 1, loses at 2), which is the textbook signature of a horizon
> effect. Three fixes were designed against that reading — bundled extra actions, turn-boundary
> counting, choice ownership — and one was fully built (an absolute game-time anchor) before being
> reverted, because it changed nothing. What settled it was printing `IsWaitingForChoice` on the
> two candidate states, which took two minutes. Both had already reported the same game time in
> the first trace taken, and it was read past.
>
> **A symptom that matches a textbook pattern is not evidence of that pattern.** Same lesson as the
> wall-clock investigation the same week.
>
> Pinned by `MtgSimulator.Tests/HalfAppliedActionTests`, which also asserts the bug REPRODUCES with
> the fix disabled — without that, the gate would pass on a board where the defect never applied.
>
> Measured at **50.2% ± 1.5pp over 1120 games**, i.e. no aggregate strength change. See
> `MtgSimulator/CLAUDE.md` for why a head-to-head is the wrong instrument for this class of fix.

A second instance of the same blindness, on a different board: a ready 9/10 Tarmogoyf facing a
7/8 and a 4/5 at exactly lethal. Killing the 7/8 outright while surviving scores **-0.9784**, and
ending the turn scores **-0.9784** — identical to four decimals.
`MtgSimulator.Tests/MultiTurnBeamSearchBugTests.AttackTargeting_TarmogoyfVersusLethalBoard_
AttacksCreatureNotFace` has therefore always passed on the tie-break rather than on evaluation.

Both point at `ScoreAfterCompletingTurn` rather than at `StateEvaluator` itself: every root
completes the turn greedily and then rolls out, so distinct positions can converge on the same
rolled-out future, and small differences in the greedy tail can dominate the real board difference.
Worth investigating in this order:

- The greedy turn-completion converging both lines, averaging the real difference away.
- `RacePressure` being symmetric and cancelling a removed attacker against the damage taken.
- `SimulateOpponentTurn(BoardOnly)` not reflecting that a dead attacker cannot attack next turn.

**Until this is fixed, unbounded free abilities must be bounded in the ENGINE, not in the search.**
`PermanentCardBuilder.WithEquip` caps equip at once per turn for exactly this reason. Do not
"fix" a loop by retuning `EndTurnBias` in either direction — the bias is a tie-break, and these
defects are not ties.


## A discard-a-land cost is priced at zero

`StateEvaluator` counts only NON-land cards in hand, and that is correct on its own terms — see
`AiLandDropTests`, where counting them made a land drop worth a net +0.6 and the AI started
skipping early drops. The consequence nobody followed through is on the cost side: **a cost that
discards a LAND is free**, while the same card played is worth a permanent +2.0 of MaxMana.

Measured (`MtgSimulator.Tests/LandDiscardCostTests`):

| | Evaluator delta |
|---|---|
| Discard a land — what Molten Vortex charges | **+0.000** |
| Play that same land | **+2.000** |
| 3 damage to the opponent — what Vortex pays | **+0.600** |

So the AI sees a free +0.6 where the real trade is 2.0 for 0.6, and takes it whenever it holds a
land to spare. The only thing pushing back is the 2-turn rollout, which is why the behaviour has a
sharp boundary: it keeps exactly one land in reserve and vents every drop beyond it, at any mana
total. A 40-card drafted deck runs 17 lands and wants drops through roughly turn six, so those are
turn-three-onward drops being burned for 3 damage each.

This is why Molten Vortex measures near the bottom of the model. It is an AI pricing problem, not a
card rate problem — raising its damage 2 -> 3 makes the bad trade *more* attractive, so if that
change does not help, this is the reason.

**Affects every discard cost, not just Vortex.** Magmatic Insight has the same shape, and
`DiscardAdditionalCost.Filter` exists precisely so cards can demand a land.

The fix is a small NON-ZERO weight for lands in hand — enough to price the cost, low enough that a
land drop still beats holding (the drop must clear `2.0 - landWeight` by a decisive margin). That
number is exactly what `AiLandDropTests` pins from the other side, so it cannot be set by
intuition: it needs the head-to-head strength harness (`BranchingCapStrengthTests`), not a guess.
Deliberately not changed here.

## Rules-derived synergy for constructed evolution

**Status: deferred, needs its own design session.**

`MtgSimulator/Evolution/` learns synergy from measured pair win rates, which covers genuine
anti-synergy (a sweeper alongside your own creatures, once enough games exist) but **cannot
express a critical-mass requirement at any sample size**. Atog does not want one artifact, it
wants ~15; that is a threshold on a whole deck, not a property of a pair.

Measured pairs also cannot help at all across sets. On the combined pool 58% of the pair space
is cross-set, and sets are drafted separately, so those pairs have never co-occurred in any
training game and never will.

The proposal is to read the signal off components, which already encode it:

| Pattern | Signal |
|---|---|
| `DestroyCreatureAction` + `AllValid(Creatures())` | −per creature in your own deck |
| cost/count filtered on a subtype (`SacrificeAdditionalCost` with `IsSubtypeSpecification`, `CreatureCountComponent.Subtype`) | +per matching card in deck |
| `ActiveInZone = Graveyard`, mill payoffs | +per self-mill enabler |

**This is not the card labelling rejected at design time.** Nobody types "aggro" on a card —
the components are the card's behaviour, so the signal cannot drift from what the card does and
there is no upkeep cost. Day of Judgment is already `DestroyCreatureAction` over
`AllValid(Creatures())`; Atog is already a `SacrificeAdditionalCost` filtered to `"Artifact"`.

Costed options when this is picked up:

1. **Three or four rules, scoring only.** Cheapest. Feeds the same fill/cut scores the measured
   synergy does. Does not change what a deck is allowed to contain.
2. **Threshold-aware deck coherence.** Adds "this payoff wants N enablers" as a deck-level term,
   which is what Atog actually needs. Requires deciding N per pattern — the first place a
   judgement call creeps in.
3. **Full archetype tagging.** Rejected already; recorded here so it is not re-proposed.

Do 1 first and measure before considering 2. The existing A/B shape is the mode's own
constructed-vs-limited diff plus a field-quality comparison at a fixed seed.

**Superseded by `SynergyFeaturePlan.md`** at the solution root, which carries the measured
sparsity numbers, the four costed options, the baseline runs to A/B against, and the trap list.
Read that rather than this entry.

---

## Pathologically slow games: why presim costs 2.5x, and why it must be understood

**Status: known, unexplained, and now visible instead of hidden. Priority raised deliberately.**

`GameRunner.SafetyTimeoutMs` was raised 300s -> 1800s because presim EXCLUDED `TimeLimitReached`
games, which made a wall-clock reading decide which data counted and left mode 6 irreproducible at
a fixed seed (see `MtgSimulator/CLAUDE.md`, "The clock was removed from ENDING a game and left in
DISCARDING one"). The fix is verified: runs are now bit-identical.

The cost, on CSC presim at 800 decks:

```
before:  9585 games in  9.4m, 1 excluded
before:  9584 games in  8.9m, 2 excluded
after:   9586 games in 23.5m, 0 excluded
```

**~14 minutes for one or two games.** That is not noise — the two "before" runs bracket 8.9-9.4m,
and the batch is parallel, so a lone straggler dominates the tail once everything else has drained.

### Why this matters more now, not less

Raising the timeout did not create the slow games; it stopped **concealing** them. Before, they
were silently discarded and their cost capped at 300s. Now every run pays their real cost, so:

- It is a standing ~2.5x tax on every presim, and mode 6 is already the most expensive mode.
- It scales with pool size. ALL is 785 cards against CSC's 408, and ALL runs were already
  disproportionately slow.
- **A game taking 1000x the median is evidence of something, and nobody knows what.** It may be
  legitimate (the deterministic bound genuinely permits it — 100 turns x 200 actions at ~70 ms per
  `SelectAction` is ~23 minutes) or it may be a real defect: a near-loop the action limit does not
  catch, a board state where branching explodes, or a choice-resolution blow-up of the kind
  `ChoiceCensus` was built to detect.

**Do not "fix" this by lowering the timeout again.** That reintroduces the exact bug just removed.
If the cost has to come down before the cause is known, the lever is the DETERMINISTIC bound —
turn limit or per-turn action limit — which changes what a game is but keeps runs reproducible.

### Cheapest way in, when it is picked up

`GameResult.GameDurationMs` already exists and `PreSimulation` already holds every result before
folding them in. So:

1. Report the p99/max game duration and the end reason of the slowest few, alongside the existing
   "N games in Xm" line. Cheap, and it says immediately whether the straggler hits the turn limit,
   the action limit, or simply ends normally after a long grind.
2. If it is not hitting a limit, save a snapshot of the slowest game — `FlaggedGameSaver` does this
   already but only fires on flagged results, so it needs a "slowest game" trigger.
3. Read `Avg actions/game` beside the clock, per this project's standing rule: actions up means
   more work, actions flat with time up means the cost is per-action and the search is the suspect.

### Related, unclaimed optimisation

An A/B runs presim TWICE for identical inputs — both arms use the same set, seed, deck count and
opponent count, so both compute a bit-identical result. Caching presim output keyed on those four
values would halve the cost of every future comparison. Not built; noted because the A/B shape is
now the standard way anything in this mode gets measured.

## FIXED — `dotnet test` under `MTG_MIN_LANDS=12` silently ran 66 fewer tests

> **Resolved in the same session it was found; kept as the diagnosis, not as a live warning.** The
> cause was an unguarded grow loop in `OutputProbe.Compare` — see `MtgSimulator/CLAUDE.md`. The
> suite now reports **348/348 with `MTG_MIN_LANDS` set to 12 and unset alike**. What still holds is
> the reading rule at the bottom: check the TOTAL, not just the word `Passed`.

Measured, same binary, MtgSimulator.Tests:

| environment | discovered | executed | result |
|---|---|---|---|
| `MTG_MIN_LANDS` unset (default 20) | 421 | **348** | all pass, 42s |
| `MTG_MIN_LANDS=12` | 421 | **282** | all pass, 23s — **and testhost never exits** |

Discovery is identical, `Skipped` is 0 in both, and nothing fails. The 66 missing tests are simply
never executed: `OutputProbeTests` wedges the host partway through, vstest reports only what had
completed, and everything queued behind it is silently absent. Isolated by running the four
game-playing fixtures separately under 12 — `ConduitBehaviourTests` (5 passed), `ContextValueTests`
(1 passed) and `TwinComboPilotTests` (6 passed) all finish in seconds; `OutputProbeTests` never
reports.

Why the variable changes anything: `OutputProbeTests.Lands` is `Math.Max(Decklist.MinLands, 17)`,
so the fixture is a 20-land deck at the default and a **17-land** deck under 12. The extra three
spells are what tips it. The turn cap is enforced (`GameRunner` checks `TurnNumber > _maxTurns`
every iteration), so this is not unbounded turns — the standing suspect is choice resolution, which
this file already records as the one cost `TotalActions` does not count.

**Read this as the reason a green suite is not proof.** A run under 12 reporting `Passed! 282` looks
healthier than the default's 348 and is strictly less evidence. Same class as the two vacuity
failures in `MtgSimulator/CLAUDE.md` — a uniformly-failing measurement produces a plausible number.

Operational rule, and it outlives the bug: **read the TOTAL, not just the word `Passed`.** 348 is
the number to expect on MtgSimulator.Tests; a run reporting fewer has lost tests somewhere, and
`Skipped: 0` will not tell you.

---

## The splash gets 2.1 of 17 lands, and the allocator is not where that gets fixed

`ManaBase.Requirements` / `Shortfall` (new) price a deck's colours as the MAX per colour rather than
the sum: a WW two-drop beside a W five-drop needs eighteen white sources, not twenty-seven. Summing
across colours is valid only because a basic produces one colour, so the lands serving each demand
are disjoint — **dual lands break that function specifically.**

Threshold-first allocation was then built into `ManaBase.Allocate`, measured, and removed. The
intuition is right and the pathology is real — measured over 400 mono-plus-two-card-splash decks on
CSC, the splash colour gets **2.1 of 17 lands** for a card the table says needs nine or ten. What the
measurement killed was fixing it *there*:

| sample (CSC, 23 spells, 17 lands) | decks whose requirement fits in 24 |
|---|---|
| random within an identity | 200/600 — and all 200 are the mono identities, where allocation is trivial |
| mono core + two-card splash | **0/400** |

Any 21-card mono core already contains a double pip worth 17-18 sources on its own, so every
realistic two-colour deck totals 27-31 against a 24-land budget. **No allocation serves both
colours.** Handing the splash its threshold means taking two lands off a main colour full of double
pips, and which of those decks wins is empirical — the allocator cannot answer it, so it stays
demand-proportional and the choice moves to whoever builds the deck, priced with `Shortfall`.

Two things to carry:

- **A gate that never fired passed the entire suite.** 410/410 green with the branch dead in every
  realistic deck shape. The measurement, not the tests, is what caught it — same family as the
  `MinPairGames = 200` scar in HANDOFF-Colours.md §5.
- This is more evidence for run B's "decks collapse to mono inside a two-colour identity". A splash
  that cannot be paid for should not be drafted or built, which is a **builder** rule, not a mana
  rule. If steps 3-5 of the colour work enforce it, the threshold allocator may never be needed;
  revisit only with a head-to-head showing the trade wins, or when dual lands arrive and change the
  arithmetic above.

---

## `StateJsonTests.ARoundTrippedState_ProducesTheSameAiDecision` is intermittently flaky

Observed once in three consecutive full runs of `MtgSimulator.Tests` (421 tests), passing in
isolation and passing on both re-runs. Cause not identified. It was NOT investigated further
because the failing run produced no assertion detail and could not be reproduced.

Two candidates worth checking if it recurs, both consistent with the working-directory scar already
recorded in the root `CLAUDE.md`:

- `AiCardValues` loads from `sim_results/` relative to the CURRENT directory. Anything that calls
  `TestPaths.ChdirToSolutionRoot()` mutates process-wide state while other fixtures may be running,
  and a table loaded from the wrong place is EMPTY rather than absent — every card reads 0.00pp and
  the AI makes a different decision. The diagnostics added recently all call it, but all are
  `[Explicit]` and so should not run in a normal suite; verify that assumption before dismissing it.
- The same family as the `OutputProbeTests` entry above: this suite has a history of tests
  interfering through the shared host rather than through their own logic.

**Read a single green run as weak evidence on this test.** It is the one test whose failure would
otherwise look like a real serialization regression.


## MagicCore known ceilings

The real-rules engine (branch `mtg-real`, `MagicCore/`). Each is fine for now and marked `ponytail:`
in the code with its upgrade path; revisit when a card or the AI needs it.

- FRA packs meet Wizards' "four of the five colors" at common by redrawing the common sheet (`Limited/Boosters.cs`, LIM-1), not by FRA's real print runs, which are unpublished — collate by runs if opened-pack data ever reveals them
- the sealed builder splits basics by the spells' pips, not by WHEN they are needed (`Limited/SealedBuilder.cs`, LIM-1) — weight early pips more if builds come out colour-screwed
- the sealed suggestions' mana is a rule of thumb (`SealedBuilder.Suggestions`, LIM-3): sources counted, not simulated — 7 per main colour, 6 per real extra, 3 per splash — with every fixer as 1, a colour-choosing land as a source of EVERY colour at once, and a landcycler or land-searcher as one of every colour the deck has at least one basic of; and a build is scored by its cards' ratings alone, so nothing prices inconsistency (a four-colour build needs a bomb three can't play — Shayne). LIM-3.3's simulated draws replace it
- a card with under 200 games in hand in both 17lands formats is pulled toward the set's median (`Limited/CardRatings.cs`) — a rarely played card is usually weak, so the pull may flatter it; the engine's own rating (DATA-2) can replace it
- the sealed screen's deck rows print a cost as its text ("{1}{W/U}", `MagicTable/SealedScene.cs`, LIM-1) — draw pips in a RichTextLabel with the mana icons if it reads poorly
- Extrapolate the Impossible measures 0 in `CardValues` — the fixture has no sideboard, so it does nothing there; the search still judges a cast by its result. Give the fixture a sideboard if the AI holds it wrongly
- the AI reveals its two highest mana values for Extrapolate the Impossible (`GreedyAi.Answer`; rollouts' `QuickAnswer` takes the first two) — mana value stands in for worth; price by held values if it misplays
- the search knows the opponent's sideboard as it really is (`Hidden` samples hands and libraries only) — it matters only to Extrapolate's "an opponent chooses"; sample it if that pick ever looks clairvoyant
- Damage split among blockers is automatic
- an exhaust stamp is per card, not per ability
- triggers are matched against the battlefield at the next priority, so a source, or an entering creature, that is gone by then — destroyed later in the same resolution — is missed (one the state-based check kills is still seen)
- the legend rule keeps a card over a token, then the newest
- last-known information keeps a card's own characteristics, not what statics gave it
- a dying card's own "when this dies" looks back, but other leaves-the-battlefield triggers read the battlefield NOW (Edgar dying alongside a creature misses its death)
- a "sacrifice the greatest" tie takes the first
- `Characteristics.BoardOf` caches ONE board per thread (AI-7.3a): a caller asking about two states in turn rebuilds it every time — keep two entries if one ever must; each thread keeps its last state alive
- the search's guessed opponent hand (`Hidden.Hand.GuessedFromColors`, AI-3.5) takes the deck to be 40 cards with 17 lands, and its cards FRA's — read both from the game if another format or set plays
- `GreedyAi` (no longer the default AI — the search is, AI-2) is one ply with a passive opponent — no turn planning, no holding up mana, no "do I need this mana now" when choosing a tapped dual, and it loses slow races to evasive fliers (`docs/findings/magic-ai.md`)
- menace drops a lone blocker instead of refusing the declaration
- the auto-tapper ignores what the rest of the hand needs
- Empower Jace with two Jace tokens loads the first
- surveil keeps the kept cards' order.
- "when enchanted creature dies" sees what was attached as the creature moved; if ONE effect moves the creature AND its Aura, the Aura moved first is missed — no FRA card does that (record attachments per resolution if one ever does)
- Tarmogoyf's */1+* (and any characteristic-defining P/T) is a battlefield static, so off the battlefield it reads as its printed 0/1 — nothing reads a creature card's power outside the battlefield yet (make CDAs a Card-level read if a card ever does)
- a static's SCOPE is refused if it would make Characteristics loop, but its Power/Toughness `Count` and its `If` are not checked — one reading P/T or keywords through a Ref's filter would recurse (check them in `StaticEffectComponent.Scope` if a card ever needs such a count)
- "Tap two untapped artifacts" (Tenured Tethermage) picks which ones for the player — what makes no mana first, then least; make it a choice if the pick ever matters more than that
- a permanent's mana abilities share one restriction (the first one's); no FRA card mixes restricted and unrestricted mana, and nothing grants mana abilities (track it per unit if one does)
- a Sculpture Treasure that may convoke is offered only for convoke (tapped, not sacrificed), never sacrificed for a colour in the same payment
- "Whenever this creature is dealt damage" (Hexhaven Invigorator) triggers once per damage SOURCE: two blockers' combat damage is two triggers, where the rules make it one for the total (coalesce per damage step if a card ever cares)
- Kindred Judgment offers the creature types on the battlefield plus "a type no creature has" — every other type does the same as that one
- "Up to that many target creatures" (Seasoned Cryomancer) keeps only neighbouring targets apart; three or more could repeat one (make it all-distinct if a card ever asks for three)
- a `DelayedTrigger` is one-shot only because Vindictive Triumph's moves the permanent; one that leaves it in place would fire every end step (mark it fired if a card ever needs that)
- Proliferate (Tam) picks for the player: your permanents' +1/+1, loyalty and charge counters, opponents' -1/-1 and stun — never the other kind; make it a choice if a card ever wants otherwise
- Puppet Crafting's type change is stamped on the host and put back by the state-based check, not a live layer-4 effect: between the Aura leaving and the next priority the host is still a creature (every rule reads card.Is(Creature) — move types into Characteristics if a card ever cares about that window)
- Uldaros chooses its graveyard cards as it resolves, not as targets when it triggers ("one of each card type, none twice" is a target word the engine can't say)
- a "this turn" effect in the command zone (`ThisTurnEffect`) ends as the next turn begins, with `TurnTally`, not in cleanup (CR 514.2) — it differs only when a cleanup gives priority; move both into cleanup if a card cares
- counters on `Card` are six fields (`PlusOneCounters`, `MinusOneCounters`, `StunCounters`, `ChargeCounters`, `HasFinalityCounter`, `Loyalty`; ~250 uses): a new kind means editing `Reset`, `RemoveCounters` and `Proliferate`, which list them by hand. Make them a keyed map when a set brings a new counter kind (decided 2026-10-15, filter refactor phase 7)
- Emrakul's "{T}: Add {C}{C}" replaces the land's own mana ability while she waits in exile, instead of being a second choice — one yield per permanent; so her "target land" is a land YOU control, or it would shut off an opponent's colour (make yields alternatives if a card ever needs it)
- the search reshuffles both libraries by default (`Hidden.Library.ShuffleUnknown`, since AI-3.9 / `26c6dc2`) — honest, and measured 3–6pp weaker than reading the REAL order (findings, AI-3.3), which `+realorder` keeps for measurement
- the rollout judges each attacker ALONE (the others declare nothing in its look-ahead), so an attack that only works together — two 3/3s into one blocker for lethal — is never found; port MtgSimulator's FindLethalAttacks if rollouts need it
- the look-ahead's safe block (`Lookahead.Survives`, AI-8.3) counts one attacker's full damage on one blocker — damage split among several blockers, prevention and protection are not seen; count them if a misjudged block is shown
- the simulated opponent's walker (`Rollout.TheirLoyalty`, AI-8.1) takes a minus that kills itself like any other (a Jace at 1 surveils and is gone) — a real player might keep the token for the next Empower; weigh that if a walker misjudged so shows
- a planeswalker's price (`Evaluator.Walker`, AI-8.2) reads only creature tokens and card draw from its abilities; damage, mana, life, pumps and exile are unpriced (the old flat body of 2 stands in), and half a card's draw stays under that body — so cashing a 3-loyalty Jace for a card still scores as a loss (monotonic over Shayne's "not a loss"); price an effect when a misjudged walker shows it
- a rollout that hits its DecisionLimit is scored where it stopped, mid-turn, beside rollouts scored at the horizon — only a loop that never advances gets there; flag or drop such a candidate if one is ever seen
- the search's table time cap (`BeamSearchAi.Settings.TimeLimit`, 1 s) gates only the lines below the root: a 16-move root always completes — 1.1–2.2 s measured; with the cap 10 of 1 913 decisions ran over 1.2 s, the slowest 1.65 s (docs/findings/magic-ai.md); skip root rollouts past the limit, scored by the cheap evaluation, if that shows on the table
- the search runs on the table's main thread, so the window does not redraw while it thinks (the slowest `StepAi` measured 1.1 s); move `Decide` to a worker and apply on the main thread if a playtest calls it a freeze. Undo rebuilds the AIs (the search commits to a plan that an undo would put out of step)
- the search AI (`Ai/BeamSearchAi.cs`) never responds to its OWN object on top of the stack — in its tree or for real — so a line that protects its spell with a second one is never found; branch that window if a deck needs it
- a revealed card in the opponent's hand is priced as itself only by what the search knows (`Ai/Evaluator.cs` Side, AI-9.5) — under `Hidden.Hand.Real` (the default) a reveal played INSIDE the look-ahead prices their real hidden cards, a peek; price only cards known at the search's root if a reveal spell is ever misjudged
- the default simulated opponent plays only the cards we know (`Ai/Rollout.cs` Allowed, AI-10.4) — no unknown land either, so its mana doesn't grow in the look-ahead and a known card it can't yet afford stays uncast; allow one land a turn if that misjudges a bounce
- patience is one flat cost (`Evaluator.PatienceCost` = 1.0, AI-13.2) on every instant or flash card cast at sorcery timing, by the search's tree and our rollout policy alike — chosen, not measured; scale it by what the card does at a later window (AI-4's held spells) if a hold looks wrong

## MagicCore search: speedups not taken (AI-7, 2026-10-19)

AI-7 cut the search's allocation in half with no decision changed (`docs/findings/magic-ai.md`,
"Decision-neutral speedups"). Three measured candidates were left, each costed:

- **Attack/block score reuse** — when creature k's attack (or block) is judged, the option
  `QuickAnswer` would give it replays the line creature k−1's chosen option already scored, so its
  score is known. Measured: 16% of combat simulations, ~6–7% of the search. Cost: `Rollout.Answer`
  returns its chosen child state and best score; `Rollout.Score` and the opponent's step carry the
  previous judgment, matched by reference; `Settle`'s 200-step cap is met a step apart on the two
  paths (a ponytail). Take it if combat judging grows (more creatures, AI-3's attack models).
- **`LegalActions`** — the largest single allocator left (12 KB a call): every candidate (targets,
  modes, X) through `ValidateAdd`'s payment search. Legality's one place (MagicCore's first rule), so
  a cheaper generator must not decide legality itself. Look first at how many candidates fail
  validation, and at the payment search's allocation.
- **`ZoneId`** — still a cached string key, a tuple-keyed lookup and a string-keyed dictionary for an
  id fixed all game. Ids on `MagicPlayer` (or a per-game array) would make it a field read; ~0.1 µs a
  call, under every `CardsIn`.

## MagicCore search: rollout models not taken (AI-3, 2026-10-19)

Measured each alone (`docs/findings/magic-ai.md`, "AI-3's matrix"); off by default, each a `--match`
name. Two worth another look, with their cost:

- **GreedyAi as our rollout policy** (`+greedyrollout`) — +2.8pp at about 2 SE, undecided at 600
  games, for +61% engine steps per decision. Under the 5pp rule a reasoning call: not worth the table's
  time. Take it up if the search gets cheaper (AI-7's leftovers) or a 1 200-game run is wanted (~35 min).
  It also mixes two changes (more plays a turn; strictly-better instead of "no worse").
- **All-in attacks with judged blocks** (`+allin` was WORSE by 5pp) — the look-ahead's defender never
  chump-blocks (`Lookahead.QuickAnswer`), so all-in looked lethal where a defender chumps. Settle the
  all-in line with the rollout's own judged blocks (`Rollout.Answer`), then measure again; cost: a
  block judgment per blocker inside every all-in settle.
- Not run: combinations (a richer opponent with GreedyAi rollouts, honest hand with real library).

## MagicTable does not use Common's Card2D or drag-to-play (hand done: TBL-3.2; battlefield deferred)

**Taken up, 2026-10-23 (TBL-3.2):** the hand is Common's fan (`MagicHandView`, from KIN) with click
and drag; the battlefield keeps its crops. The rest below is the original note.

Shayne, 2026-10-09: the MagicTable front end (`SQGodotCommon/MagicTable/`) draws every card as
plain Controls built in `CardViews` — a `TextureRect` of the Scryfall image in the hand, a Panel
crop on the battlefield — and plays by click. It does NOT use `Common/Cards/2D` (`Card2D`,
`Hand2D`, `DraggableNode2D`) or drag-to-play, which the other games share. **Building games on the
common features is the standing intent; this was a miss, deferred so playtesting could start.**

Why it went this way: Card2D renders a card from game data through its own card scene (frame,
name, cost, rules text); MagicTable shows Scryfall's printed card instead, so the Oracle text is
exact for checking cards. The two had to be reconciled and weren't.

Options, roughly in cost order:
- **Hand only:** `Hand2D` + `Card2D` with the Scryfall image as the whole face (no frame, no text
  layers) and drag-to-play onto the table → the same `ObjectClicked` router. Keeps the
  battlefield crops. Read `.claude/rules/godot-frontend.md` "The shared card" first — Card2D's
  0.7 scale, ready-only fill, hover/drag traps.
- **Hand and battlefield:** permanents as Card2D too (tapped = rotated Node2D). Lays out by hand,
  since a Container won't lay out a Node2D — the whole row code changes.
- Either way the click paths stay (Arena supports both), and `IsMoveFor` / `Targeting` are reused.

## MagicCore's AI plays implausibly — one-ply greedy is not enough (done: AI-1, AI-2)

**Taken up, 2026-10-16:** AI-1 priced held cards by their measured value (the Rank Rat is kept), and
AI-2 ported the beam search — the table's and the console's default since it beat GreedyAi 111–45
(`docs/findings/magic-ai.md`). Kept below for the reasoning.

Shayne, 2026-10-09, playing the table: the AI cast **Prophesied End** ("Destroy target creature.
If it wasn't attacking, its controller draws a card.") on a **Rank Rat** that was not attacking —
spending premium removal on a 1-drop AND giving him a card. A plausible opponent is needed for
playtests to mean anything.

`GreedyAi` simulates each move one ply and scores the result; it cannot see that the card drawn
is worth more than the rat, or that the spell is better held for an attacker. Options:
- **Port MtgSimulator's `MultiTurnBeamSearchAiStrategy`** (MtgCore's default, `MtgGameManager.cs:84`):
  beam over the turn + greedy rollouts of future turns, choices branched. 1,455 lines; adapt to
  MagicCore's priority, stack and choices. Bring a plan first (handoff §4).
- **Cheaper first:** value a card in hand in the evaluator, so "they draw" costs; weigh a removal
  target's value against the spell's. Measure every change over 25+ games per pairing and record it
  in `docs/findings/magic-ai.md` — twice a change there measured as noise.
- Capture positions like this one to test against: MagicTable's seed + undo make them repeatable.

## MagicCore's evaluator values resources by count, not by what they provide (Shayne, 2026-10-22)

Shayne: "we are judging resources (life, cards in hand, creatures in play etc) but not properly
judging what the resources actually provide" — three lands put into a hand that already has ten in
play are worth almost nothing, yet score as three cards; loyalty "only matters as to what it can
actually provide if you spend it, otherwise it is an empty resource" — losing a 3-loyalty Jace to
draw a card, or a 1-loyalty one to surveil, is not a loss.

**Instances already met** (story AI-8): the threat term let any blocker "stop" any attacker (AI-8.4:
trample now gets past); the walker term is 2 + 0.35 a counter, nothing for what it buys (AI-8.1 —
fixed for the OPPONENT by simulating its walkers' use in the rollout; since AI-8.2 a walker
is priced by what its best ability makes (tokens, draw) with a floor of the old body, so a Jace cashed for one card still scores as a loss); the opponent's unseen hand is 2.71 a
card whatever is in it; every land in our hand is a flat 0.3, however many are in play.

**The precedent**: MtgSimulator priced a land in hand by what it unlocks — "a land is worth 0 on its
own; its value is entirely the difference it makes to everything else" (`.claude/rules/sim-ai.md`,
card values in ResolveChoice): a lot with four-drops stuck at three lands, ~nothing at six lands or
in flood. MagicCore never ported it.

**Two ways to price a resource by its use**: simulate the use (the walker fix — the rollout spends
it, the evaluator sees the result), or a cheap context check in the evaluator (MtgSimulator's land:
the castable spells it adds). Each is a measured change — a switch, the suite, a match.

**Revisit**: as a story when the next recorded blunder is one of these; the likely first ones — our
own walkers valued by use (the other half of AI-8.1), and lands in hand by what they unlock.

## MagicCore search: a lethal board at the horizon (deferred from AI-8.5, 2026-10-23)

A rollout scores a loss inside the look-ahead near −10 000 (decayed 0.95 a turn), but a certain loss
one turn PAST the horizon only as a bad board: at Shayne's turn 14 (AI-8.3, kept record
`DuelistWontTrade` #332) the block line died on turn 18 (−8145) while the no-block line, alive at 3
life on turn 19 facing a trampling 7/5, scored −17.91. MtgSimulator never had a fix for this — its
horizon fixes were the terminal discount and the absolute horizon (both ported) and the pre-search
lethal check (`docs/findings/magic-ai.md`, AI-8.3).

**The option, costed**: where a rollout stops, if the opponent's next attack kills through our
blockers (the threat term's own reading — trample counted since AI-8.4), score it as a loss at
horizon + 1, decayed like any other; "dead on 18" and "dead on 20" are then compared honestly. Cost:
a lethal check at every rollout's end (cheap — `Pressure` already assigns blockers), and a match.

**Revisit when** a recorded blunder needs it — AI-8.4 fixed the report it came from (with trample
counted the threat term reads the danger, and the search trades). Take it up as a story with a kept
record as its position.

## MagicCore AI: machine learning, later (Shayne asked, 2026-10-24)

Shayne asked whether machine learning or neural networks could help the AI. Yes — after AI-13's
lessons and a test bed (AI-11's settled positions, AI-4.2's built ones) exist; without one, a trained
model is a different set of numbers nobody can judge. Where it fits best is scoring positions:
today `Evaluator` is hand-tuned weights; a model can learn from how games turned out what a position
is worth — "value is what a card makes happen" (AI-12) without being told why.

**The options, costed** (cheapest first):
1. **Learn the evaluator's existing weights from outcomes** — thousands of AI-vs-AI games
   (`AiMatch`), every position labelled with who won, the weights fitted. Cheap; the search is
   unchanged; measured like any evaluator change.
2. **A small neural network as the position scorer** — board, hands, life in; trained in Python
   (PyTorch), run from C# (ONNX Runtime or ML.NET). Needs a description of each card for the
   network: fine for one set (FRA, 285 cards), retrained per set — MtgSimulator's trained draft
   picker learned card NAMES and never carried to a new set (`.claude/rules/sim-draft.md`).
3. **AlphaZero-style self-play**, the network also choosing which moves to search — a large project,
   and far more games than one PC plays with the search in the loop (~40 s a game with the search
   on both sides, LIM-1.8's smoke run).

Shayne's own games are the EXAM, not the training data: one game gives ~10 settled positions, a model
needs thousands of games. **Revisit when** AI-11 and AI-4.2 have positions, and AI-13 has tried its
first rules — then option 1, as a story.
