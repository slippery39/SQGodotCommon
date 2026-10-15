# Designing a card-game engine on ImmutableGameObjects — how to structure the rules, and why

**Scope:** any engine on `ImmutableGameObjects` where cards are scripted as data: effects, triggers,
targets, statics, costs, conditions. Proven on MtgCore and MagicCore. A roguelike's *game* design
is in `docs/roguelike-deckbuilder/`; this is about the *engine* under it.

**Why this file exists:** every engine here has drifted into the same shape and needed the same
repair. MagicCore was built card by card, and then needed a seven-phase refactor
(`MagicCore/FilterRefactorPlan.md`, 2026-10-10 to 10-15) to undo it, with golden replays and
card-by-card probes to prove nothing changed. The evidence is in `MagicCore/DesignAudit.md`.
**Read this before the first card script of a new engine**, when the fix is free.

**These are lessons with their evidence, not vetoes.** To break one, say what the break costs (the
evidence below says what it cost last time) and decide on purpose.

---

## Why it keeps happening

One card at a time, an enum value or a bool field is always the shortest diff:
`CreatureWithPower4OrMoreYouControlEnters` is one line, and building a filter language is a day's
work. The cost doesn't show until card fifty, when the same idea has been spelled five ways and
none of them composes. By then 300 call sites depend on it.

**The test that catches it early:** for each new card, ask whether it added a new *property* or a
new *combination*. A new property is fine: "is a token", "has a +1/+1 counter", "entered this turn".
A new combination written as code ("a token YOU control", "a creature with power 4+ that ENTERS")
means the design has failed. Leaves grow with properties, never with combinations.

---

## 1. One composable filter says "which objects", everywhere

```csharp
Target(Is.Creature & Is.Yours)
Ref.Permanents(Is.Land & Is.Yours & !Is.Source)
Subject = Is.Creature & Is.Yours & !Is.Source & !Is.Token
new CostChange(1, !Is.Creature) { By = Whose.Opponent }       // Thalia
```

A `Filter` is a record with `&`, `|` and `!`. Each leaf asks about one property of one object.
The SAME filter type serves targets, mass effects ("each creature you control"), counts, trigger
subjects, static scopes, cost changes, sacrifice and discard choices, and library searches.

**Evidence:** before the refactor, MagicCore had six separate vocabularies for "which objects", each
a closed list grown one card at a time: `RefKind` 22 values, `TargetSpec` ~20 one-card fields,
`TriggerEvent` 51 values, a static's `Affects` enum plus five `Only*` fields, one `Condition`
record per question, and bare `CardType` parameters. "Has a +1/+1 counter" alone was written three
ways. Of 157 effect types, 71 were used by exactly one card, most of them a generic effect with a
filter baked in. MtgCore had already solved this with ONE predicate language
(`TargetSpecification`), which MagicCore ignored. Shayne knew from pre-AI experience that an
enum for targeting balloons ("elves you control", "elves you don't control", "flying", "power 2 or
less"). It did.

**Context, not fields:** the filter's context carries who is asking (controller, source, event
card), so "you control", "another" and "other than that card" are leaves (`Is.Yours`, `!Is.Source`),
not a field per card.

## 2. The filter only tests; the zone and the targeting rules live outside it

`Is.Creature` means "is a creature" wherever the object is. Whoever supplies the candidates names
the zone: `TargetSpec(From, Filter)`, `Ref.All(zone, filter)`. Hexproof, shroud and "a spell can't
target itself" are TARGETING rules, so they belong in `TargetSpec`, not in the filter.

**Evidence (MtgCore's flaws, fixed on the way in):** `IsCreatureSpecification` was battlefield-only,
so in a library search it silently matched nothing (`SearchLibraryAction` carries a warning comment
about it), and zone-specific classes followed (`IsCreatureInOwnGraveyardSpecification`). Hexproof
lived inside the filter behind an `IsNonTargeted` toggle. `AndSpecification` guessed which side was
the zone to find candidates.

## 3. Filters take the object, never an id to look up again

`Matches(GameState s, GameObject o, FilterContext c)`. The object may be a **last-known snapshot**:
a "dies" trigger asks about the creature as it was on the battlefield, not the new card in the
graveyard. Likewise, **the same id is not the same object**: ids survive zone changes, so "is it
still that permanent?" compares an incarnation stamp (`Card.Incarnation`).

**Evidence:** MtgCore's filters took an id, so a dies trigger filtered the card in its NEW zone.

## 4. A trigger is an event kind + a subject filter + who did it

```csharp
Trigger(EventKind On) { Subject, By, Step, Also, LooksBack }
```

Every event record carries its **subject** (the object, as it was) and its **actor**. One generic
matcher reads them. Common triggers keep names that read like the card text (`SelfEnters`,
`YourUpkeep`), defined as instances over that one shape, not as new vocabulary. The few tests on event
data beyond the subject ("two or more", "attacks alone") are small named event predicates. Keep that
list short and look at it whenever it grows.

**Evidence:** `TriggerEvent` had 51 values over 17 real event kinds: every event crossed with
self / another / you / opponent / nontoken / power 4+. One generic matcher replaced a 51-arm switch,
with 7 residue predicates left over. MtgCore found the subject with a switch
(`EventTriggerCondition.ExtractSubjectId`) whose own comment said to move it onto the event.

## 5. Effects are data: the type says WHAT, the parameters say who and how many

An effect record's type is the verb (`DealDamage`, `Destroy`, `CreateTokens`). Who it acts on is a
`Ref` (a referent, or `All(zone, filter)`), and how many is a `Count`. **A `Count` counts a `Ref`;
it never filters again.** A one-off effect is legitimate only when the card is genuinely unique
(`WinTheGame`); `DestroyCreaturesNotOfChosenType` is a `Destroy` plus a filter.

**No delegates, ever** (root `CLAUDE.md`, Serialization Rule): every rule is a record, so the whole
`GameState` round-trips through JSON, and the AI can copy, compare and replay it. A lambda in a
card script is the same failure as an enum value: it can't be serialized, compared or composed.

## 6. Costs are one polymorphic list, used everywhere a cost appears

`AdditionalCost` (valid payments / validate / pay / describe), held as a list on activation,
casting, flashback and ward alike. "Or pay {N}" wraps a cost; it is not a second cost type.
**Cost changes** are one component: `CostChange(Count Amount, Filter? Spells) { By, If }`.

**Evidence:** MagicCore's activated ability carried 11 cost flags, and the same costs reappeared
under other names on flashback, ward, `SacrificeOrPay…` and `BeholdOrPay…`. Six cost-change components
each had a branch in `Costs.Of`. MtgCore already had both designs.

## 7. Statics: the type says what it does, a filter says whom it affects. Read live, never stamped

A static ability sits on its source and is READ by one computation (`Characteristics`: power,
toughness, keywords), so it stops the instant the source leaves. Free composition needs one
enforced guard: a P/T static whose scope reads P/T would recurse forever, so building one throws.

**Evidence:** MtgCore stamped statics onto what they affected, which made every equipment-granted
hexproof cosmetic. MagicCore had the same rule written twice for two scopes
(`AttacksDespiteDefender` and `YourCreaturesAttackDespiteDefender`).

## 8. One `Condition` hierarchy, never an enum beside it

"If you control a Jace", "unless you control two other lands", "if this is prepared": one
`Condition` type, and `Matches(Ref, Filter)` covers the questions about an object.

**Evidence:** MtgCore has two hierarchies asking the same kind of question (`ActivationCondition`,
`TriggerCondition`) and had to share code to keep them in step. MagicCore grew `StaticCondition`
and `EntersUntappedIf` enums beside its own `Condition`.

## 9. No per-card fields on shared records

A shared record (a player's turn tally, a card) holds what every card can have: counts, damage,
tapped. One card's effect is an OBJECT: an emblem or effect in a command zone, or a component on
the permanent. It is visible to the UI, it ends by a general rule, and the shared record doesn't
grow with the card pool.

**Evidence:** MagicCore's `TurnTally` held five one-card flags (`NoLegendRule`,
`NextSpellCantBeCountered`…), each invisible to the table. Phase 7 moved them into the command zone
(`ThisTurn(...)`). The six counter fields on `Card` were never fixed: by then they had ~250 uses,
so the keyed map waits for a new counter kind (`DesignNotes.md`). That is what fixing late costs.

## 10. Fail loudly at build time; never let a sentinel mean "any"

An inert card throws no error. So a filter of `null` means "any", and a sentinel like
`Is.Type(None)` THROWS. Ported mechanically, the old "None = any" would match nothing. A trigger
refuses a step filter on an event that has no step; a cost in a slot the action doesn't carry
throws. Every rule that can be checked when a card is built is checked then, not discovered in play.

---

## Starting a new engine: the checklist

1. **Grep MtgCore and MagicCore first** for every mechanic, and copy-adapt (memory "Copy from MTG").
   Reuse the DESIGN as well as the code.
2. Build the filter language (§1–3) before the second card that says "which objects".
3. Events carry subject and actor from the first event record (§4).
4. Costs as a list from the first cost beyond mana (§6).
5. For each card: did it add a property (fine) or a combination (stop and compose)?
6. Read the generated catalog (`tools/magic_docs.py` for MagicCore) after each batch: a type used by
   exactly one card is a question to answer, not a finding to ignore.

## What MagicCore kept, on purpose

Not everything diverging from MtgCore is drift (`DesignAudit.md` §9): turns, priority and the stack;
a mana solver; statics read live; one `Condition` hierarchy; keywords as strings rather than a bool
per keyword; last-known information. Diverge when the rules need it, and write down why.
