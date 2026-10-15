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

### Phase 1: the filter language ✅ done 2026-10-11 (new code only, nothing uses it yet)
- `MagicCore/Filters/`: `Filter`, `AllOf` / `AnyOf` / `NotFilter`, `FilterContext`, the leaves, `Is`.
- Tests: each leaf, the operators, last-known objects. Plus a **migration table test**: every
  `RefKind`, `TargetSpec` field, `Affects` value and filter-shaped `TriggerEvent` written as a
  filter expression and checked against the old code on a test board. That table is also the
  guide for phases 2–5.
- `tools/magic_docs.py`: catalog the `Is` leaves (the generator reads enums today).
- **Built:** `MagicCore/Filters/Filter.cs` (base, `AllOf`/`AnyOf`/`NotFilter`, `FilterContext`, leaves:
  `OfType`, `OfSupertype`, `OfSubtype`, `OfColor`, `HasKeyword`, `OfController`, `StatBetween`, and
  `HasProperty(Property)` for the yes/no ones) and `Filters/Is.cs`. Type and supertype leaves take flags
  and match ANY of them (`card.Is`). `Is.InCombat` is `Attacking | Blocking`, not a leaf.
- **The migration table is `MagicCore.Tests/Filters/FilterMigrationTests.cs`** on `FilterBoard` (one of
  everything, every zone). Each row checks the old code against zone + filter, SAME ORDER; a
  completeness test per vocabulary fails when a value is in neither the rows nor a "not a filter" list
  (Ref referents, TargetSpec's `Optional`/`NotThePrevious`, what a static does, and the trigger lists
  `EventAndWho`, `Residue` (8), `ReadFromTheCard`). Old private code is reached by reflection
  (`Triggers.Matches`/`OfSubtype`, `Characteristics.AppliesTo`); a phase that deletes one moves its rows.
  Proven: one mutated leaf failed 4 rows across Ref, TargetSpec, statics and the leaf test.
- **Found while writing the rows — decide in the phase named:**
  - Order differs by vocabulary: a `Ref` lists YOUR objects first, `TargetSpec` player 1's. The board's
    "you" is player 2, so the rows pin both (phases 2, 3).
  - `SpellOrCreature` is two clauses, `(Battlefield, Is.Creature)` + `(Stack, any)`: "spell" is a zone,
    not a property. So `From` takes several zones each with its own filter, or this stays a special case (phase 2).
  - Mana value: `TargetSpec` reads `ManaCost.ManaValue` in a graveyard and `ManaValueNow` elsewhere;
    `Is.ManaValue…` reads `ManaValueNow` everywhere, equal off the stack (X is 0 there).
  - "Each creature target player controls" is written as a zone from a Ref (`TargetPlayersBattlefield`),
    the simpler answer to phase 3's open question — FilterContext doesn't carry targets.
  - A `Ref`'s or trigger's "any card" has no filter (null), not a match-all leaf. `Is.Type(None)`
    THROWS: the old "None = any" ported mechanically would match nothing — an inert card.
  - `Is.Source` / `Is.EventCard` compare ids, as the old code does; `Ref.Source` also checks
    Incarnation. A flickered source in a filter ("each OTHER creature") — decide in phase 3.
  - `Is.AttachedToSource` looks the source up live (as `Affects.AttachedCreature` does). The LKI
    host stays `Ref.SourceHost`. `Is.PowerAtMost` matches noncreatures (power 0), as the old code did.
- **Test trap:** NUnit's `Is` collides with `MagicCore.Filters.Is`. A test file using filters aliases
  both: `using Is = MagicCore.Filters.Is; using Nu = NUnit.Framework.Is;`. Card test files will need
  this from phase 2 on.

### Phase 2: TargetSpec ✅ done 2026-10-11
- `TargetSpec(From, Filter)` + `Optional`, `NotThePrevious` + targeting rules. `TargetKind` becomes
  `From` (battlefield, stack, your graveyard, any graveyard, players). The ~20 fields go.
- Its public methods keep their signatures (`IsLegal`, `Candidates`, `Combinations`, `AreLegal`), so
  the AI, MagicTable and `Describe` don't change. The 17 engine reads of `TargetKind` get updated.
- Non-targeting uses of `TargetSpec` (`MaySacrifice`, `EachSacrifices`, activated
  `Sacrifice` / `ExileFromGraveyard`) take a plain `Filter`.
- Card scripts: `Creature with { EnteredThisTurn = true }` → `Target(Is.Creature & Is.EnteredThisTurn)`.
- **Built:** `TargetSpec(From, Filter?)` with `Optional`, `NotThePrevious` and **`Or`** (another spec,
  so it stays value-equal): the two-zone kinds are `Spell() with { Or = Creature }` and
  `AnyTarget(f)` = players + creature/planeswalker, each narrowed by `f`. Shortcuts on `TargetSpec`
  (`Creature`, `Permanent(f)`, `Player(f)`, `Spell(f)`, `InYourGraveyard(f)`, `AnyTarget(f)`…);
  FraCards' `Target(f)` is `Permanent(f)`. The targeting rules: hexproof (battlefield only), and off
  the battlefield never the source's own card — the old graveyard and stack branches both did that.
- **Non-targeting costs are a bare `Filter` over the chooser's OWN zone** (`filter.Among(s, player,
  zone, source)`, in `FilterChoices`): activated `Sacrifice`/`ExileFromGraveyard`, `SacrificeOrPayAdditionalCost`,
  `MaySacrifice`, `EachSacrifices`, `ChoosePermanentChoice` (null filter = any permanent). Scripts
  drop `Is.Yours` there. Gallia's "exile ANOTHER creature card" is `Is.Creature & !Is.Source` — the
  old graveyard spec excluded the source implicitly.
- 112 script specs converted by a script (the scratchpad `convert_specs.py`, not kept); the 12 cost
  sites by hand. Golden replays identical (150 games, 277 card games); bench −1.7% (noise).
- **Conversion verified card by card:** a throwaway probe walked every scripted card's components,
  dumped each target spec's and cost filter's candidates on `FilterBoard` in 3 contexts (you/your
  source, you/a graveyard source, the opponent), on HEAD and on the working tree: 483 lines, identical.
  Do the same in phases 3–5: it covers what the golden replays never cast.
- The migration table's TargetSpec rows now check the NEW `TargetSpec` against the test's own zone
  walk + filter (phase 1 proved the old one equal to that); the test's zone enum is `Zone` now.
- Found: `DecliningMaySacrifice_IgnoresAStaleEarlierPick` ran with controller 0 (the pipeline
  replaces a step's InputContext), so the Bear was never even offered; the old candidate scan hid it.
  It now passes the context on the pipeline and asserts the Bear is offered; revert-checked.
- `magic_docs.py`: catalogs the `TargetSpec` shortcuts; a member access (`Ref.Target`) no longer
  counts as a use of a script helper (`Target`); `&amp;` in a summary prints as `&`.

### Phase 3: Ref and Count ✅ done 2026-10-15
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
- **Built:** `RefKind` is the eleven referents + `All`. `Ref.All(zone, filter?, of?)`: every
  player's zone, yours first (the old order), filtered; **`Of` is a Ref naming whose zone** — the
  open question's answer: Twisted Fates is `Of = Target(i)`, "your graveyard / hand" is `Of = You`.
  "You control" on the battlefield stays a filter (`Is.Yours`), as in targets. `Ref.Permanents(f)`
  is `All(Battlefield, f)`. `Type`, `Subtype`, `ExceptEventCard` and FraCards' `YoursOfSubtype` are
  gone; the static names are one-liners. 8 script sites converted by hand.
- **Decided: `Is.Source`/`Is.EventCard` keep comparing ids** (phase 1's open note). The old
  `YourOtherPermanents` and `ExceptEventCard` compared ids too, so that is the unchanged behaviour;
  `Ref.Source` itself keeps its Incarnation check. Revisit only with a card that flickers its source
  mid-resolution and counts "other" creatures.
- Verified card by card (as in phase 2): every Ref and Count in every scripted card, plus all of
  Ref's and Count's static names, resolved on `FilterBoard` in 3 contexts on HEAD and on the working
  tree — 19,263 lines, identical. The board got Forest/Island/Angel subtypes for that run so Roiling
  Canopy's and Jace's Refs weren't empty on both sides. Golden replays identical; bench −0.9%.
- Review fixes: `Ref.All` without a `Zone` throws (ZoneType's default is Hand, so it would have
  meant "hands"); an unknown `RefKind` throws instead of resolving to nothing; Garruk, Curse Breaker
  keeps the old type-free `Is.Yours & Is.AttackingAPlayer` (adding `Is.Creature` was a behaviour
  change for an attacker that stops being a creature). Left: "you control" scans both battlefields
  and filters (bench shows no cost; `of: You` is the fix if it ever shows).

### Phase 4: statics and conditions ✅ done 2026-10-15
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
- **Built:** `StaticEffectComponent(Filter Affects, Keyword)`; `While` folded into `If`
  (FraCards' `YouControlAJace`). **The guard:** no static's scope may read keywords, and a P/T static's
  scope may not read power or toughness (`Filter.ReadsKeywords` / `ReadsPowerOrToughness`); it throws
  as the component is built, `with` included — explicit backing fields, since MagicCore is net9 (no
  `field`). The read flags are abstract METHODS: every leaf must say (a default would fail open),
  and they stay out of a filter's ToString. A keyword static MAY read P/T (Tetsuko): the P/T pass never asks about keywords. Ceiling,
  in DesignNotes: the static's `Count` and `If` aren't checked.
- "Can attack as though it didn't have defender" is a keyword-like grant now (review), like
  `CantBeBlocked`: `StaticEffectComponent(scope, Combat.AttacksDespiteDefender)`, and `CanAttack`
  asks `s.Has` — both defender components are gone, emblems and the per-state board come free.
- `Matches(Ref, Filter)`: any object the Ref names matches. It reads `Ref.ObjectsAsTheyWere` — the
  ONE "this, as it last was, once it's gone" rule, now shared with `Count` (it was inline there).
  New leaf `Is.DealtCombatDamage` (Ruric Thar). `EntersTappedComponent(Condition? Unless)`: dual lands'
  `YouControlAPlaneswalker`, `YouControlTwoOrMoreOtherLands`, and `YouCanBehold("Jace")`.
- Verified card by card (probe, HEAD vs working tree): every Condition in 4 contexts, every static
  through `AppliesTo` from 3 sources, every enters-tapped for 3 lands — 307 lines, identical but 3:
  a prepare creature's "if this creature isn't prepared" when the source is GONE by resolution. Old
  read whatever now has the id (a fresh graveyard card: not prepared), new reads it as it was. The
  effect, `BecomePrepared(Ref.Source)`, finds no object either way, so the outcome is the same; the new
  reading is the rules' (last-known information). Defender: revert-checked — both card tests fail
  without it. Golden replays identical; bench −0.7%.
- The table's static rows now check `AppliesTo` against the filter; three tests pin the guard.

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
  All tests green, and counts never go down (MagicCore.Tests 882 + 277 explicit, SQGodotCommon.Tests
  150 after phase 1).
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
- [x] Phase 1: filter language (2026-10-11)
- [x] Phase 2: TargetSpec (2026-10-11)
- [x] Phase 3: Ref, Count (2026-10-15)
- [x] Phase 4: statics, conditions (2026-10-15)
- [ ] Phase 5: triggers
- [ ] Phase 6: costs
