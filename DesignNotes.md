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
