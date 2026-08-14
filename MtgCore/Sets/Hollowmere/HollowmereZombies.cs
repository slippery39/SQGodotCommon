using System.Collections.Immutable;
using ImmutableGameObjects;
using MtgCore.Cards.Builders;
using static MtgCore.Cards.Builders.TargetBuilder;

namespace MtgCore;

/// <summary>
/// Theme 8 — Zombie Tribal. The tribe that wants its own creatures in the graveyard, which
/// makes it the natural partner for Mill.
///
/// Batch 1 contribution: 4 cards.
/// </summary>
public static class HollowmereZombies
{
	public static IReadOnlyList<Card> Cards { get; } =
		[
			// Recursion, not raw stats: a 1-drop that keeps coming back is a mana sink and a
			// sacrifice engine. Not exiled on recursion, so mana is the only limiter.
			CardFactory
				.Creature("Gravecrawler", manaCost: 1, power: 2, toughness: 1)
				.WithSubtype(Hollowmere.Zombie)
				.WithGraveyardRecursion(manaCost: 2)
				.Build(),
			// The tribal lord.
			CardFactory
				.Creature("Cemetery Reaper", manaCost: 3, power: 2, toughness: 2)
				.WithSubtype(Hollowmere.Zombie)
				.WithComponent(
					new StaticPTBoostAbility
					{
						PowerBonus = 1,
						ToughnessBonus = 1,
						Filter = new IsSubtypeSpecification { Subtype = Hollowmere.Zombie }.And(
							TargetSpecification.OtherCreaturesYouControl()
						),
					}
				)
				.Build(),
			// Scales with Zombies in the GRAVEYARD, not on the battlefield — the payoff that
			// makes self-mill a Zombie deck's plan rather than a liability.
			new()
			{
				Name = "Diregraf Colossus",
				ManaCost = 3,
				Subtypes = ImmutableHashSet.Create(
					StringComparer.OrdinalIgnoreCase,
					Hollowmere.Zombie
				),
				Components = ImmutableArray.Create<GameComponent>(
					new PermanentComponent(),
					new CreatureComponent { Power = 2, Toughness = 2 },
					new TriggeredAbilityComponent
					{
						Name = "Swell the Ranks",
						Condition = TriggerConditions.OnSelfEntersBattlefield(),
						Effect = new CardEffect
						{
							TargetingStrategy = TargetingStrategy.NoTarget(),
							ActionTemplate = new PipelineAction
							{
								Steps = ImmutableList.Create<GameAction>(
									new CountCardsWithSubtypeAction
									{
										Subtype = Hollowmere.Zombie,
										Zone = ZoneType.Graveyard,
										OutputKey = "colossus_zombie_count",
										PlayerIdContextKey = ContextKeys.CastingPlayerId,
									},
									new CreateCardAction
									{
										CardTemplate = HollowmereTokens.Zombie(),
										CountInputKey = "colossus_zombie_count",
									}
								),
							},
						},
					}
				),
			},
			// Mills to find Zombies and leaves a body behind when it dies. Two-for-one on a
			// four-drop, which is the bar for a card that also enables the deck.
			CardFactory
				.Creature("Grimgrin Acolyte", manaCost: 4, power: 3, toughness: 3)
				.WithSubtype(Hollowmere.Zombie)
				.WithEtbTrigger("Rot Down", eb => eb.WithMill(3))
				.WithDeathTrigger(
					"Rise Again",
					eb => eb.WithCreateTokens(HollowmereTokens.Zombie())
				)
				.Build(),
		];
}
