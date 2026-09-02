using MtgCore.Cards.Builders;
using static MtgCore.Cards.Builders.TargetBuilder;

namespace MtgCore;

/// <summary>
/// **Package 4 — +1/+1 counters, and the package most likely to be found as the WRONG archetype.**
///
/// Every piece is an artifact creature that grows, plus a replacement effect that makes every
/// counter bigger. No loop, no single-card payoff: the deck is worth playing only when the counter
/// producers and the counter multiplier are in the same list.
///
/// ### Measured, and a pessimistic prediction here was WRONG
///
/// The guess was that "counters" could not be a demand at all: the `PoolFeatures` fixture places
/// cards with `AddObject`, which skips the enters-the-battlefield ceremony, so nothing in it ever
/// has a +1/+1 counter — and a filter reading "a creature with a counter on it" would be answered
/// by zero cards and build no core.
///
/// **Measured on DES, the Marshal builds a core with 28 suppliers.** The reason the guess failed is
/// that <see cref="HasPermanentPowerBonusSpecification"/> is broader than its name suggests: it
/// matches any PERMANENT `PowerToughnessModifier` with a positive bonus, which is deliberate — the
/// engine's own comment says "a permanent `AddModifierAction` IS our counter, so this is the
/// faithful question". So Tarmogoyf's `GraveyardCountComponent`, the `*/*` cards, threshold
/// creatures and the land-count cards all answer it. The demand is real, and it is "creatures that
/// grow" rather than strictly "creatures with counters".
///
/// What DID hold: the Ravager is found primarily as an **ARTIFACT** deck, via its
/// `SacrificeAdditionalCost.Filter` — the same demand Atog and Thoughtcast ask — with a 46-card
/// artifact slot. `MtgSimulator/CLAUDE.md` records that an artifact deck is not a distinguishable
/// archetype in this pool, so the counters theme and the affinity theme overlap here by
/// construction.
///
/// The Rite and the Ballista harvest nothing, as expected: neither names a card filter anywhere.
///
/// ### One new cost was needed
///
/// <see cref="RemovePlusOneCounterAdditionalCost"/>. The existing counter cost spends CHARGE
/// counters, which carry no P/T and are immune to counter doublers — the wrong resource entirely.
/// Writing the Ballista's ability as an effect instead ("remove a counter and deal 1 damage") would
/// have been free repeatable damage, because `AddCountersAction` floors at zero while the damage
/// half resolves regardless.
/// </summary>
public static class ComboProvingCounters
{
	public const string Artifact = "Artifact";

	/// <summary>
	/// Modular, as printed at 1: the creature enters with a +1/+1 counter and hands one on when it
	/// dies. **No new mechanic was needed** — real modular moves however many counters the creature
	/// died with, which would need an action that reads the dying card's counter count, but at
	/// modular 1 "enters with one, gives one" is the same card. Anything larger would need the
	/// general version; say so rather than quietly scaling this.
	///
	/// The death trigger targets with `Best()`, not `Single()`: a trigger has no cast-time targeting
	/// step, so a UserSelect strategy resolves empty and the counter goes nowhere.
	/// </summary>
	private static CreatureCardBuilder Modular(CreatureCardBuilder builder) =>
		builder
			.WithEntersWithCounters(1)
			.WithDeathTrigger(
				"Modular",
				eb =>
					eb.WithCounters(1)
						.WithTarget(
							Best()
								.WithSpec(
									new IsCreatureSpecification().And(
										new IsControlledByYouSpecification()
									)
								)
						)
			);

	public static IReadOnlyList<Card> Cards { get; } =
		[
			// Hardened Scales. A REPLACEMENT rather than a trigger, and that distinction is
			// load-bearing: a trigger that adds counters in response to counters being added feeds
			// itself forever. Because the bonus applies inside AddCountersAction, exactly one
			// CountersAddedEvent is emitted and it already carries the increased number.
			//
			// ApplyReplacements walks the controller AND every permanent they control, so this
			// affects counters placed anywhere on your board rather than only on itself — the same
			// way Conclave Mentor works in CSC. One mana and no body, so it is strictly the enabler
			// half rather than a card that is fine on its own.
			//
			// **MEASURED LIMITATION: it does NOT apply to a creature ENTERING with counters.**
			// `EntersWithCountersComponent` is stamped by the ETB ceremony rather than going through
			// `AddCountersAction`, and the `CountersPlaced` replacement lives inside that action. So
			// the Servitor still enters as a 1/1 beside this, where real Hardened Scales would make
			// it a 2/2. Only counters ADDED afterwards — the Ravager's Devour, the Ballista's Graft
			// — are boosted. `ComboProvingCountersTests.EntryCountersAreNotBoostedByACounterBonus`
			// characterises it. Not fixed here because routing entry counters through the
			// replacement would change a live CSC interaction (Conclave Mentor with every Hydra and
			// Hangarback Walker), which is a measured-set change rather than a test-set one.
			CardFactory
				.Enchantment("Ironscale Rite", manaCost: 1)
				.WithComponent(new CounterBonusComponent { Amount = 1 })
				.Build(),
			// Walking Ballista. Printed 0/0 with X counters, so it IS its counters — which makes it
			// the cleanest possible test of whether the multiplier is being valued: every extra
			// counter is literally another point of damage.
			CardFactory
				.Creature("Sporeback Ballista", manaCost: 0, power: 0, toughness: 0)
				.WithSubtype(Artifact)
				.WithXCost()
				.WithEntersWithCounters(fromXValue: true)
				.WithActivatedAbility(
					"Fling Spore",
					manaCost: 0,
					effect: eb => eb.WithDamage(1).WithTarget(Single().PlayersOrCreatures()),
					costs: c => c.RemovePlusOneCounter(),
					maxPerTurn: 0
				)
				.WithActivatedAbility(
					"Graft",
					manaCost: 4,
					effect: eb => eb.WithSelfCounters(1),
					maxPerTurn: 0
				)
				.Build(),
			// Arcbound Ravager. Unlimited activations by design — this is the card CLAUDE.md names
			// as the reason MaxActivationsPerTurn = 0 exists at all. It cannot loop: each activation
			// eats an artifact, so the board is the bound.
			//
			// Its sacrifice Filter is an Artifact spec, which is the ONE demand in this package the
			// harvester can answer — see the class comment.
			Modular(
					CardFactory
						.Creature("Scrapyard Ravager", manaCost: 2, power: 1, toughness: 1)
						.WithSubtype(Artifact)
						.WithActivatedAbility(
							"Devour",
							manaCost: 0,
							effect: eb => eb.WithSelfCounters(1),
							costs: c => c.SacrificeSubtype(Artifact),
							maxPerTurn: 0
						)
				)
				.Build(),
			// Arcbound Worker. A 0/0 that is only a card because of the counter it enters with, and
			// only worth a slot because the Rite makes that counter two.
			Modular(
					CardFactory
						.Creature("Scrapyard Servitor", manaCost: 1, power: 0, toughness: 0)
						.WithSubtype(Artifact)
				)
				.Build(),
			// **The observable zero.** "Creatures you control with a +1/+1 counter on them get
			// +1/+0" — a genuine counters payoff whose filter the harvester CAN read but cannot
			// ANSWER, because no card in the fixture has a counter on it. Included so the prediction
			// in the class comment is measurable rather than argued.
			CardFactory
				.Creature("Ironscale Marshal", manaCost: 3, power: 2, toughness: 3)
				.WithComponent(
					new StaticPTBoostAbility
					{
						PowerBonus = 1,
						ToughnessBonus = 0,
						Filter = new HasPermanentPowerBonusSpecification().And(
							new IsControlledByYouSpecification()
						),
					}
				)
				.Build(),
		];
}
