# MagicCore design audit vs MtgCore — 2026-10-10

Every MagicCore subsystem against its MtgCore equivalent: where a design was **reinvented** instead of
reused, where MagicCore **diverged for a good reason**, and what MtgCore's design gets **wrong** and
must be fixed on the way in. Findings only. Nothing has been changed.

## The headline

Most of what's wrong comes down to one thing. **MagicCore has no shared predicate language.** It says
"which objects" in six different ways, each a closed list grown one card at a time. MtgCore had
one: `TargetSpecification` (And/Or/Not), used for targeting, mass effects, trigger filters, static
filters, cost-reduction filters and library searches.

A rough measure: of the 157 effect/ability record types in `MagicCore/Effects/`, **71 are referenced
by exactly one card script** (and 19 by none, mostly engine-internal steps). Some one-offs are
legitimate (Magic has genuinely unique cards), but most are a generic thing plus a baked-in filter.

---

## 1. Reinvented — the filter, six times over  ★ root cause

| Where | How it says "which objects" | Example of the balloon |
|---|---|---|
| `RefKind` (Refs.cs) | 22 enum values + `Subtype`, `ExceptEventCard` | `YourPermanentsWithCounters`, `AllPermanentsExceptYourTokens`, `YourOtherPermanents` |
| `TargetSpec` | 9 `TargetKind`s + ~20 one-card fields | `EnteredThisTurn`, `AttackedThisTurn`, `WithPlusOneCounter`; `OrSupertype` ("widens, never narrows") because there's no Or |
| `TriggerEvent` | the filter is part of the trigger's NAME | `CreatureWithPower4OrMoreYouControlEnters` |
| `StaticEffectComponent` | `Affects` enum (5) + `OnlyWithCounter`, `OnlySubtype`, `OnlyTokens`, `OnlyTypes`, `OnlyPowerOrToughnessAtMost` | |
| `Condition` records | one record per question about a Ref | `TargetIs`, `TargetManaValueAtMost`, `TargetIsNotAttacking`, `EventCardHasSubtype`, `SourceIsToken`, `SourceIsPrepared` |
| Effect parameters | a bare `CardType` where a filter belongs | `SearchLibrary(CardType)`, `MillAndMayTake(Types)`, `YouChooseTheyDiscard(Types)`, `RevealUntilOntoBattlefield`, `SacrificeGreatestManaValue` |

"Has a +1/+1 counter" alone is written three ways: `RefKind.YourPermanentsWithCounters`,
`TargetSpec.WithPlusOneCounter`, `StaticEffectComponent.OnlyWithCounter`. And `MaySacrifice` /
`EachSacrifices` already use `TargetSpec` as a non-targeting filter, so the code is already asking
for a shared filter.

**MtgCore's version:** `TargetSpecification` + `TargetingContext` (MtgCore/Targeting/). Used by
`TargetingStrategy`, `EventTriggerCondition.Filter`, `StaticAbilityComponent.Filter`,
`ConditionalCostReductionComponent.AppliesTo`, `SearchLibraryAction.Filter`.

## 2. Reinvented — triggers

`TriggerEvent` has 51 values. Underneath there are only 17 event records (Triggers.cs), which are fine.
The 51 come from crossing those events with who/what filters: Self / Another / You control /
Opponent controls / nontoken / power ≥ 4 / mana value ≤ 2. On top of that, `EventSubtype` and the
`If` field are bolted onto `TriggeredAbilityComponent`.

**MtgCore's version:** `EventTriggerCondition` = event type + `TargetSpecification` filter on the
event's subject, plus `AndTriggerCondition` for an intervening "if". One class instead of a value per
card.

## 3. Reinvented — costs

`ActivatedAbilityComponent` carries **11 cost fields**: `TapCost`, `SacrificeSelf`, `Sacrifice`,
`ExileSelf`, `DiscardSelf`, `DiscardACard`, `TapUntapped` + `TapUntappedType`, `DiscardMustBe`,
`ExileFromGraveyard`, `CostsLessPerCounterOnTarget`. The same costs reappear elsewhere under other
names:
- `FlashbackComponent.DiscardACard`
- `CounterUnlessPays.DiscardACard` / `.SacrificePermanents` (ward costs)
- `SacrificeOrPayAdditionalCost`
- `BeholdOrPayAdditionalCost`

**MtgCore's version:** `AdditionalCost` (MtgCore/Costs/), a polymorphic record with
`GetValidPayments` / `Validate` / `Pay` / `Describe` and a list of them on the ability. One cost type
would serve activation, casting, flashback and ward alike.

## 4. Reinvented — cost changes

Six components, each with its own branch in `Costs.Of`: `OpponentsNoncreatureSpellsCostMore`,
`YourNoncreatureSpellsCostLess`, `YourSpellsOfTypeCostLess`,
`CostsLessIfYouControlLegendaryCreature`, `CostsLessIfYouCastNoncreatureSpellThisTurn`,
`CostsLessByGreatestStatYouControl`.

**MtgCore's version:** one `ConditionalCostReductionComponent(Amount, Condition, AppliesTo)`. With
MagicCore's `Count` as the amount (Ghalta's "greatest power") and a whose-spells field for taxes, all
six become data.

## 5. Reinvented — static abilities

Covered by §1 for P/T and keywords. There is also a separate component per rule *per scope*:
`AttacksDespiteDefender(Condition)` (self) and `YourCreaturesAttackDespiteDefender` (yours) are the
same rule written twice. The same goes for `YourCreaturesAssignDamageByToughness`,
`YourInstantsAndSorceriesHaveSplitSecond`, `OneAttackerPerPlaneswalkerYouControl`,
`YourPlaneswalkersSurviveZeroLoyalty`.

**MtgCore's version:** `StaticAbilityComponent` with `Filter` and `ActiveInZone`. What the static
*does* is the subclass; *who it affects* is the filter.

## 6. Duplicated inside MagicCore — conditions as enums

`Condition` itself is good (§9). But two enums sit beside it and repeat its job:
`StaticCondition` (one real value: `YouControlAJacePlaneswalker`) and `EntersUntappedIf` (3 values,
switched in `MagicState.EnterBattlefield`). Both should simply be a `Condition`.

## 7. Per-card fields on shared records

- **`TurnTally`** mixes real counts (`SpellsCast`, `CardsDrawn`, `LifeGained`) with one card's effect
  stored as a field: `JaceLoyaltyAtInstantSpeed`, `NextSpellCantBeCountered`,
  `ExtraRedFromMountains`, `CopyNextInstantOrSorcery`, `NoLegendRule`. MagicCore already has a
  general way to do this, an effect object in the command zone (`GetEmblem.ToCommandZone`, used by
  `UntilYourNextTurnWhenever`).
- **Counters on `Card`** are six separate fields (`PlusOneCounters`, `MinusOneCounters`,
  `StunCounters`, `ChargeCounters`, `HasFinalityCounter`, `Loyalty`). A new kind means editing
  `Reset`, `RemoveCounters` and `Proliferate`, which already hand-list them. MtgCore doesn't solve this
  either (one component per kind), so there's nothing to lift, only a keyed map to consider. Lower
  priority.

## 8. One-off effects that are a generic effect plus a filter

`DestroyCreaturesNotOfChosenType`, `ReturnChosenToHandIfManaValueAtMost`,
`SacrificeGreatestManaValue`, `ExileOneOfTypeFromGraveyard`, `MillAndMayTake`. These collapse once §1
exists. Truly unique ones (`WinTheGame`, the Emrakul cast-copies chain) are fine as they are.

---

## 9. Diverged for a good reason — keep

| MagicCore | Why it's right |
|---|---|
| Turns, priority, stack, blocking, combat | MtgCore deliberately isn't Magic here |
| `ManaPayment` solver, colour units | MtgCore's two-track mana needs no solver |
| `Characteristics`: statics read live, not stamped | MtgCore's stamping (`StaticAbilityEngine`) made every equipment-granted hexproof cosmetic (see the comment in `IsCreatureSpecification`) |
| One `Condition` hierarchy | MtgCore has two that ask the same kind of question (`ActivationCondition`, `TriggerCondition`) and had to share `LandCounts` to keep them in step |
| Keywords as strings | MtgCore's `StaticGrantKeywordAbility` is a bool per keyword, its own balloon |
| `Ref`'s last-known info, `Incarnation`, `SourceSnapshot` | real CR 400.7 / 608.2h work; MtgCore has no stack to need it |
| Hexproof checked in `TargetSpec`, not in the filter | correct: it's a targeting rule, not a property of the object |
| Choices on ImmutableGameObjects' `ChoiceAction` | already shared |
| `Replacements`, flashback, mill, search | lifted from MtgCore and credited |

## 10. What MtgCore gets wrong — fix on the way in

Lifting `TargetSpecification` unchanged would bring its flaws along:

1. **Zone is baked into type predicates.** `IsCreatureSpecification` is battlefield-only, so in a
   library it silently matches nothing (`SearchLibraryAction` carries a warning comment about exactly
   this). There are also zone-specific classes (`IsCreatureInOwnGraveyardSpecification`). → The filter
   asks only about the object. The zone belongs to the Ref or TargetSpec that supplies candidates.
2. **Targeting rules inside a filter.** Hexproof, shroud and protection live in
   `IsCreatureSpecification`, toggled by `IsNonTargeted`. → Keep them in `TargetSpec`, as MagicCore
   does now.
3. **Filters take an id and look the object up**, so a "dies" trigger filters the card in its *new*
   zone. → Filter a `Card` (possibly a last-known snapshot) against the state, so LKI works.
4. **Candidates come from a heuristic** (`AndSpecification` guessing which side is a
   `ZoneSpecification`). → Candidates come from the zone (point 1), and the filter only tests.
5. **The event's subject is found by a switch** (`EventTriggerCondition.ExtractSubjectId`; its own
   comment says to move it onto the event). → MagicCore's events should carry their subject.
6. **`TargetSelectionMode.Best`** was a workaround for having one target slot. MagicCore has real
   multi-target, so it's not needed.

The filter's context should carry controller, source and event card. That covers "you control",
"another" and "other than the event's card" without a field for each.

## Proposed order (once the review is done)

Each step stays green on the existing tests, and the regenerated `FraCardSheet.md` diff shows what moved.

1. The filter language (object predicates + And/Or/Not + context), with its own tests.
2. `TargetSpec` → zone + filter + targeting rules.
3. `Ref` → real referents + `All(zone, filter)`; `Count` follows for free.
4. `StaticEffectComponent` and the filter-shaped `Condition`s → filter. Fold `StaticCondition` and
   `EntersUntappedIf` into `Condition`.
5. Triggers → event + subject filter + whose.
6. Costs → `AdditionalCost`; cost changes → one reduction component.
7. `TurnTally` effect flags → command-zone effects; counters → maybe a map.

Steps 1–3 are the core. 4–6 are mostly mechanical once the filter exists. 7 is separate.
