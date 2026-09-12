---
paths:
  - "MtgCore/Cards/Builders/*.cs"
  - "MtgCore/Sets/*.cs"
  - "MtgCore/Sets/*/*.cs"
  - "MtgCore/Cards/*.cs"
  - "MtgCore.Tests/*Card*.cs"
  - "MtgCore.Tests/*Set*.cs"
---

# MtgCore — card building and set mechanics

The card builder API, what each Core Set Cube colour section added, and the creation cookbook.
The step-by-step procedure for adding a card is the `add-card` skill; this is the reference it
cites.

## Colourless Section Mechanics (Core Set Cube)

The colour sections were creature-and-spell work. This one is artifacts, equipment and counters,
and it needed four primitives plus a handful of small additions. **Two latent engine bugs surfaced
while building it, both silent, both caught by a test that asserted the consequence.**

### Animation — `AnimateAction`

The only way a `CreatureComponent` reaches a permanent already in play. It was previously created
in exactly two places (`CreatureCardBuilder.Build`, `PutIntoBattlefieldAction`), and
`AddModifierAction`, `AddCustomModifierAction` and `AddCountersAction` all silently `continue` on a
non-creature. `BecomesBaseCreatureComponent` does not help — it is a `PowerToughnessModifier` that
reshapes an EXISTING creature, and on a bodyless card its bonus is read by nobody.

Three things it must get right, each silent when wrong:

- **No summoning sickness.** An animated permanent has been under your control since the turn began
  in the overwhelmingly common case, and the other default makes every animation a do-nothing on
  the turn you paid for it. Limiters belong on the card (Haunted Plate Mail's "only if you control
  no creatures").
- **`StaticAbilityEngine.ProcessPermanentEntered` is called DIRECTLY, with no event staged.** The
  engine is a push model driven by `CreatureEnteredBattlefieldEvent`, so an anthem otherwise misses
  the new creature entirely — but staging that event would fire every ETB payoff on the board
  (Corpse Knight, Soul Warden) for a permanent that entered nothing.
- **Reverting happens in `EndTurnAction.RevertAnimatedPermanents`, for BOTH players.** A
  `CreatureComponent` is not a modifier, so `ClearEndOfTurnModifiers` will not touch it, and
  `StartTurnAction`'s per-component loop runs for the active player only — cleanup there would let
  the animation survive the opponent's entire turn. Same reasoning as the impulse-draw and
  prevention cleanups already living in `EndTurnAction`. The revert also strips the applied anthem
  components and calls `ProcessPermanentLeft`, because `StampEffect` unconditionally `Add`s: a
  permanent animated on three turns under a Glorious Anthem would otherwise accumulate three
  boosts, the same accounting failure that once made a bounced creature collect a second one.

`AddCountersAction`'s non-creature guard came out in the same change. It was harmless while nothing
read P/T off a non-creature, and wrong the moment animation shipped.

### Charge counters — the resource kind

`ChargeCounterComponent { Kind, Count }` is a **sibling** of `PlusOneCounterComponent` and
deliberately NOT a `PowerToughnessModifier`. `CreatureEvaluator` walks every one of those, so
inheriting would turn a gold counter into +1/+1 and let a counter-doubler double it.
`AddChargeCountersAction` is likewise separate from `AddCountersAction`: charge counters are not
subject to the `CountersPlaced` replacement (Conclave Mentor says "+1/+1 counters") and emit no
`CountersAddedEvent`, so a counters-matter payoff cannot fire on one. Unlike `AddCountersAction`
they may go on a non-creature, which is the entire point.

`RemoveCounterAdditionalCost` is the removal cost green deferred with "build it when a second card
wants to spend counters". **It is NOT a selection cost** — a counter is fungible and "which of your
three identical gold counters" is not a decision — so it sits with `LifeAdditionalCost`, validated
against state and paid with no prompt, and neither `MtgActionGenerator` nor the Godot cost walker
needed a change.

**BUG FOUND: `ActivateAbilityAction.Execute` read the source card BEFORE paying costs**, then
rebuilt its component array from that stale snapshot to bump `ActivationCount` — writing the
pre-payment array straight back over the payment. Any cost that mutates the source card's own
components validated, appeared to be paid, and silently was not; Dragon's Hoard would have drawn
forever off one counter. Latent because every existing cost touches something else: sacrifice and
discard move OTHER cards, life changes the player. The card is now re-read after payment.

### Playing off the top of your library

`PlayFromLibraryTopComponent { Types }` widens `GameState.IsInCastableZone`, which is the single
"can you play this from where it is" predicate all four play actions consult — so `PlayLandAction`
and the three cast actions all started working off the top with no edit to any of them. Same shape
as impulse draw. Scope is the top card only, the controller's own library only.

**BUG FOUND, and it is the one this feature was always going to risk:** the generator half was
sequenced AFTER `AddHandActions`' early return for an empty `PlayableExiledIds` — which is nearly
always — so `IsInCastableZone` said yes while `MtgActionGenerator` never offered the action. The
generator and the predicate disagreeing is exactly the "the AI can do it and I can't" class, and
the test that caught it asserts both halves together. Keep asserting both.

Godot renders the playable top card **as a hand card** (`MtgGameScene.SyncHand`) rather than as a
new board slot: the hand is already the "cards you can play" surface, with drawing, details, click
routing and highlight tinting, and the engine treats the card identically. It is labelled
"(Top of your library)" in its rules text, because an unlabelled card that is not in your hand
sitting among cards that are is worse than not showing it at all.

### Smaller additions

- **`IsEquippedBySourceSpecification`** — "equipped creature", read off `TargetingContext.SourceCardId`,
  which already existed. Without it an attachment cannot address its own wearer from its own
  trigger, and the five Rings collapse into near-identical vanilla equipment.
- **`GraveyardCountComponent.Types` / `.AffectsToughness`** — two fields, not a second type.
  `AffectsToughness = false` is the `*/N` templating (Enigma Drake). `Card.EffectiveTypes` reports
  `Instant|Sorcery` for an undeclared spell, which is exactly the union these cards count.
- **`ReplaceableEvent.CountersPlaced` + `CounterBonusComponent`** — Conclave Mentor. Applied to the
  net INCREASE rather than to `Amount`, so it also modifies a doubling (which really is "put that
  many more counters on it") and cannot bump a removal.
- **`ControlsCardNamedCondition`** — the Empires trio. Name-matching was missing, not structurally
  absent, so it gets built.
- **`TargetBuilder.NonlandPermanents()`** — every other mass helper was creature-shaped, so no
  sweeper could name an artifact, enchantment or planeswalker. `Land` is masked out for honesty;
  a land is never a battlefield permanent here.
- **`PermanentCardBuilder.WithStaticGrantKeyword`** — the machinery already worked from a
  non-creature source; only the builder method was missing.
- **`GainPermanentManaAction.Deferred`** — "put it onto the battlefield tapped". Mirrors
  `BonusManaLandComponent.Deferred`.
- **`TriggerConditions.OnYourEndStep()`** — filtered, for the same reason `OnYourUpkeep` is. Note
  `LifeGainedThisTurnCondition` deliberately does NOT filter: Resplendent Angel is printed "each
  end step". Check the card before assuming either is a bug.
- **`CannotLoseComponent`** — Platinum Angel, checked in `CheckLossConditions`. Suppresses the
  outcome, not the cause, so killing the Angel collects the waiting loss.
- **`ThresholdSource.ControlledEnchantments`** — Blood-Cursed Knight, and the card that showed the
  "conditional static abilities" deferral is narrower than it reads. That deferral is about a
  conditional TEAM anthem, which `StaticAbilityEngine`'s push model cannot keep current; a
  condition on a SINGLE creature buffing itself is a live-evaluated modifier, which is exactly what
  `ThresholdComponent` already is. One enum value and one case in `IsActive`.
- **`HasManaCostAtLeastSpecification`** — a real type rather than `Not(HasManaCostAtMost)`. The
  negation is the same arithmetic, but it inverts to true for a non-card AND `MtgCardMapper` cannot
  describe it, so the whole restriction vanished from Dragon's Hoard's face and the card read as
  triggering on every creature. **A spec that cannot print itself will be dropped from a card.**

### Two constraints the multicolour section pinned down

**`SpellCardBuilder.WithTarget` binds only the PENDING effect, not every effect built so far.**
Chaining two effects and putting one `WithTarget` at the end silently leaves the first on its
default strategy. On Heroic Reinforcements that made the +1/+1 a single-target buff while only the
haste went team-wide — a card that looks entirely correct until you count what got buffed. Repeat
`WithTarget` after each effect.

Its sibling trap: **`WithSelfBuff` is `Permanent` duration.** Used for an "until end of turn" pump
it stacks every activation into an unbounded creature. Write the `AddModifierAction` out with
`TargetContextKey = SourceCardId` when the duration matters.

**`ResolveEffectAction` resolves EVERY effect's targets before any of them executes.** So an
`AllValid` list cannot include a token an earlier effect on the same card just made — reordering
does not help, since the tokens are absent either way. Heroic Reinforcements' Soldiers therefore
carry haste natively instead of receiving it. `CoresetCubeMulticolourTests` asserts the token is a
1/1, so if targeting ever becomes lazy the test says so rather than quietly passing.

### Mana producers produce on your upkeep, rocks included

Extending the green dork rule to artifacts: Gilded Lotus, Meteorite, Dragon's Hoard and Scuttlemutt
all use `OnYourUpkeep()` → `AddTemporaryManaAction`, never a tap ability. An unactivated mana
ability is invisible — the AI must re-derive the activation every turn on every producer before it
can cast anything. Price them slightly above their printed rate for the reliability.

## Red Section Mechanics (Core Set Cube)

- **Damage to planeswalkers did not work at all.** See "Planeswalkers" — two independent holes,
  both silent, both predating red.
- **Divided damage is sprayed, not split.** "Deals N damage divided as you choose among any number
  of targets" (Cone of Flame, Flames of the Firebrand, Chandra's Outrage, Thundermaw Hellkite,
  Inferno Titan, Drakuseth) has no home here: targeting is single-target or all-valid, and there is
  no shape for "pick K targets and apportion N among them". Modelled as N independent 1-damage
  effects each with `Random()` targeting — Hearthstone's Arcane Missiles. **These cards cost one
  less than printed** to pay for the loss of aim. This needed no engine code: a card already
  carries a list of effects and each resolves its own targeting.
- **`DiscardAdditionalCost.Filter`** gives Magmatic Insight and Molten Vortex their real
  "discard a land card" cost. Faithful rather than reskinned — a land IS a card in hand here, and
  only becomes `MaxMana` when played.
- **`HasFlyingSpecification`** for Earthquake's "each creature without flying".
- **`CreatureCountComponent.Subtype`** — "+2/+0 for each other Goblin you control" (Goblin
  Piledriver, Goblin Rabblemaster). A field on the existing component rather than a parallel type:
  the counting, controller check, self-exclusion and live-evaluation rationale are identical, and a
  second copy is one more place to forget `Duration = Permanent`.
- **`CreatureDamagedEvent` never reached `PendingGameEvents`** — from both `DealDamageAction` and
  `AttackAction — so **no "whenever this creature is dealt damage" trigger had ever fired**. The
  fifth instance of that bug and the best disguised: the event already had an `EventTypeNames`
  constant, an `ExtractSubjectId` entry AND `TriggerAmountOf` support, so every downstream piece
  was ready for a trigger that could never arrive. Brash Taunter reflected nothing.

**Rules text caught four more silent failures, none visible from the card definitions.** Dumping
every red card's rendered text (the `MtgCardMapper` pass the "rules text is not cosmetic" rule
demands) found: Boggart Brute rendering a completely blank text box once menace was cut;
`ExileTopCardPlayableAction` missing from the mapper entirely, blanking both impulse-draw cards; a
context-driven `DealDamageAction` printing **"Deal 0 damage"** on Brash Taunter and Volley Veteran,
in two separate switches; `SpellCast` hardcoded to "Whenever you cast a spell", so Scab-Clan
Berserker described the opposite of the card it is. **Confidently wrong text is worse than blank
text — nothing looks broken.**

**Ogre Battledriver is the reason `ContextKeys.TriggerSubjectId` exists.** "Whenever another
creature you control enters, IT gets +2/+0 and haste" must land on the creature that entered; a
targeting strategy cannot see the event, so `Random()` buffs some other creature and the new
arrival misses the haste that is the card's whole point. Same failure as Wall of Frost.

### "Can't be countered" is kept, unlike most "can't be X" clauses

`CannotBeCounteredComponent`, checked by `CounterTrapEngine.TryCounterCast` **before a trap is
chosen**, so an uncounterable spell does not even SPEND the opponent's counterspell — a trap that
cannot counter its target was never a legal response to it.

This is worth implementing rather than cutting precisely because counterspells genuinely exist
here (blue's traps fire from hand off unspent mana), so the clause protects against something real
rather than describing a mechanic the game lacks. `Condition` reuses `ActivationCondition`, so
Exquisite Firecraft's spell mastery works with no new type.

**Banefire's is unconditional rather than "if X is 5 or more".** The chosen X lives on
`CastSpellAction`, not on the card — that is what lets two copies be cast for different X — so a
component on the card cannot see it. Threading `XValue` into the counter engine for one clause on
one card is not worth it.

### The rules-text pass found three more, all in the second half

`Earthquake` rendered **"deal X damage to each creature"** — the flying exemption is the entire
card, and `DescribeSpecification`'s walker had no `NotSpecification` case, so the wrapper was
walked straight past. `DiscardAdditionalCost`'s filter was ignored, so "discard a land card" read
as "discard a card" — understating it (only a land will do) and overstating it (a landless hand
cannot pay) at the same time. And `CannotBeCounteredComponent` rendered nothing at all.

**Two rules-text passes, eight silent failures, zero of them visible from the card definitions or
catchable by a cast-and-resolve test.** Dump and read the rendered text of every new card.

**Cut clauses, all with existing precedent.** Menace and "can't block"/"can't be blocked" (no
blocking — Boggart Brute, Frenzied Goblin, Goblin Glory Chaser, Stormblood Berserker); colour-based
targeting (Fry); Chandra, Fire of Kaladesh's flip to a planeswalker (as Kytheon, Jace and Liliana);
"if it would die, exile it instead" (Scorching Dragonfire — structural replacement, see
`DesignNotes.md`); "attacks each turn if able" (Borderland Marauder, Goblin Rabblemaster — no
forced-attack concept, and dropping it only ever helps the player, like vigilance).

**Goblin count-based buffs read the BOARD, not the attack.** Goblin Piledriver and Goblin
Rabblemaster are printed as "for each other attacking Goblin"; attacking is a fleeting state here
(one attack per turn, resolving immediately), so they count Goblins you control instead. Costed
down accordingly, since not having to commit the attack is a real upgrade.

## Green Section Mechanics (Core Set Cube)

Green is the counters colour, the ramp colour and the fight colour, and the engine had none of the
three properly. Two shipped cards were also found to be silent no-ops on the way through.

### +1/+1 Counters

`PlusOneCounterComponent : PowerToughnessModifier { Count }` — **one component per card whose count
mutates**, not N stamped modifiers. "Double the counters" and "remove a counter" both need a
number, and "count the permanent P/T modifiers" answers a different question: an anthem's
`AppliedStaticPTBoost`, an `EquippedBoostComponent` and Unholy Strength's +2/+1 are all permanent
and none are counters. Doubling would double those too.

`Duration` is forced to `Permanent` **in the constructor**. Every other live-evaluated modifier here
carries a comment begging callers to remember it; this one simply cannot be built wrong.

`CreatureEvaluator` and `HasPermanentPowerBonusSpecification` needed **no changes** — both already
walk every `PowerToughnessModifier`.

`AddCountersAction { Amount, Multiplier }` is the only mutator:
`newCount = max(0, old * Multiplier + ResolveAmount(Amount))`. Negative `Amount` removes;
`Multiplier = 2` is "double the counters on it". Builders: `WithSelfCounters`, `WithCounters`,
`WithSelfCounterMultiplier`, `CreatureCardBuilder.WithEntersWithCounters`.

**Counters are stripped on ENTRY, not on exit** — in `PutIntoBattlefieldAction.ApplyEntryCounters`,
and deliberately absent from `MoveCardTracked`'s closed strip list. Real MTG says two things that
pull apart here: counters cease to exist on a zone change, but a leaves-the-battlefield trigger uses
last-known information. Exit-stripping honours the first and breaks the second, **which is exactly
what was wrong with Chasm Skulker**: its counters were stripped before its own death trigger
resolved, so it read power 1, applied its −1 offset and created **zero** tokens. Entry-stripping
honours both. Do not move it.

`EntersWithCountersComponent { Count, FromXValue }` is applied in the ETB ceremony, not as an ETB
trigger — it is a replacement effect, and the ceremony is the one path every creature takes onto the
battlefield, so cast/reanimated/cloned/token all behave alike. It emits **no** `CountersAddedEvent`:
entering with counters is not counters being put on a creature.

`CountersAddedEvent` got the full four-site treatment (record, `EventTypeNames`, `ExtractSubjectId`,
`TriggerAmountOf`) and is staged into `PendingGameEvents`. **It is deliberately not
`CreatureModifiedEvent`**, which is inert *and* fires for Giant Growth — reusing it would have made
Wildwood Scourge grow off every combat trick.

`ThresholdComponent.CountSource` (`GraveyardCards` | `PlusOneCounters`) covers "trample as long as
it has ten or more +1/+1 counters". A field on the existing live-evaluated component rather than a
parallel type: `StaticAbilityEngine`'s push model would go stale the moment a counter landed, and a
second type is one more place to forget `Duration = Permanent` and one more `Grants*` list to keep
in sync with the six-site keyword rule.

**Barkhide Troll's "remove a +1/+1 counter" cost is reskinned**, not built. What the removal is FOR
is bounding the hexproof, and `MaxActivationsPerTurn = 1` does that for zero lines. Build
`RemoveCounterAdditionalCost` only when a second card wants to spend counters.

### X on creature spells

`CastCreatureAction.XValue` → `ResolveCreatureAction` → `PutIntoBattlefieldAction`'s
`InputContext[ContextKeys.XValue]`. Only `CastSpellAction` had X before, so `{X}` creatures were
impossible. `MtgActionGenerator.AffordableXValues` was already card-type agnostic and is reused
verbatim.

**`AddTargetedSpellAction` never looped X**, so a *targeted* X-spell could only ever be cast for
X = 0 — Primal Might's entire cost was unreachable. Fixed in the same pass.
`MtgGameManager.CastCreature` now passes `MaxAffordableX`, which it already had; without it a human
casting a Hydra got a 0/0 while the AI cast it correctly — the "the AI can do it and I can't"
signature.

### Fight, and two-target spells

Four cards say "target creature YOU CONTROL … target creature you DON'T control". Two independent
problems:

- **`TargetSelectionMode.Best`** — the engine picks the strongest valid target (highest effective
  power, ties on lowest id). `RequiresUserSelection` stays **false**, which is the whole point: a
  `Best` effect and a `UserSelect` effect coexist on one card with no change to Multi-Effect
  Targeting or `ValidateTargets`. Precedent: Clone picks highest power, `WithEdict` takes the
  cheapest. Power rather than mana cost, because these cards read "damage equal to its power" and in
  a counters set a two-mana Hydra with eight counters beats a five-mana 3/3.
- **`FightAction`'s source fallback** — on a spell, `ContextKeys.SourceCardId` is the *spell*, which
  has no `CreatureComponent`, so `FightAction` bailed out silently. **Hollowmere's Set Upon the Pack
  shipped as a complete no-op for exactly this reason.** When the source is not a creature, the
  caster's strongest creature fights instead.

Both share one picker, `CreatureEvaluator.PickStrongest` / `GetStrongestCreature`, so a card that
buffs and then fights cannot pick two different creatures.

`FightAction.OneSided` drops the return damage (Rabid Bite, Hunter's Edge).
`RequiresCreatureRestriction` stops these being castable on an empty board, where the fallback finds
nobody and the spell is a silent no-op at full price.

### Everything else

- **`AddModifierAction.PowerBonusContextKey` / `ToughnessBonusContextKey`** — two explicit keys, not
  an overload of the inherited `AmountContextKey`, which would have to mean "both bonuses" and would
  silently make `+X/+0` inexpressible.
- **`CountGreatestPowerAction`** emits both the maximum power **and** the creature-id list, from the
  one scan it already does. That second output is what makes Overwhelming Stampede expressible at
  all: a mass buff needs a number *and* a target list, and `PipelineAction` is not `ITargetedAction`
  so nothing can inject mass targets into a pipeline step. A live `GreatestPowerComponent` is
  explicitly rejected — its `GetPowerBonus` would call `GetEffectivePower`, which reads the same
  component on every other creature. That recurses.
- **`ConditionalCostReductionComponent.AppliesTo`** — a `TargetSpecification` asking about the CARD
  being cast, for a reduction that lives on a battlefield permanent (Goreclaw). `CostEngine` gained
  a **caster's-battlefield-only** scan; `ComputeTax` scans both sides because a tax is symmetric,
  but a discount must not cross the table.
- **`MtgGame.CreaturesDiedThisTurn`** — game-level, not `MtgPlayer`, because Fungal Rebirth asks
  about "a creature" either side. Incremented in `CheckStateBasedEffectsAction` from the pending
  events, **not** at the five actions that stage `CreatureDestroyedEvent` — five sites is five
  chances to miss one, which is how sacrifice came not to count as a death.
- **`ControlsSubtypeCondition`** — one type, used through the existing `ConditionalAction` rather
  than gaining a `TriggerCondition` twin. `ConditionalAction` evaluates with `cardId = 0`, so
  "another Elf" is expressed as `Minimum = 2` and the card counts itself. Say so on the card.
- **`BecomesBaseCreatureComponent.GrantsFlying/Trample/Reach`** — "becomes a 2/2 Bird **with
  flying**" is one effect. `GetEffectiveStats` applies that component's suppression *after* every
  grant, so a separate `GrantKeywordAction` would be stripped by the very effect meant to give it,
  and Skinshifter's bird mode would silently be a worse copy of its rhino mode.
- **`AsAura` could not grant trample, reach or deathtouch**, although `EquippedBoostComponent` has
  always carried all three. Rancor would have been a silent +2/+0.

### Mana dorks produce on your upkeep, never via a tap ability

All five green dorks are `TriggerConditions.OnYourUpkeep()` → `AddTemporaryManaAction`. No engine
work; both halves already existed. `StartTurnAction` refills `CurrentMana = MaxMana` and the trigger
resolves after that, so the mana is additive and evaporates at the next refill — correct, since it
must vanish if the creature dies — and a dork cast this turn produces nothing until the next upkeep,
which is what summoning sickness would have done.

The reason is not faithfulness, it is that **an unactivated mana ability is invisible**: the creature
just looks weak, and the AI has to re-derive the activation every turn on every dork before it can
cast anything. Price them slightly above their printed rate for the reliability.

### The rules-text pass found nine more, and one was catastrophic

`DescribeModes` rendered **every** mode against a hardcoded placeholder target, so Return to Nature's
"destroy target artifact" printed as **"Destroy each creature you control"** — a one-sided board wipe
on what is a Naturalize. Also: `AddTemporaryManaAction` and `GainLifeAction` printing "add 0 mana"
and "gain 0 life" for context-driven amounts **in the pipeline switch after the standalone switch was
already fixed** (fourth and fifth instances of that class); `AddModifierAction` printing "+0/+0";
`IsNotCardTypeSpecification` collapsing "noncreature permanent" to "permanent"; `PowerAtLeastSpecification`
dropped entirely, so Goreclaw discounted every creature spell; `SelectCardFromZoneAction.Filter`
ignored, so Woodland Bellower read as an unrestricted tutor; an OR of types with a creature clause
losing half itself (Vivien Reid); "Enchanted creature gets can't attack"; and `Elfs`.

**`NoCard_PrintsAnAmountThatMeansItDoesNothing` in `CoresetCubeRulesTextTests` is the blanket guard**
for the context-driven-amount class, because the next card to hit it has not been written yet.

### Auditing the bottom of the win-rate table found three more inert-card bugs

**A blank card and a merely weak card score the same (~40%), and only one is a bug.** Ten green
cards came back at 39–43%; seven of them turned out to do literally nothing.
`GreenLowWinRateAuditTests` pins each one by asserting the board or zone consequence — the
technique that works here is auditing the low band card by card, not rebalancing it.

1. **`WithDig` was a query with no consumer.** It built a bare `LookAtTopCardsAction`, which
   writes card IDs into pipeline context and stops. Nothing read them, so *every* dig card in the
   cube revealed cards and then did nothing: Track Down lost its cantrip, Llanowar Empath and
   Garruk's Harbinger were vanilla creatures, and Drawn from Dreams, Fateful Vision, Vivien Reid's
   `+1` and Hollowmere's Search the Parish were blank. `WithDig` is now reveal → choose → move,
   with `SelectFromRevealedAction` as the missing middle.
2. **`IsCreatureSpecification` is battlefield-only** (it enforces shroud/hexproof, so it must be),
   which makes it silently wrong as a `SelectCardFromZoneAction.Filter` against a LIBRARY: it
   matched nothing, and Shared Summons, Evolutionary Leap, Fauna Shaman and Woodland Bellower all
   searched and found nothing. **For "a creature CARD in any zone" use
   `IsCardTypeSpecification { Types = CardType.Creature }`,** which is zone-agnostic.
   `HasManaCostAtMostSpecification` is zone-agnostic already.
3. **A MODE could not target.** `SelectModeAction` spawns its `ResolveEffectAction` with no
   `TargetIds` — the mode is chosen during resolution, long after the cast action fixed its
   targets — so a `UserSelect` mode targeting strategy resolved to an empty list. Return to
   Nature destroyed nothing. This is the same failure `TriggerTargeting` exists for, in a second
   place, and it is now fixed the same way: `SelectModeAction` downgrades `UserSelect` to `Random`
   at the point of construction, so it is unreachable rather than fixed one card at a time.

**Harness note that cost real time:** `GameState.ProcessAllActions` deliberately STOPS at a
`ChoiceAction` — the AI resolves choices (`MultiTurnBeamSearchAiStrategy.ResolveAllChoices`), not
the action loop. A test that only calls `ProcessAllActions` leaves any card containing a scry or a
mode frozen mid-pipeline, and everything after it looks inert. Track Down, Llanowar Empath and
Return to Nature all looked broken for that reason on top of their real bugs. Use the
`SettleChoices` helper pattern in `GreenLowWinRateAuditTests` before concluding a card is dead.

## Black Section Mechanics (Core Set Cube)

**Four bugs were found building this, all of the same shape: a card that builds, casts and
resolves without erroring while doing nothing, or doing the wrong amount.** None were visible
without a test that asserted the consequence.

1. **`SacrificeAdditionalCost` never announced a death** — see "Additional Costs" above.
2. **`TriggerConditions.OnYourUpkeep()` had no filter.** `TurnStartedEvent`'s subject is the
   player whose turn began, so every upkeep trigger in the engine fired on **both** turns, at
   double the printed rate. Six existing white/blue/Hollowmere cards were affected.
   `OnOpponentUpkeep()` is its counterpart (Stab Wound).
3. **`DrawCardsAction` ignored `AmountContextKey`.** It looped on the raw `Amount` field, so any
   context-driven draw silently drew the default 1.
4. **`MtgActionGenerator.BuildAdditionalCostPayments` only ever offered one payment**, while
   `Validate` demands an exact count — so a cost of 2 made the card permanently uncastable with
   no error. `AdditionalCost.RequiredPaymentCount` fixes it; **any selection cost with a `Count`
   must override it.**

### What was added

- **`ContextKeys.TriggerAmount`** — the numeric payload of the event that fired a trigger,
  injected by `ResolveEffectAction` from `CheckStateBasedEffectsAction.TriggerAmountOf`. This is
  what makes "whenever you lose life, draw **that many** cards" (Vilis) expressible; every such
  clause had previously been flattened to a constant. Read it with `EffectAction.AmountContextKey`.
  `TriggerAmountOf` is a deliberately closed list of events — an event that gains an unrelated
  numeric field later must not start silently feeding these effects.
- **`MtgPlayer.LifeLostThisTurn`** — mirror of `LifeGainedThisTurn`, accumulating in
  `LoseLifeAction`, `DrainLifeAction` and the player-damage path of `DealDamageAction`.
  **It resets for BOTH players in `StartTurnAction`, unlike every other per-turn counter.** Life
  loss overwhelmingly happens to the non-active player, so an active-player-only reset would let
  the defender's tally span two turns. Read by `LifeLostThisTurnCondition` (which asks about ANY
  player by default) and `OpponentLostLifeThisTurnCondition` (bloodthirst).
- **`SetLifeTotalAction`** — absolute set, deliberately NOT routed through `ReplacementEngine`
  (a life-gain bonus must not turn "becomes 10" into "becomes 11"). **`Amount = 0` is how "you
  lose the game" is expressed**: the state-based loss check already owns `HasLost`,
  `PlayerLostEvent` and winner determination, so there is no second way to lose.
- **`ChosenModesComponent` + `SelectModeAction.ExcludeAlreadyChosen` +
  `ApplyChosenModeAction.RecordChoice`** — "choose one that hasn't been chosen" (Demonic Pact).
  State lives on the CARD because the exclusion must survive between resolutions turn after
  turn; `ActivationCount` is reset every turn and pipeline context dies with the resolution.
  The two flags must move together, so `WithModes(onceEach: true, …)` sets both.
- **`FlashbackComponent.AdditionalCosts`** + **`ExileFromGraveyardAdditionalCost`** — a
  repeatable graveyard recursion bounded only by mana just returns forever; a cost that eats the
  graveyard gives it a hard floor and makes graveyard hate live. Paid **before** the card leaves
  the graveyard, so it cannot select the card paying for itself.
- **`WithSymmetricEdict(excludeSubtype)`** — "each player sacrifices a creature". Takes the
  **cheapest** on each side, not the biggest: a real edict lets each player choose and each keeps
  their bomb, so taking the biggest would make a symmetric effect one-sided.
- **`WithTutor()`** with no subtype, backed by `SelectCardFromLibraryAction.SelectBestByManaCost`.
  Library order is random, so a first-match search with no subtype is just "draw the top card" —
  Grim Tutor would have been a strictly worse Sign in Blood.
- **`WithChosenDiscard(n)`** — "you choose a card" via most-expensive-non-land, as distinct from
  `WithOpponentDiscard`'s random. The gap is real: random discard off a full hand is a coin flip.
- **`WithDrain(n)`** — hand-rolled ~10× in Hollowmere before this. Must be `DrainLifeAction`
  rather than `WithLoseLife` + `WithLifeGain`, because inside a trigger `NoTarget()` overwrites
  hardcoded `TargetIds` and the loss half hits nobody.
- **`WithReanimate(fromAnyGraveyard: true)`** / `IsCreatureInAnyGraveyardSpecification`,
  `CreatureCostBuilder.PayLife(n)` / `SpellCardBuilder.WithLifeCost(n)`,
  `HasAttackedThisTurnSpecification`, `DifferentPowerAndToughnessSpecification`, and the trigger
  helpers `OnCreatureYouControlDies(subtype)`, `OnOpponentCreatureDies()`,
  `OnOpponentCreatureEnters()`, `OnCreatureAttacksYou()`, `OnCardDiscarded()`.

**Dark Tutelage needed no new code** — `RevealTopCardAction` already outputs
`ContextKeys.RevealedCardManaCost` (built for Dark Confidant) and `LoseLifeAction` already reads
`AmountContextKey`.

## Card Builder Additions (originally for Hollowmere, now set-agnostic)

`CreatureCardBuilder`: `WithDeathtouch()`, `WithThreshold(power, toughness, minimum, …)`,
`WithGraveyardRecursion(manaCost)`, and `WithDeathTrigger(name, effect)` — the last sets
`ActiveInZone = Graveyard` for you, which is mandatory and easy to forget.
`WithTriggeredAbility` now takes an optional `ActiveInZone` and keeps **all** effects the
builder produced, not just the first.

`SpellCardBuilder`: `WithMill(n)` (defaults to milling yourself; override with
`.WithTarget(Single().Opponent())`), `WithReanimate()`, `WithReturnCreatureFromGraveyard()`,
`WithReturnSpellFromGraveyard()`, `WithGrantKeyword(…)`, `WithExileFromGraveyard()`,
`WithOpponentDiscard(n)`, `WithDiscard(n)`, `WithProwessBuff()`, `WithCreateTokensPerCard(…)`.

**Discard comes in two flavours and they are not interchangeable:**

| | `WithDiscard(n)` | `WithDiscardCost(n)` |
|---|---|---|
| Mechanism | `SelectCardsFromHandAction` → `DiscardCardsAction` pipeline, no targeting | `DiscardAdditionalCost` on `AdditionalCastCosts` |
| Chosen | At resolution | Before the spell reaches the stack |
| Empty hand | Casts, discards nothing | Uncastable |
| Card text | "draw 4, then discard a card" | "as an additional cost, discard a card" |

A post-draw discard **must** be the choice form: cast-time selection happens before the draw, so a
targeted version could only ever pitch from the pre-draw hand.

Both are human-playable — cards in hand are clickable as targets and as cost payments (see
`MtgGameScene.OnHandCardClicked`). A hand card cannot be draggable and clickable at once:
starting a drag clears `CardUIManager.CurrentHoveredCard` on the same press, which is the
guard `CardUI2D._UnhandledInput` tests, so `Hand2D.DragEnabled` must be false whenever the
hand is a selection surface (`MtgGameScene.IsWaitingForSelection`).

Removal and selection verbs, added because the set was built from ~13 verbs and cards had
begun repeating each other at different mana costs: `WithWeaken(p, t)` (-X/-X, kills via the
zero-toughness rule), `WithBounce()`, `WithFight()`, `WithEdict()` (picks by mana cost, so it
answers Hexproof and Shroud), `WithTutor(subtype)`, `WithDig(n)`.

**The card design floors were `MtgCore.Tests/HollowmereRateTests.cs`, and went with that set.**
The RULES are still the right ones and a new set should re-establish them: an ability-less creature
must meet a stats-plus-keywords rate floor, and no pure token-maker may be strictly worse than
another. Both existed because real cards failed them. Keep the comparisons to things code can judge
honestly; whether a card is *interesting* is a human review job.

`TargetBuilder`: `Players()`, `Opponent()`, `AllYourCreatures()`, `CreaturesInYourGraveyard()`,
`OtherCreaturesYouControl()`.

## Card Builder Additions for the Core Set Cube

`CreatureCardBuilder`: `WithFirstStrike()`, `WithIndestructible()`, `WithShroud()`,
`WithHexproof()`, `WithExalted(count)`, `WithProtectionFrom(params subtypes)`,
`WithLifeTotalBonus(p, t, minimum)`, `WithPowerEqualToCreatureCount()` (the `*/*` templating — build
the card at base 0/0), `WithSpellTax(amount)`, `WithLifeGainBonus(amount)`,
`WithCastRestriction(...)`, and `WithRenown(n)`.

`WithRenown` is worth using rather than hand-rolling: it sets `MaxTriggers = 1`, the **lifetime**
cap, which is what "if it isn't renowned" means. A per-turn cap makes the creature grow every turn.

`WithActivatedAbility` now takes `condition`, `requiresTap` and `maxPerTurn`.
`WithTriggeredAbility` now takes `maxTriggers` and `maxPerTurn`.

`SpellCardBuilder`: `WithExhaust()` (defaults to an opponent's creature) and `WithSelfBuff(p, t)`
— a permanent buff on the card running the effect, i.e. "put a +1/+1 counter on this creature".
`WithSelfBuff` sets `TargetContextKey = SourceCardId` rather than hardcoding `TargetIds`, which
matters because `ResolveEffectAction` overwrites hardcoded targets on a `NoTarget()` strategy.
`WithGrantKeyword` gained `firstStrike`, `doubleStrike`, `indestructible`, `exalted`, and the
previously-missing `reach`/`shroud`/`hexproof`.

`CreatureCostBuilder`: `SacrificeSelf()`, `Discard(count)`.

`SpellCardBuilder` (second pass): `WithScry(n)` — a real choice with `MinChoices = 0`, since an
unconditional bottom-the-top-card is strictly worse than doing nothing half the time;
`WithDamagePrevention(...)`, `WithConditionalAction(condition, action)`, `WithModes(...)`,
`WithXCost()`, `WithConvoke()`, `WithTypes(...)`.

Red pass: `WithImpulseDraw()` (see "Impulse Draw"), and `WithDiscardCost(count, subtype)` /
`CreatureCostBuilder.Discard(count, subtype)` for "discard a land card". Both discard-cost builders
route through one shared `DiscardCost` helper so the filter and its player-facing wording cannot
drift apart.

`PermanentCardBuilder` (new — `CardFactory.Enchantment` / `.Artifact` / `.Planeswalker`):
`WithLoyalty(n)`, `WithLoyaltyAbility(name, cost, effect)`, `AsAura(...)`, `WithStaticBoost(...)`,
plus the usual `WithEtbTrigger` / `WithTriggeredAbility` / `WithActivatedAbility` / `WithComponent`.
Its `WithEtbTrigger` uses `OnSelfEntersBattlefieldAsNonCreature` — a non-creature permanent never
fires `CreatureEnteredBattlefield`, so the creature version silently never triggers.

Deliberately a separate class from `CreatureCardBuilder` rather than a shared base: several hundred
existing cards depend on that builder, and re-parenting it to extract four small methods is a far
riskier change than duplicating them.

`SelectCardFromZoneAction.Filter` takes a `TargetSpecification`, because subtype alone cannot
express "a creature card" or "an instant or sorcery" — those are identified by components.

`ReturnToHandAction` is the targeted counterpart to `MoveCardToHandAction`, so
"return target creature card from your graveyard to your hand" works with ordinary targeting
instead of a pipeline.

## Card Creation Cookbook

Canonical recipes using verified working cards. All examples are in `Cards/CardLibrary.cs`.

### 1. Simple ETB trigger (no pipeline)

```csharp
CardFactory
    .Creature("Name", manaCost: N, power: P, toughness: T)
    .WithEtbTrigger("ETB Effect", eb => eb.WithCreateTokens(SomeToken()))
    .Build()
```

`WithEtbTrigger` is shorthand for `WithTriggeredAbility` using `TriggerConditions.OnSelfEntersBattlefield()`. Use `eb.WithDamage(3)`, `eb.WithDraw(1)`, etc. for non-token effects.

### 2. Death trigger (fires from graveyard)

```csharp
CardFactory
    .Creature("Mogg War Marshal", manaCost: 1, power: 1, toughness: 1)
    .WithComponent(new TriggeredAbilityComponent
    {
        Name = "Dies Token",
        Condition = TriggerConditions.OnSelfDies(),
        Effect = new CardEffect
        {
            TargetingStrategy = TargetingStrategy.NoTarget(),
            ActionTemplate = new CreateCardAction { CardTemplate = GoblinToken(), Count = 1 },
        },
        ActiveInZone = ZoneType.Graveyard,   // REQUIRED — card is already in graveyard when trigger fires
    })
    .Build()
```

**Key rule:** `ActiveInZone = ZoneType.Graveyard` is mandatory for death triggers. By the time `CheckStateBasedEffectsAction` scans for triggers, the card has already moved to the graveyard. `TriggerConditions.OnSelfDies()` wraps `EventTriggerCondition` + `IsSourceCardSpecification` so the trigger fires only for this specific card.

### 3. Pipeline ETB tutor (search library → put in hand)

```csharp
.WithEtbTrigger("ETB Tutor", eb => eb.WithAction(
    new PipelineAction
    {
        Steps = ImmutableList.Create<GameAction>(
            new SelectCardFromLibraryAction
            {
                Subtype = "Goblin",
                OutputKey = "tutor_target",
                PlayerIdContextKey = ContextKeys.CastingPlayerId,
            },
            new MoveCardToHandAction
            {
                CardIdContextKey = "tutor_target",
                PlayerIdContextKey = ContextKeys.CastingPlayerId,
            }
        ),
    },
    TargetingStrategy.NoTarget()
))
```

`SelectCardFromLibraryAction` writes a card ID to `OutputKey`. `MoveCardToHandAction` reads it via `CardIdContextKey`. `ContextKeys.CastingPlayerId` is injected by `ResolveEffectAction` before the pipeline runs — always use it to identify the card's controller. For multiple tutor steps (e.g. Ringleader fetching 3), chain pairs with unique keys (`ringleader_1`, `ringleader_2`, `ringleader_3`) — each step sees the updated game state so already-moved cards are skipped automatically.

### 4. Activated ability with pipeline and self-buff

```csharp
.WithActivatedAbility("Ability Name", manaCost: 0,
    effect: eb => eb.WithAction(
        new PipelineAction
        {
            Steps = ImmutableList.Create<GameAction>(
                new SelectCardFromZoneAction
                {
                    Zone = ZoneType.Graveyard,
                    TargetOpponent = true,                        // targets opponent's zone
                    PlayerIdContextKey = ContextKeys.CastingPlayerId,
                    OutputKey = "exile_target",
                },
                new MoveCardToExileAction { CardIdContextKey = "exile_target" },
                new GainLifeAction
                {
                    Amount = 1,
                    TargetContextKey = ContextKeys.CastingPlayerId,
                },
                new AddModifierAction
                {
                    PowerBonus = 1,
                    ToughnessBonus = 1,
                    Duration = ModifierDuration.Permanent,
                    TargetContextKey = ContextKeys.SourceCardId,   // buff the card itself
                }
            ),
        },
        TargetingStrategy.NoTarget()
    )
)
```

**`TargetContextKey = ContextKeys.SourceCardId`** on `AddModifierAction` buffs the card running the ability. `ContextKeys.SourceCardId` is injected by `ResolveEffectAction` alongside `CastingPlayerId`. **`TargetOpponent = true`** on `SelectCardFromZoneAction` derives the opponent via `GetOpponentId(gameState, castingPlayerId)` — same pattern as `DiscardRandomCardAction` and `SelectCardFromHandByManaCostAction`. Canonical example: Scavenging Ooze in `CardLibrary.cs`.

### 5. Context key quick-reference

| Key | What it holds | Who injects it |
|-----|--------------|----------------|
| `ContextKeys.CastingPlayerId` | Controller of the resolving card/ability | `ResolveEffectAction` |
| `ContextKeys.SourceCardId` | ID of the card whose effect is resolving | `ResolveEffectAction` |
| Custom key (e.g. `"tutor_target"`) | Output from a previous pipeline step | `SelectCardFromLibraryAction` / `SelectCardFromZoneAction` |

All `PlayerIdContextKey` and `CardIdContextKey` fields on actions read from the pipeline's `InputContext` — only set them when you want runtime lookup. Leave them empty and use the direct `int` fields for compile-time-known values.

## Colour assignment: beware name-anchored bulk edits

**Assignment status: every live set is assigned and verified** — CSC (`CoresetCubeColorTests`),
LEG and CMB (`LegacyAndComboColorTests`). HLM was retired rather than coloured.

Artifacts and lands stay colourless on purpose: an artifact's real colour IS colourless, and a
land's colour is what it PRODUCES (`LandColorComponent`), not what it costs. CMB's counters package
is artifact creatures and so is colourless too.

**The first pass at assigning LEG and CSC anchored insertion on any quoted occurrence of a card's
name, and card names appear in other cards' doc comments** — so pips landed on whatever card was
defined next. Llanowar Elves came out needing UUBRG and Lotus Bloom, a colourless artifact, needed
UU, and every existing test still passed. **Anchor on the FACTORY CALL, and assert an expected pip
table.**

## The card sets

**Core Set Cube (CSC)** — built from an external cube list
(https://cubecobra.com/cube/list/magiccoreset20xx). All five colours complete: **335 cards**; the
colourless (50) and multicolour (53) sections are what remain. See `MtgCore/Sets/CoresetCube/`.

The cube is 450 cards: 67 per colour, 50 colourless, 53 multicolour. Verify a colour's card list by
downloading `cubecobra.com/cube/download/csv/magiccoreset20xx` and filtering on the `Color` and
`board` columns — the HTML page is a SPA and cannot be scraped.

**CSC is the set the Godot draft mode plays** (`DraftScene.DraftedSet`). Making a set playable is
three things, not one: the cards, the rules text that renders them, and a trained draft model. A set
with correct cards and no rules text is undraftable, and the failure is invisible in a screenshot.

**Combo Proving Ground (CMB)** is a test instrument, not a draftable set —
`MtgCore/Sets/ComboProving/`. It plants combos with known answers so a run can distinguish "the
builder cannot find combos" from "this pool has none". It is a **supplement meant to be played
inside DES**, never alone: at ~60 cards `DeckCore.MinPoolForBreadth` (100) switches the breadth gate
off entirely, so a run over CMB by itself measures the fixture. Nothing in it is costed to a rate —
read cohesion and assembly, never win rate.

**Hollowmere (HLM) was RETIRED** along with its cards, tests and trained model. It was designed
around the engine having no colours — "ten overlapping themes stand in for colours" — and its tribes
do not map onto five colours evenly, so colouring it would have meant redesigning it. A new set
built WITH colours from the start is the intended successor.

Sets sourced from a real cube exist to force new mechanics: the card list drives the engine rather
than the engine driving the list. When a card needs something the engine lacks, **build the
mechanic** — dropping the ability defeats the exercise. Only cut text when the concept is
structurally absent (no blocking, no priority), and comment the cut on the card.
