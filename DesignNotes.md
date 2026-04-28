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
