using System.Collections.Immutable;
using ImmutableGameObjects;
using MtgCore.Cards.Builders;
using static MtgCore.Cards.Builders.TargetBuilder;

namespace MtgCore;

/// <summary>
/// Blue enchantments and planeswalkers from the Core Set Cube — 6 enchantments (4 Auras) and
/// 4 planeswalkers. https://cubecobra.com/cube/list/magiccoreset20xx
///
/// The lockdown Auras (Claustrophobia, Capture Sphere) combine two mechanics built earlier: the
/// aura attachment rails from white, and the persistent freeze from this pass. An enchanted
/// creature is exhausted on entry and held down for as long as the Aura is attached — killing the
/// Aura frees it, which is what keeps the effect answerable.
///
/// DIVERGENCES, each also commented on its card:
///   - Teferi's phasing and instant-speed loyalty are cut: there is no phasing, and instant-speed
///     activation is the same priority gap counterspells route around.
///   - Emblems that grant lands abilities (Mu Yanling's ultimate) cannot work — lands are
///     consumed into MaxMana rather than existing as permanents.
/// </summary>
public static class CoresetCubeBluePermanents
{
	public static IReadOnlyList<Card> Cards { get; } =
		[
			// ===== ENCHANTMENTS =====

			CardFactory
				.Enchantment("Sensory Deprivation", manaCost: 1)
				.AsAura(
					powerBonus: -3,
					toughnessBonus: 0,
					targeting: TargetingStrategy.SingleTarget(
						TargetSpecification.OpponentCreatures()
					)
				)
				.Build(),
			// "Can't be blocked" is blank with no blocking, so the +1/+0 is the whole card.
			CardFactory
				.Enchantment("Aether Tunnel", manaCost: 1)
				.AsAura(
					powerBonus: 1,
					toughnessBonus: 3,
					targeting: TargetingStrategy.SingleTarget(
						TargetSpecification.CreatureControlledByYou()
					)
				)
				.Build(),
			// Tap on entry, then hold it down for as long as the Aura is attached — a
			// source-linked freeze, so destroying the Aura releases the creature.
			CardFactory
				.Enchantment("Claustrophobia", manaCost: 3)
				.AsAura(
					targeting: TargetingStrategy.SingleTarget(
						TargetSpecification.OpponentCreatures()
					)
				)
				.WithEtbTrigger(
					"Confine",
					eb =>
						eb.WithFreeze(1, whileSourceRemains: true)
							.WithTarget(AllValid().OpponentCreatures())
				)
				.Build(),
			CardFactory
				.Enchantment("Teferi's Tutelage", manaCost: 3)
				.WithEtbTrigger(
					"Study",
					eb => eb.WithDraw(1).WithTarget(TargetingStrategy.Self()).WithDiscard(1)
				)
				.WithTriggeredAbility(
					"Tutelage",
					TriggerConditions.OnYouDraw(),
					eb => eb.WithMill(2).WithTarget(Single().Opponent())
				)
				.Build(),
			// Flash is cut (no priority window); otherwise identical to Claustrophobia.
			CardFactory
				.Enchantment("Capture Sphere", manaCost: 4)
				.AsAura(
					targeting: TargetingStrategy.SingleTarget(
						TargetSpecification.OpponentCreatures()
					)
				)
				.WithEtbTrigger(
					"Ensnare",
					eb =>
						eb.WithFreeze(1, whileSourceRemains: true)
							.WithTarget(AllValid().OpponentCreatures())
				)
				.Build(),
			// "Draw a card for each LAND put into their graveyard" cannot be counted — lands are
			// consumed into MaxMana and never reach a graveyard. Flat one card per upkeep instead.
			CardFactory
				.Enchantment("Patient Rebuilding", manaCost: 5)
				.WithTriggeredAbility(
					"Rebuild",
					TriggerConditions.OnYourUpkeep(),
					eb =>
						eb.WithMill(3)
							.WithTarget(Single().Opponent())
							.WithDraw(1)
							.WithTarget(TargetingStrategy.Self())
				)
				.Build(),
			// ===== PLANESWALKERS =====

			CardFactory
				.Planeswalker("Jace Beleren", manaCost: 3)
				.WithLoyalty(3)
				.WithLoyaltyAbility(
					"+2: Each player draws a card",
					2,
					eb => eb.WithDraw(1).WithTarget(AllValid().Players())
				)
				.WithLoyaltyAbility(
					"-1: Target player draws a card",
					-1,
					eb => eb.WithDraw(1).WithTarget(TargetingStrategy.Self())
				)
				.WithLoyaltyAbility(
					"-10: Target player mills twenty cards",
					-10,
					eb => eb.WithMill(20).WithTarget(Single().Opponent())
				)
				.Build(),
			CardFactory
				.Planeswalker("Mu Yanling, Sky Dancer", manaCost: 3)
				.WithLoyalty(3)
				// "Loses flying" needs keyword REMOVAL, which no grant can express — grants only
				// ever OR keywords on. The -2/-0 half is kept.
				.WithLoyaltyAbility(
					"+2: Up to one target creature gets -2/-0",
					2,
					eb => eb.WithWeaken(2, 0)
				)
				.WithLoyaltyAbility(
					"-3: Create a 4/4 blue Elemental Bird with flying",
					-3,
					eb => eb.WithCreateTokens(CoresetCubeBlueTokens.ElementalBird())
				)
				// The printed emblem gives Islands a draw ability. Lands are not permanents here,
				// so the emblem draws directly instead.
				.WithLoyaltyAbility(
					"-8: You get an emblem that draws you a card each turn",
					-8,
					eb =>
						eb.WithAction(
							new GrantEmblemAction
							{
								Emblem = new Emblem
								{
									Name = "Yanling's Insight",
									Condition = TriggerConditions.OnYourUpkeep(),
									Effect = new CardEffect
									{
										TargetingStrategy = TargetingStrategy.Self(),
										ActionTemplate = new DrawCardsAction { Amount = 1 },
									},
								},
							},
							TargetingStrategy.Self()
						)
				)
				.Build(),
			// Instant-speed loyalty and phasing are both cut. The -3 becomes a freeze, which is
			// the closest thing to "treat it as though it doesn't exist until your next turn".
			CardFactory
				.Planeswalker("Teferi, Master of Time", manaCost: 4)
				.WithLoyalty(3)
				.WithLoyaltyAbility(
					"+1: Draw a card, then discard a card",
					1,
					eb => eb.WithDraw(1).WithTarget(TargetingStrategy.Self()).WithDiscard(1)
				)
				.WithLoyaltyAbility(
					"-3: Freeze target creature you don't control",
					-3,
					eb => eb.WithFreeze(1)
				)
				.WithLoyaltyAbility(
					"-10: Take two extra turns after this one",
					-10,
					eb => eb.WithExtraTurn(2)
				)
				.Build(),
			CardFactory
				.Planeswalker("Tezzeret, Artifice Master", manaCost: 5)
				.WithLoyalty(5)
				.WithLoyaltyAbility(
					"+1: Create a 1/1 Thopter with flying",
					1,
					eb => eb.WithCreateTokens(CoresetCubeBlueTokens.Thopter())
				)
				.WithLoyaltyAbility(
					"0: Draw a card",
					0,
					eb => eb.WithDraw(1).WithTarget(TargetingStrategy.Self())
				)
				.WithLoyaltyAbility(
					"-9: You get an emblem that puts a permanent onto the battlefield each turn",
					-9,
					eb =>
						eb.WithAction(
							new GrantEmblemAction
							{
								Emblem = new Emblem
								{
									Name = "Tezzeret's Arsenal",
									Condition = TriggerConditions.OnYourUpkeep(),
									Effect = new CardEffect
									{
										TargetingStrategy = TargetingStrategy.NoTarget(),
										ActionTemplate = new CreateCardAction
										{
											CardTemplate = CoresetCubeBlueTokens.Thopter(),
											Count = 2,
										},
									},
								},
							},
							TargetingStrategy.Self()
						)
				)
				.Build(),
		];
}
