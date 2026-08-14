using MtgCore.Cards.Builders;
using static MtgCore.Cards.Builders.TargetBuilder;

namespace MtgCore;

/// <summary>
/// Theme 6 — Go-Wide Spirits. Token flood plus pump.
///
/// Rates are deliberately below their paper equivalents. With no blockers every token
/// connects with the opponent's face, so a squad of 1/1 fliers is closer to a squad of
/// unblockable creatures — and an anthem multiplies that across the whole board. Spirits
/// overlap heavily into Spells (cast triggers make them) and Graveyard (flashback makes
/// them twice).
///
/// Batch 1 contribution: 4 cards.
/// </summary>
public static class HollowmereSpirits
{
	public static IReadOnlyList<Card> Cards { get; } =
		[
			// Four evasive bodies across two casts — the archetype's defining card.
			CardFactory
				.Spell("Lingering Souls", manaCost: 3)
				.WithCreateTokens(HollowmereTokens.Spirit(), count: 2)
				.WithFlashback(4)
				.Build(),
			// The lord. Hexproof protects the whole board from the format's removal, which
			// is why the body is a bare 2/2 for three.
			CardFactory
				.Creature("Drogskol Captain", manaCost: 3, power: 2, toughness: 2)
				.WithSubtype(Hollowmere.Spirit)
				.WithFlying()
				.WithComponent(
					new StaticPTBoostAbility
					{
						PowerBonus = 1,
						ToughnessBonus = 1,
						Filter = new IsSubtypeSpecification { Subtype = Hollowmere.Spirit }.And(
							TargetSpecification.OtherCreaturesYouControl()
						),
					}
				)
				.WithComponent(
					new StaticGrantKeywordAbility
					{
						GrantsHexproof = true,
						Filter = new IsSubtypeSpecification { Subtype = Hollowmere.Spirit }.And(
							TargetSpecification.OtherCreaturesYouControl()
						),
					}
				)
				.Build(),
			// A repeatable token engine that doubles as the deck's mana sink.
			CardFactory
				.Creature("Chapel Bellringer", manaCost: 4, power: 2, toughness: 4)
				.WithSubtype(Hollowmere.Spirit)
				.WithTaunt()
				.WithActivatedAbility(
					"Toll the Bell",
					manaCost: 2,
					effect: eb => eb.WithCreateTokens(HollowmereTokens.Spirit())
				)
				.Build(),
			// The payoff: a one-shot team pump that ends a stalled board. Priced at five
			// because go-wide plus a global pump is close to lethal on its own.
			CardFactory
				.Spell("Vigil of the Drowned", manaCost: 5)
				.WithAction(
					new AddModifierAction
					{
						PowerBonus = 2,
						ToughnessBonus = 1,
						Duration = ModifierDuration.UntilEndOfTurn,
					},
					AllValid().AllYourCreatures()
				)
				.WithCreateTokens(HollowmereTokens.Spirit(), count: 2)
				.Build(),
		];
}
