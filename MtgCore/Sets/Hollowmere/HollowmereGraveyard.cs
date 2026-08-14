using System.Collections.Immutable;
using ImmutableGameObjects;
using MtgCore.Cards.Builders;
using static MtgCore.Cards.Builders.TargetBuilder;

namespace MtgCore;

/// <summary>
/// Theme 1 — Graveyard Matters. The set's spine: threshold, flashback, reanimation, and
/// creatures that scale off graveyard size. Most other themes overlap into this one.
///
/// Batch 1 contribution: 10 cards.
/// </summary>
public static class HollowmereGraveyard
{
	public static IReadOnlyList<Card> Cards { get; } =
		[
			// Mills and replaces itself, so the mill is upside rather than the whole card.
			CardFactory
				.Creature("Grave Scholar", manaCost: 2, power: 3, toughness: 2)
				.WithSubtype(Hollowmere.Human)
				.WithSubtype(Hollowmere.Wizard)
				.WithEtbTrigger(
					"Dredge the Shallows",
					eb =>
						eb.WithAction(
							new PipelineAction
							{
								Steps = ImmutableList.Create<GameAction>(
									new MillAction
									{
										Amount = 3,
										TargetContextKey = ContextKeys.CastingPlayerId,
									},
									new SelectCardFromZoneAction
									{
										Zone = ZoneType.Graveyard,
										Filter =
											new IsInstantOrSorceryInOwnGraveyardSpecification(),
										PlayerIdContextKey = ContextKeys.CastingPlayerId,
										OutputKey = "scholar_target",
									},
									new MoveCardToHandAction
									{
										CardIdContextKey = "scholar_target",
										PlayerIdContextKey = ContextKeys.CastingPlayerId,
									}
								),
							},
							TargetingStrategy.NoTarget()
						)
				)
				.Build(),
			// Tarmogoyf's rate is the whole appeal — at a higher cost this is a worse vanilla.
			new()
			{
				Name = "Splinterbone Horror",
				ManaCost = 2,
				Subtypes = ImmutableHashSet.Create(
					StringComparer.OrdinalIgnoreCase,
					Hollowmere.Zombie,
					Hollowmere.Horror
				),
				Components = ImmutableArray.Create<GameComponent>(
					new PermanentComponent(),
					new CreatureComponent { Power = 0, Toughness = 1 },
					new GraveyardCountComponent { Duration = ModifierDuration.Permanent }
				),
			},
			// The threshold payoff at common rate: fine early, a real threat once online.
			CardFactory
				.Creature("Nightfall Reveler", manaCost: 3, power: 2, toughness: 2)
				.WithSubtype(Hollowmere.Human)
				.WithThreshold(power: 3, toughness: 3, flying: true)
				.Build(),
			// The zone-dependent static. Flying is the strongest keyword here because it is
			// the only clean way past a Taunt blocker-substitute.
			new()
			{
				Name = "Wonder of the Drowned",
				ManaCost = 4,
				Subtypes = ImmutableHashSet.Create(
					StringComparer.OrdinalIgnoreCase,
					Hollowmere.Spirit
				),
				Components = ImmutableArray.Create<GameComponent>(
					new PermanentComponent(),
					new CreatureComponent { Power = 2, Toughness = 2 },
					new StaticGrantKeywordAbility
					{
						GrantsFlying = true,
						ActiveInZone = ZoneType.Graveyard,
						Filter = new IsOnBattlefieldSpecification()
							.And(new IsCreatureSpecification())
							.And(new IsControlledByYouSpecification()),
					}
				),
			},
			// Reanimation at a rate that lets it target the set's 6-8 drops.
			CardFactory
				.Spell("Ghoulcaller's Bargain", manaCost: 3)
				.WithReanimate()
				.WithFlashback(6)
				.Build(),
			// Cheap self-mill plus a body later. Two cards' worth of value in one slot.
			CardFactory.Spell("Grim Excavation", manaCost: 1).WithMill(4).WithFlashback(3).Build(),
			// Threshold on a defensive body. Taunt matters — without it a big butt does nothing.
			CardFactory
				.Creature("Cairn Warden", manaCost: 3, power: 1, toughness: 4)
				.WithSubtype(Hollowmere.Spirit)
				.WithTaunt()
				.WithThreshold(power: 4, toughness: 2)
				.Build(),
			// Recursion engine. Returning to hand rather than the battlefield keeps it fair.
			CardFactory
				.Creature("Sexton of the Drowned Chapel", manaCost: 4, power: 3, toughness: 3)
				.WithSubtype(Hollowmere.Human)
				.WithSubtype(Hollowmere.Cleric)
				.WithEtbTrigger("Exhume the Faithful", eb => eb.WithReturnCreatureFromGraveyard())
				.Build(),
			// Payoff for a full graveyard that also fills it. Draws two at threshold.
			CardFactory
				.Spell("Rites of the Sunken Choir", manaCost: 3)
				.WithMill(3)
				.WithDraw(2)
				.Build(),
			// A 1-drop that is never dead: mills early, becomes a threat late.
			CardFactory
				.Creature("Drowned Acolyte", manaCost: 1, power: 1, toughness: 2)
				.WithSubtype(Hollowmere.Human)
				.WithSubtype(Hollowmere.Cleric)
				.WithEtbTrigger("Wade In", eb => eb.WithMill(2))
				.WithThreshold(power: 2, toughness: 1)
				.Build(),
		];
}
