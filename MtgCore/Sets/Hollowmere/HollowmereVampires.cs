using MtgCore.Cards.Builders;
using static MtgCore.Cards.Builders.TargetBuilder;

namespace MtgCore;

/// <summary>
/// Theme 10 — Vampire Tribal. Lifedrain and sacrifice; the tribe that profits from creatures
/// dying, which makes it the natural home for the set's deathtouch.
///
/// Deathtouch is concentrated here and kept rare — with no blockers it turns every attack
/// into a favourable trade, so it goes on small bodies where the trade is the whole point.
///
/// Batch 1 contribution: 3 cards.
/// </summary>
public static class HollowmereVampires
{
	public static IReadOnlyList<Card> Cards { get; } =
		[
			// The drain engine. Every creature that dies — including the opponent's, and
			// including tokens — is reach, which is what makes the go-wide themes fear it.
			CardFactory
				.Creature("Blood Artist", manaCost: 2, power: 0, toughness: 1)
				.WithSubtype(Hollowmere.Vampire)
				.WithTriggeredAbility(
					"Toast the Fallen",
					TriggerConditions.OnAnyCreatureDies(),
					eb =>
						eb.WithAction(
							new DrainLifeAction
							{
								Amount = 1,
								TargetOpponent = true,
								PlayerIdContextKey = ContextKeys.CastingPlayerId,
							},
							TargetingStrategy.NoTarget()
						)
				)
				.Build(),
			// A repeatable token maker on an evasive body — the tribe's mana sink and its
			// best reanimation target below the Angels.
			CardFactory
				.Creature("Bloodline Keeper", manaCost: 4, power: 3, toughness: 3)
				.WithSubtype(Hollowmere.Vampire)
				.WithFlying()
				.WithActivatedAbility(
					"Convene the Bloodline",
					manaCost: 1,
					effect: eb => eb.WithCreateTokens(HollowmereTokens.Vampire())
				)
				.Build(),
			// Deathtouch plus lifelink plus flying: attacks profitably into anything, which
			// is the ceiling for a three-drop and the reason there are so few of these.
			CardFactory
				.Creature("Nighthawk Penitent", manaCost: 3, power: 2, toughness: 3)
				.WithSubtype(Hollowmere.Vampire)
				.WithFlying()
				.WithLifelink()
				.WithDeathtouch()
				.Build(),
		];
}
