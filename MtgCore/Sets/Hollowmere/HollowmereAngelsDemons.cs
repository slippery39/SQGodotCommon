using System.Collections.Immutable;
using ImmutableGameObjects;
using MtgCore.Cards.Builders;
using static MtgCore.Cards.Builders.TargetBuilder;

namespace MtgCore;

/// <summary>
/// Theme 5 — Angels and Demons. The set's top end, and the reanimation package's payoff.
///
/// Every card here obeys the 6-8 rate bar: it must effectively end the game on resolution.
/// A body alone is only a clock in a no-blocker combat model, and these are also the cards
/// most often cheated into play, so their value is front-loaded into the ETB. Nothing here
/// pays off "next upkeep" — a recurring drawback or a delayed reward would make them dead.
///
/// Angels reward Humans; Demons feed on discard and sacrifice.
///
/// Batch 1 contribution: 4 cards.
/// </summary>
public static class HollowmereAngelsDemons
{
	public static IReadOnlyList<Card> Cards { get; } =
		[
			// Lethal on resolution in a Human deck: the team grows and gains evasion at once.
			CardFactory
				.Creature("Archangel of Vigils", manaCost: 7, power: 6, toughness: 6)
				.WithSubtype(Hollowmere.Angel)
				.WithFlying()
				.WithLifelink()
				.WithEtbTrigger(
					"Vigil Unbroken",
					eb =>
						eb.WithAction(
								new AddModifierAction
								{
									PowerBonus = 2,
									ToughnessBonus = 2,
									Duration = ModifierDuration.Permanent,
								},
								TargetingStrategy.AllValid(
									new IsSubtypeSpecification { Subtype = Hollowmere.Human }.And(
										TargetSpecification.CreatureControlledByYou()
									)
								)
							)
							.WithAction(
								new GrantKeywordAction { GrantsFlying = true },
								AllValid().AllYourCreatures()
							)
				)
				.Build(),
			// Lifelink is what stops this being a worse Griselbrand — it refuels the life it
			// spends, so it draws into the winning turn instead of just being a big flier.
			new()
			{
				Name = "Abyssal Tyrant",
				ManaCost = 8,
				Subtypes = ImmutableHashSet.Create(
					StringComparer.OrdinalIgnoreCase,
					Hollowmere.Demon
				),
				Components = ImmutableArray.Create<GameComponent>(
					new PermanentComponent(),
					new CreatureComponent
					{
						Power = 7,
						Toughness = 7,
						HasFlying = true,
						HasLifelink = true,
					},
					new ActivatedAbilityComponent
					{
						Name = "Bargain",
						ManaCost = 0,
						MaxActivationsPerTurn = 0, // unlimited — the life total is the limiter
						AdditionalCosts = ImmutableList.Create<AdditionalCost>(
							new LifeAdditionalCost { Amount = 7 }
						),
						Effect = new CardEffect
						{
							TargetingStrategy = TargetingStrategy.Self(),
							ActionTemplate = new DrawCardsAction { Amount = 7 },
						},
					}
				),
			},
			// Two-for-one on resolution. Replaces a version whose upkeep sacrifice was pure
			// downside: with no blockers there is nothing to convert the sacrifice into.
			CardFactory
				.Creature("Hollow Vow Tyrant", manaCost: 6, power: 6, toughness: 6)
				.WithSubtype(Hollowmere.Demon)
				.WithFlying()
				.WithDeathtouch()
				.WithEtbTrigger(
					"Collect the Vow",
					eb =>
						eb.WithAction(
								new DestroyCreatureAction(),
								TargetingStrategy.SingleTarget(
									TargetSpecification.OpponentCreatures()
								)
							)
							.WithAction(
								new DiscardRandomCardAction { TargetOpponent = true },
								TargetingStrategy.NoTarget()
							)
				)
				.Build(),
			// The cheap Angel that bridges to the Human deck — a reanimation target you are
			// also happy to hard-cast.
			CardFactory
				.Creature("Chapel Seraph", manaCost: 5, power: 4, toughness: 4)
				.WithSubtype(Hollowmere.Angel)
				.WithFlying()
				.WithEtbTrigger(
					"Sanctify",
					eb => eb.WithCreateTokens(HollowmereTokens.Human(), count: 2)
				)
				.Build(),
		];
}
