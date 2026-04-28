# Design Notes

Patterns and decisions that work for the current scope but will need revisiting as the engine grows.
This is not authoritative architecture (that lives in CLAUDE.md) — it's a watchlist.

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
