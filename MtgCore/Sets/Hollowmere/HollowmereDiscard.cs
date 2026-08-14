using System.Collections.Immutable;
using ImmutableGameObjects;
using MtgCore.Cards.Builders;
using static MtgCore.Cards.Builders.TargetBuilder;

namespace MtgCore;

/// <summary>
/// Theme 2 — Discard Bonuses. Outlets that turn dead cards into graveyard fuel, plus the
/// payoffs that make discarding a benefit.
///
/// Madness is modelled as "when you discard this, ⟨effect⟩" — a graveyard-active trigger on
/// CardDiscardedEvent — because casting a discarded card needs a priority window this engine
/// does not have. The effect therefore costs no mana, so these are priced as free effects,
/// not as a discount on the card's face cost.
///
/// Batch 1 contribution: 5 cards.
/// </summary>
public static class HollowmereDiscard
{
	public static IReadOnlyList<Card> Cards { get; } =
		[
			// The archetypal outlet. Cheap, refuels, and comes back once.
			CardFactory
				.Spell("Faithless Looting", manaCost: 1)
				.WithDraw(2)
				.WithAction(
					new DiscardCardsAction(),
					TargetingStrategy.SingleTarget(new IsInHandSpecification())
				)
				.WithFlashback(3)
				.Build(),
			// Madness. Free 3 damage when discarded, or a real removal spell when cast.
			CardFactory
				.Spell("Fiery Temper", manaCost: 2)
				.WithDamage(3)
				.WithTarget(Single().PlayersOrCreatures())
				.WithComponent(
					new TriggeredAbilityComponent
					{
						Name = "Madness",
						ActiveInZone = ZoneType.Graveyard,
						Condition = new EventTriggerCondition
						{
							EventTypeName = EventTypeNames.CardDiscarded,
							Filter = new IsSourceCardSpecification(),
						},
						Effect = new CardEffect
						{
							TargetingStrategy = TargetingStrategy.NoTarget(),
							ActionTemplate = new DrainLifeAction
							{
								Amount = 3,
								TargetOpponent = true,
								PlayerIdContextKey = ContextKeys.CastingPlayerId,
							},
						},
					}
				)
				.Build(),
			// Grows every time you loot. A 3-drop that must do two things, and does.
			CardFactory
				.Creature("Asylum Chronicler", manaCost: 3, power: 2, toughness: 3)
				.WithSubtype(Hollowmere.Human)
				.WithSubtype(Hollowmere.Wizard)
				.WithTriggeredAbility(
					"Record the Madness",
					new EventTriggerCondition
					{
						EventTypeName = EventTypeNames.CardDiscarded,
						Filter = new IsControlledByYouSpecification(),
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
			// Madness on a body: discarding it puts it straight onto the battlefield, so the
			// looting outlets in this theme are never truly discarding it away.
			CardFactory
				.Creature("Shrieking Penitent", manaCost: 4, power: 4, toughness: 3)
				.WithSubtype(Hollowmere.Horror)
				.WithComponent(
					new TriggeredAbilityComponent
					{
						Name = "Madness",
						ActiveInZone = ZoneType.Graveyard,
						Condition = new EventTriggerCondition
						{
							EventTypeName = EventTypeNames.CardDiscarded,
							Filter = new IsSourceCardSpecification(),
						},
						Effect = new CardEffect
						{
							TargetingStrategy = TargetingStrategy.NoTarget(),
							ActionTemplate = new PutIntoBattlefieldAction
							{
								CardIdContextKey = ContextKeys.SourceCardId,
							},
						},
					}
				)
				.Build(),
			// Rummaging that nets a card. The discard is the point, not the cost.
			CardFactory
				.Spell("Cathartic Reunion", manaCost: 1)
				.WithAction(
					new DiscardCardsAction(),
					TargetingStrategy.SingleTarget(new IsInHandSpecification())
				)
				.WithDraw(3)
				.Build(),
		];
}
