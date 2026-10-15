# Scripting a card — the MagicCore cookbook

How a card becomes playable, by pattern, with real FRA scripts to copy. For **what pieces exist**, read
`MagicCore/AbilityCatalog.md` (generated); for **every card's script next to its printed text**,
`MagicCore/FraCardSheet.md` (generated). The rules each mechanic follows are in
`.claude/rules/magic-mechanics.md`.

## How a card is built

A card's printed data — name, cost, types, P/T, loyalty, keywords — comes from `Sets/FRA.json`
(Scryfall). Its **abilities** are hand-written in `Sets/Fra/FraCards.cs`, in the `Scripts` dictionary:

```csharp
["Card Name"] =
[
	/* one entry per ability: triggers, activated abilities, statics, the spell's text */
],
```

- **Keywords need nothing.** Flying, reach, trample, deathtouch, lifelink, first/double strike,
  vigilance, haste, menace, defender and flash come from the printed keywords. A vanilla or
  keyword-only card has no entry at all.
- **Everything else is data**: an effect is a record (`new DrawCards(Ref.You, 2)`), "who" is a `Ref`,
  "how many" is a `Count`, "if" is a `Condition`. No card has code of its own — if a card needs
  something no piece does, that is new ENGINE work (tier 3, below), never a special case in the script.
- The card name must match Scryfall exactly (two-faced: `"Pyre Rhymer // Molten Tide"`).

## Patterns

**An instant or sorcery** — `Spell(targets, effects…)`. `Ref.Target()` is the first target word.

```csharp
["Prophesied End"] =        // Destroy target creature. If it wasn't attacking, its controller draws a card.
[
	Spell(
		[Creature],
		new If(new Matches(Ref.Target(0), !Is.Attacking), [new DrawCards(Ref.ControllerOfTarget(0), 1)]),
		new Destroy(Ref.Target())
	),
],
```
Order matters: the "wasn't attacking" check runs before the destroy, while the creature still knows.

**"Choose one —"** — `ModalSpell(name, howMany, M(targets, effects…), …)`, one `M` per printed bullet
(it refuses a count that differs). Each mode's `Ref.Target()` is its own first target.

```csharp
["Theorix Charm"] =
[
	ModalSpell(
		"Theorix Charm",
		1,
		M([TargetSpec.Spell(!Is.Creature)],
			new CounterUnlessPays(Ref.Target(), ManaCost.Parse("{2}"))),
		M([Creature], new ModifyUntilEndOfTurn(Ref.Target(), -2, -2)),
		M([], new Mill(Ref.You, 3), new DrawCards(Ref.You, 1))
	),
],
```

**A triggered ability** — `When(trigger, targets, effects…)`. A trigger is an event kind, which of its
subjects (`Subject`), who did it (`By`), at which `Step`: the common ones are named (`TriggerEvent.SelfEnters`,
`YourUpkeep`, `YouCastNoncreatureSpell` — the catalog's `TriggerEvent` line); a one-off writes its own,
`new Trigger(EventKind.Entered) { Subject = Is.Creature & Is.Yours & !Is.Source & !Is.Token }` (Arni), or
narrows a named one, `TriggerEvent.YouCastSpell with { Subject = Is.PreparedCopy }`. "Whenever a Plains you
control enters" is a Subject (`Is.Subtype("Plains")`), never an `If`. **An activated ability** — `new ActivatedAbilityComponent(cost, [effects])` with its
options (`Costs`, `Targets`, `From = ZoneType.Graveyard`, `SorcerySpeed`, …). **Costs beyond mana** are one
list, in the order paid: `Costs = [new TapSelfCost(), new SacrificeCost(Is.Artifact & !Is.Source)]` — also
`SacrificeSelfCost`, `DiscardSelfCost`, `DiscardCost(filter)`, `ExileSelfCost`, `ExileFromGraveyardCost(filter)`,
`TapUntappedCost(n, filter)`, `BeholdCost(subtype)`. The same list is a spell's additional cost
(`new AdditionalCostsComponent([new SacrificeCost(…) { OrPay = 3 }])` — "or pay {3}"), a flashback's
(`FlashbackComponent(cost) { Costs = [new DiscardCost()] }`) and ward's (`CounterUnlessPays { Costs = … }`).
**"Costs {N} more/less"** is `CostChange(amount, spellsFilter)`: on a permanent for whose spells (`By`), or
`OnItself = true` for "this spell costs … less", with an `If`.

```csharp
["Arni, Humble Scribe"] =   // Whenever another nontoken creature you control enters, untap Arni.  {T}: Draw, then discard.
[
	When(
		new Trigger(EventKind.Entered) { Subject = Is.Creature & Is.Yours & !Is.Source & !Is.Token },
		[],
		new Untap(Ref.Source)
	),
	new ActivatedAbilityComponent(ManaCost.None, [new DrawCards(Ref.You, 1), new Discard(Ref.You, 1)])
	{
		Costs = [new TapSelfCost()],
	},
],
```

**"If …" on a trigger** — an *intervening* if ("When/At …, if X, …") is `When(…) with { If = condition }`
— checked as it triggers AND as it resolves. Never a bare `If` effect for that (rule in magic-mechanics.md).

**"You may … If you do, …"** — `new May(prompt, canDoIt, [the doing, then the rest])`:

```csharp
["Guiding Hydra"] =         // enters with X counters; at your combat, you may move a counter to each other creature
[
	new EntersWithCountersComponent(Count.X),
	When(
		TriggerEvent.BeginningOfCombatOnYourTurn,
		[],
		new May(
			"Remove a +1/+1 counter from Guiding Hydra?",
			new AtLeast(Count.CountersOn(Ref.Source), 1),
			[new AddCounters(Ref.Source, -1), new AddCounters(Ref.OtherCreaturesYouControl, 1)]
		)
	),
],
```

**A static ability** — `new StaticEffectComponent(scope, "Keyword") { Power = …, Toughness = …, If = … }`,
read live while its source is on the battlefield. The scope is a filter: `Is.Source` ("this"),
`Is.Creature & Is.Yours & Is.Subtype("Thopter")` ("Thopters you control"). A scope that reads keywords, or
a P/T static's scope that reads power/toughness, is refused as it's built (Characteristics would loop).
**"If …"** on a thing is `new Matches(ref, filter)` — `Not(new Matches(Ref.Source, Is.Token))` is "if it
isn't a token". **Equipment / Auras** — `AttachedGets(p, t)`, a static on `Is.AttachedToSource`,
`Equip("{2}")` or `new AuraComponent(enchantSpec)`:

```csharp
["Medic's Kitesail"] =      // Equipped creature gets +1/+0, has flying and "whenever this attacks, gain 1". Equip {2}.
[
	AttachedGets(1, 0),
	new StaticEffectComponent(Is.AttachedToSource, "Flying"),
	When(TriggerEvent.AttachedCreatureAttacks, [], new GainLife(Ref.You, 1)),
	Equip("{2}"),
],
```

**A planeswalker** — its loyalty is printed; each ability is `Loyalty(cost, targets, effects…)`
(`new LoyaltyAbilityComponent(0, …) { MinusX = true }` for "−X"; `GetEmblem([...])` for an emblem):

```csharp
["Ajani Unrelenting"] =
[
	When(TriggerEvent.YouActivateLoyaltyAbility, [], new CreateTokens(Cadet, 1)),
	Loyalty(1, [], new ModifyUntilEndOfTurn(Ref.CreaturesYouControl, 1, 0, "Haste")),
	Loyalty(-2, [], new DiscardHand(Ref.You), new DrawCards(Ref.You, Count.CreaturesYouControl)),
	Loyalty(-3, [], new DealDamage(Ref.CreaturesExceptYourTokens, 4)),
],
```

**Tokens** — `new CreateTokens(Cadet, n)` with a token's `CardData` (Cadet, Beast, Treasure… at the top of
FraCards); its abilities in `Abilities = [...]`. **Abilities from the graveyard** — `FromGraveyard = true`
on a trigger, `From = ZoneType.Graveyard` on an activated ability (see Command the Stage in the sheet).

**Targets** are a zone and a filter. A permanent: `Target(Is.Creature & Is.Yours & !Is.Source)` is
"another target creature you control"; `Creature` and `CreatureOrPlaneswalker` are the plain ones.
Elsewhere: `TargetSpec.Player(Is.Opponents)`, `TargetSpec.AnyTarget()`, `TargetSpec.Spell(!Is.Creature)`,
`TargetSpec.InYourGraveyard(Is.Creature & Is.ManaValueAtMost(3))`; "spell or creature" is
`TargetSpec.Spell() with { Or = TargetSpec.Creature }`. "Up to one" is `with { Optional = true }`. Write
the filter as the text reads — type first, then whose, then the rest. **A sacrifice or graveyard cost is a
bare filter over your own zone** — `Sacrifice = Is.Artifact & !Is.Source`, `new MaySacrifice(Is.Creature,
…)` — no `Is.Yours`, no `Target(…)`. Every filter word: the catalog's `Is` line.

**Groups of objects** — a named `Ref` (`Ref.CreaturesYouControl`, `Ref.OtherCreaturesYouControl`…), or
any filter over the battlefield: `Ref.Permanents(Is.Token & Is.Yours & Is.Subtype("Wizard"))` is "each
Wizard token you control". Another zone: `Ref.All(ZoneType.Graveyard, Is.Creature, of: Ref.You)`; whose
zone is `of` — "each creature target player controls" is `Ref.All(ZoneType.Battlefield, Is.Creature,
Ref.Target(0))`. A `Count` counts a `Ref`: `Count.NumberOf(Ref.Permanents(…))`.

## How much checking a card needs — the tiers

| Tier | The card | Up front |
|---|---|---|
| 0 | Vanilla or keywords only | No script, no test |
| 1 | Only pieces and options already tested | Script; the every-card random-game test covers crashes; **read its card-sheet entry against the printed text** |
| 2 | A tested piece with a new option or combination | One test of the new option; revert-check that line |
| 3 | A new engine piece | The full process: tests, revert-checks, fuzz probe, `/pre-commit` (the `/card-batch` skill) |

A **set sanity pass** (a test per card that casts it and checks something happened) is optional, on
request — before an Arena direct, say.

## A card test (tiers 2–3)

Card tests derive from `MagicCore.Tests/Sets/FraBoardTest` (a first main phase, four of each basic for
both players) and use `TestCards` for inline helpers. Real casting, the real stack:

```csharp
[Test]
public void Gardenize_AChargeCounterPerDeath()
{
	var gardenize = Field(FraCards.Get("Gardenize"), me);
	var bear = Field(TestGames.Creature("Bear", "2/2"), me);
	Cast(Hand(TestCards.Kill(), me), [], bear);   // casts and resolves the whole stack
	Assert.That(s.GetCard(gardenize).ChargeCounters, Is.EqualTo(1));
}
```

Two traps: call a helper that changes `s` on its own line (`s.Act(new … { CardId = Hand(…) })` reads the
OLD `s`); and in a choice answer use a `switch`, never `c is T ? [x] : []` (CSharpier stops formatting).

## Tools

```
python tools/magic_set.py text "Card Name"…        # printed text
python tools/magic_set.py coverage                 # playable by rarity, and what's missing
python tools/magic_docs.py                          # regenerate the catalog and the card sheet
python tools/edit.py SPEC                           # replace-exactly-once edits, all or none
python tools/revert_check.py SPEC                   # break each piece, confirm its test fails
MAGIC_PROBE_CARDS="A;B" dotnet test MagicCore.Tests --filter "FullyQualifiedName~CardProbeTests"
tools/bench_vs_head.sh                              # timing vs HEAD, for hot-path changes
python tools/usage_report.py                        # what the session cost
```
