# MagicCore filter refactor — plan

**Why:** `DesignAudit.md`. MagicCore has six closed vocabularies for "which objects", each grown one
card at a time (RefKind 22, TriggerEvent 51, TargetSpec ~20 fields…). This replaces them with **one
composable filter**, lifted from MtgCore's `TargetSpecification` and repaired (audit §10).

**Decided (2026-10-10):**
- Filters read with **operators** in card scripts: `Is.Creature & Is.Yours & !Is.Source`.
- MtgCore stays untouched. MagicCore's own good divergences stay (audit §9).
- **Behaviour must not change.** Any intended change gets its own commit, made after the refactor.

**Rule for the vocabulary:** a leaf filter is a *property of one object* (its type, its controller,
its power…). Leaves grow with properties, never with combinations. If a new card needs a new leaf,
that's fine. If it needs a new *combination* written as code, the design has failed.

---

## The design

```csharp
// MagicCore/Filters/Filter.cs: records, no delegates (Serialization Rule)
public abstract record Filter
{
    /// Asked about an OBJECT, which may be a last-known snapshot, never an id looked up again.
    public abstract bool Matches(GameState s, GameObject o, FilterContext c);

    public static Filter operator &(Filter a, Filter b) => new AllOf(a, b);
    public static Filter operator |(Filter a, Filter b) => new AnyOf(a, b);
    public static Filter operator !(Filter a) => new NotFilter(a);
}

/// Who is asking: "you", "this", "the event's card".
public readonly record struct FilterContext(int Controller, int Source, int EventCard);

public static class Is   // the vocabulary card scripts read
{
    public static readonly Filter Creature = new OfType(CardType.Creature);
    public static readonly Filter Yours = new ControlledBy(Whose.You);
    public static readonly Filter Source = new IsSourceFilter();
    public static Filter PowerAtLeast(int n) => new StatAtLeast(Stat.Power, n);
    …
}
```

Repairs over MtgCore (audit §10), built in from the start:
1. **No zone in a filter.** The zone comes from whoever supplies candidates (`TargetSpec`, `Ref.All`).
   `Is.Creature` means "is a creature", wherever the object is.
2. **No targeting rules in a filter.** Hexproof and "a spell can't target itself" stay in `TargetSpec`.
3. **Filters take the object**, so a "dies" trigger can filter the card as it last existed.
4. **Candidates come from the zone.** There's no candidate-guessing in `AllOf`.

Leaves, from today's vocabulary: type, supertype, subtype, colour, keyword (through `s.Has`), whose
(cards and players), token, copy, source, event card, attached-to-source, power / toughness / mana
value bounds (through `s.Power` etc.), +1/+1 counter, entered / attacked this turn, attacking, in
combat, prepared, player.

---

## Phases

One phase per session, with this file as the hand-off. Each phase ends with the **gate** below, a
`/pre-commit`, and a commit when asked.

### Phase 0: safety net ✅ done 2026-10-10
- **Two golden replays** (`MagicCore.Tests/GoldenReplay.cs`). Each step records the move or answer
  AND how many options there were. A running hash every 100 steps locates a divergence.
  - `RandomGameTests` (normal suite): the 150 existing seeded games → `GoldenReplay.txt`.
  - `CardReplayTests` (explicit, ~90 s): every scripted card in 4 games built around it, to turn 16,
    with "yes" and multi-picks played → `GoldenCardReplay.txt`.
- **Proven:** a one-line bug in one Ref (two cards use it) failed **1** of 150 broad games but **29**
  per-card tests, both owning cards among them. The broad net alone is not enough; run both.
- An explicit fixture runs only when named ALONE in the filter (`~CardReplayTests`); with `|` NUnit skips it.
- **Not covered:** 6 cards are never cast in their games (Fblthp, Knows the Way; Ghalta the
  Immovable; Hexhaven Invigorator; Omnipresence; Return to the Light Realms; Verdant Kraken). They rely
  on their focused tests. The run prints the list ("not exercised").
- **Constraints this creates:** keep **candidate order** (players, permanents, stack, graveyard) and
  **`Describe`'s text** (phase 6 touches costs that `Describe.WhichAbility` reads). Otherwise everything
  diverges while behaving identically.

### Phase 1: the filter language (new code only, nothing uses it yet)
- `MagicCore/Filters/`: `Filter`, `AllOf` / `AnyOf` / `NotFilter`, `FilterContext`, the leaves, `Is`.
- Tests: each leaf, the operators, last-known objects. Plus a **migration table test**: every
  `RefKind`, `TargetSpec` field, `Affects` value and filter-shaped `TriggerEvent` written as a
  filter expression and checked against the old code on a test board. That table is also the
  guide for phases 2–5.
- `tools/magic_docs.py`: catalog the `Is` leaves (the generator reads enums today).

### Phase 2: TargetSpec
- `TargetSpec(From, Filter)` + `Optional`, `NotThePrevious` + targeting rules. `TargetKind` becomes
  `From` (battlefield, stack, your graveyard, any graveyard, players). The ~20 fields go.
- Its public methods keep their signatures (`IsLegal`, `Candidates`, `Combinations`, `AreLegal`), so
  the AI, MagicTable and `Describe` don't change. The 17 engine reads of `TargetKind` get updated.
- Non-targeting uses of `TargetSpec` (`MaySacrifice`, `EachSacrifices`, activated
  `Sacrifice` / `ExileFromGraveyard`) take a plain `Filter`.
- Card scripts: `Creature with { EnteredThisTurn = true }` → `Target(Is.Creature & Is.EnteredThisTurn)`.

### Phase 3: Ref and Count
- `Ref` = real referents (`You`, `EachOpponent`, `EachPlayer`, `Target(i)`, `ControllerOf`, `OwnerOf`,
  `Source`, `SourceHost`, `EventCard`, `EventObject`, `ControllerOfEventCard`) + **`All(zone, filter)`**.
  About ten `RefKind`s fold into `All`. The last-known and same-object handling stays as it is.
- Static names (`Ref.CreaturesYouControl`…) stay as one-liners over `All`, so most of the 383 card
  call sites don't change. `Subtype`, `ExceptEventCard` and `YoursOfSubtype` go.
- `Count` follows automatically: its aggregates already take a `Ref`. **Left alone:** the tally kinds
  (`CardsYouDrewThisTurn`…), which grow one per tracked stat, not per combination.
- **Open (decide in the phase):** "each creature *target player* controls" needs a filter that reads
  a Ref. Either `FilterContext` carries the targets, or `All` takes a whose-Ref. Pick whichever
  reads simpler.

### Phase 4: statics and conditions
- `StaticEffectComponent(Filter Affects, …)`: the `Affects` enum and the five `Only*` fields go.
  Characteristics stays a live read.
- **A guard composition makes necessary:** a P/T static whose filter reads power or toughness would
  recurse forever. Today a comment prevents it. With free composition it must be enforced: a filter
  reports whether it reads P/T, and building such a P/T static throws.
- Statics whose *name* bakes in a scope fold into one component with a filter, e.g.
  `AttacksDespiteDefender` + `YourCreaturesAttackDespiteDefender`. A one-off without a scope in its
  name stays, since there's no second use to justify it.
- `Condition`: one `Matches(Ref, Filter)` replaces `TargetIs`, `TargetIsNotAttacking`,
  `TargetManaValueAtMost`, `EventCardHasSubtype`, `SourceIsToken`, `SourceIsPrepared`,
  `SourceHasDealtCombatDamage`. The `StaticCondition` and `EntersUntappedIf` enums become `Condition`s.

### Phase 5: triggers
- Events get a common base carrying their **subject** (the object, as it was) and the **player**
  who did it. That repairs audit §10.5.
- `TriggeredAbilityComponent(EventKind On, …) { Filter? Subject; Whose? By; }`. `EventKind` is one
  value per event record (17, which grows only with new kinds of event). `EventSubtype` goes, and
  so does the use of `If` for subject tests.
- **Expect residue:** a few triggers test event data beyond the subject (`RemovingTwoOrMore`,
  `AttacksPlayerAlone`, "a spell that targets your creature", "first noncreature spell each turn").
  Each becomes a small event predicate. List them in the phase and keep them few.
- Common triggers keep names that read like the text (`SelfEnters`, `YourUpkeep`) as helpers.

### Phase 6: costs (the riskiest; design at phase start)
- Lift MtgCore's `AdditionalCost` (valid payments / validate / pay). One list of costs serves
  activation, casting (behold-or-pay, sacrifice-or-pay), flashback and ward. The 11 flags on
  `ActivatedAbilityComponent` go.
- **Read first:** how cast and activate enumerate cost choices into legal actions today ("chosen like
  a sacrifice, one legal action each"). The AI and MagicTable depend on that.
- Cost changes: one `CostChange(Count Amount, Filter AppliesTo, Whose spells, Condition? If)`. The
  six components and their branches in `Costs.Of` go. Warrior's Blades falls out as
  `Count.CountersOn(Ref.Target(0))`.

### Phase 7: separate decision, not part of this plan
`TurnTally` per-card effect flags → command-zone effects; counter fields → a map. Different problem
(per-card state, not filters). Decide after phase 6.

---

## The gate (every phase)

- Both golden replays identical: `RandomGameTests` (in the suite) and `CardReplayTests` (explicit).
  All tests green, and counts never go down (MagicCore.Tests 762 + 277 explicit, SQGodotCommon.Tests
  137 at the start).
- `tools/bench_vs_head.sh` for phases 2–5: they sit under the AI (`Candidates`, `Resolve`,
  `Characteristics`).
- `python tools/magic_docs.py`. The `FraCardSheet.md` diff shows the scripts changed shape and nothing else.
- Docs updated in the same step: `Cookbook.md`, `.claude/rules/magic-mechanics.md` (e.g. the
  `EventSubtype` rule), the `MagicCore/CLAUDE.md` source map.

## Risks

| Risk | Guard |
|---|---|
| Candidate order changes and random play diverges | order kept; golden replay shows it at once |
| Slower AI from per-object virtual calls | bench each hot phase; `Characteristics` keeps its per-state cache |
| P/T static recursion through a filter | enforced guard (phase 4) |
| Filter ↔ Ref circularity | decided explicitly in phase 3 |
| Context loss across sessions | this file is the hand-off; tick phases off below |

## Status

- [x] Phase 0: golden replay (2026-10-10)
- [ ] Phase 1: filter language
- [ ] Phase 2: TargetSpec
- [ ] Phase 3: Ref, Count
- [ ] Phase 4: statics, conditions
- [ ] Phase 5: triggers
- [ ] Phase 6: costs
