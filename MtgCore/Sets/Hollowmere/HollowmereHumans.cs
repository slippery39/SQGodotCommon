using MtgCore.Cards.Builders;
using static MtgCore.Cards.Builders.TargetBuilder;

namespace MtgCore;

/// <summary>
/// Theme 3 — Human Tribal. The set's aggressive backbone, and the tribe Angels reward.
/// Humans are also the most common creature type on cards belonging to other themes, so a
/// Human deck picks up density from Spells and Werewolves without trying.
///
/// Batch 1 contribution: 5 cards.
/// </summary>
public static class HollowmereHumans
{
	public static IReadOnlyList<Card> Cards { get; } =
		[
			// The one-drop that rewards density. Grows on every subsequent Human.
			CardFactory
				.Creature("Champion of the Parish", manaCost: 1, power: 1, toughness: 1)
				.WithSubtype(Hollowmere.Human)
				.WithSubtype(Hollowmere.Soldier)
				.WithTriggeredAbility(
					"Rally the Parish",
					new EventTriggerCondition
					{
						EventTypeName = EventTypeNames.CreatureEnteredBattlefield,
						Filter = new IsSubtypeSpecification { Subtype = Hollowmere.Human }
							.And(new IsControlledByYouSpecification())
							.And(new IsNotSelfSpecification()),
					},
					eb =>
						eb.WithAction(
							new AddModifierAction
							{
								PowerBonus = 1,
								ToughnessBonus = 1,
								Duration = ModifierDuration.Permanent,
								TargetContextKey = ContextKeys.SourceCardId,
							},
							TargetingStrategy.NoTarget()
						)
				)
				.Build(),
			// The lord. Anthem effects are strong with no blockers, so the body stays small.
			CardFactory
				.Creature("Thraben Marshal", manaCost: 3, power: 2, toughness: 2)
				.WithSubtype(Hollowmere.Human)
				.WithSubtype(Hollowmere.Soldier)
				.WithComponent(
					new StaticPTBoostAbility
					{
						PowerBonus = 1,
						ToughnessBonus = 1,
						Filter = new IsSubtypeSpecification { Subtype = Hollowmere.Human }.And(
							TargetSpecification.OtherCreaturesYouControl()
						),
					}
				)
				.Build(),
			// Two bodies for two mana — the density enabler every tribal deck needs.
			CardFactory
				.Spell("Gather the Townsfolk", manaCost: 2)
				.WithCreateTokens(HollowmereTokens.Human(), count: 2)
				.Build(),
			// Bridges Humans into the graveyard theme: a Human that likes a full graveyard.
			CardFactory
				.Creature("Parish Gravedigger", manaCost: 3, power: 2, toughness: 3)
				.WithSubtype(Hollowmere.Human)
				.WithEtbTrigger("Unearth a Kinsman", eb => eb.WithReturnCreatureFromGraveyard())
				.Build(),
			// Payoff at the top of the Human curve. Immediate board impact, per the rate bar.
			CardFactory
				.Creature("Hollowmere Militia Captain", manaCost: 5, power: 3, toughness: 3)
				.WithSubtype(Hollowmere.Human)
				.WithSubtype(Hollowmere.Soldier)
				.WithEtbTrigger(
					"Muster the Watch",
					eb => eb.WithCreateTokens(HollowmereTokens.Human(), count: 3)
				)
				.Build(),
		];
}
