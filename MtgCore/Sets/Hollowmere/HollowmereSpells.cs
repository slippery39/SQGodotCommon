using MtgCore.Cards.Builders;
using static MtgCore.Cards.Builders.TargetBuilder;

namespace MtgCore;

/// <summary>
/// Theme 7 — Spells, and spells in the graveyard. Cantrips, prowess, and cast triggers.
///
/// The bridge theme: cheap spells fill the graveyard for theme 1, make Spirits for theme 6,
/// and turn on threshold. Prowess needs no engine support — it is a SpellCast trigger that
/// adds an until-end-of-turn modifier to its own source.
///
/// Batch 1 contribution: 5 cards.
/// </summary>
public static class HollowmereSpells
{
	public static IReadOnlyList<Card> Cards { get; } =
		[
			// The format's smoothing cantrip, and a graveyard enabler in the same slot.
			CardFactory.Spell("Ponder", manaCost: 1).WithDraw(2).WithMill(1).Build(),
			// Young Pyromancer as a Spirit-maker: one card that serves themes 6 and 7 at once.
			CardFactory
				.Creature("Chapel Conjurer", manaCost: 2, power: 2, toughness: 1)
				.WithSubtype(Hollowmere.Human)
				.WithSubtype(Hollowmere.Wizard)
				.WithTriggeredAbility(
					"Conjure the Choir",
					new EventTriggerCondition
					{
						EventTypeName = EventTypeNames.SpellCast,
						Filter = new IsControlledByYouSpecification(),
					},
					eb => eb.WithCreateTokens(HollowmereTokens.Spirit())
				)
				.Build(),
			// Prowess. Free to model — a SpellCast trigger buffing its own source.
			CardFactory
				.Creature("Zealous Penitent", manaCost: 1, power: 1, toughness: 2)
				.WithSubtype(Hollowmere.Human)
				.WithSubtype(Hollowmere.Cleric)
				.WithHaste()
				.WithTriggeredAbility(
					"Prowess",
					new EventTriggerCondition
					{
						EventTypeName = EventTypeNames.SpellCast,
						Filter = new IsControlledByYouSpecification(),
					},
					eb =>
						eb.WithAction(
							new AddModifierAction
							{
								PowerBonus = 1,
								ToughnessBonus = 1,
								Duration = ModifierDuration.UntilEndOfTurn,
								TargetContextKey = ContextKeys.SourceCardId,
							},
							TargetingStrategy.NoTarget()
						)
				)
				.Build(),
			// Snapcaster-tier value: a body that rebuys the best spell in your graveyard.
			CardFactory
				.Creature("Mere-Watch Adept", manaCost: 2, power: 2, toughness: 1)
				.WithSubtype(Hollowmere.Human)
				.WithSubtype(Hollowmere.Wizard)
				.WithEtbTrigger("Echo of the Mere", eb => eb.WithReturnSpellFromGraveyard())
				.Build(),
			// The graveyard-spells payoff. Rebuys a spell now and again later.
			CardFactory
				.Spell("Past in Ashes", manaCost: 4)
				.WithGiveFlashback()
				.WithDraw(1)
				.WithFlashback(6)
				.Build(),
		];
}
