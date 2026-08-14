using MtgCore.Cards.Builders;
using static MtgCore.Cards.Builders.TargetBuilder;

namespace MtgCore;

/// <summary>
/// Format glue — cards that belong to no theme but that the format needs to function:
/// removal, and an answer to the graveyard theme. Gothic-horror flavoured, so they still
/// read as part of the set.
///
/// Removal is at a premium in this combat model. With no blockers you cannot trade by
/// blocking, so a resolved threat stays a threat until a card answers it — which is why
/// the glue slot leads with removal rather than card draw.
///
/// Batch 1 contribution: 2 cards.
/// </summary>
public static class HollowmereGlue
{
	public static IReadOnlyList<Card> Cards { get; } =
		[
			// Clean unconditional removal at the format's baseline rate.
			CardFactory
				.Spell("Sever the Bloodline", manaCost: 2)
				.WithDestroy()
				.WithTarget(Single().OpponentCreatures())
				.Build(),
			// The graveyard hate. A maindeckable answer rather than a sideboard card, since
			// nearly every deck in this format uses its graveyard as a resource.
			CardFactory
				.Spell("Consecrate the Mere", manaCost: 1)
				.WithExileFromGraveyard()
				.WithDraw(1)
				.Build(),
		];
}
