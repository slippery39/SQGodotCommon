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

**Proposed fix:** Action-level deduplication inside the simulator AI (not in `MtgActionGenerator`,
which stays authoritative). After `GetLegalActions` returns, collapse actions that operate on cards
sharing an identical fingerprint: `Name + ManaCost + Components + current Damage + active modifiers`.
Two cards with the same fingerprint are interchangeable for search purposes — only evaluate one
representative per group. Cards that diverge (one takes damage, one gets a buff) will naturally
have different fingerprints and stay separate.

**Edge cases to handle:** Partial activation (`HasActivated`), `UntilEndOfTurn` modifiers,
summoning sickness flag — all must be part of the fingerprint.

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
