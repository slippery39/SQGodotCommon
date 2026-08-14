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
/// Batches 1 and 6: 10 cards.
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
			// ===== BATCH 6 =====
			// A second copy of the format's baseline removal, so every deck can find one.
			CardFactory
				.Spell("Silt-Choked Grasp", manaCost: 2)
				.WithDestroy()
				.WithTarget(Single().OpponentCreatures())
				.Build(),
			// Exile removal — the only clean answer to the set's many recursion threats.
			CardFactory
				.Spell("Banish to the Mere", manaCost: 3)
				.WithExile()
				.WithTarget(Single().OpponentCreatures())
				.Build(),
			// Removal that replaces itself, for the decks that can afford to wait.
			CardFactory
				.Spell("Final Rites", manaCost: 5)
				.WithDestroy()
				.WithTarget(Single().OpponentCreatures())
				.WithDraw(2)
				.Build(),
			// Generic card advantage, so a deck without a theme payoff still has a plan.
			CardFactory.Spell("Read the Bones", manaCost: 3).WithDraw(3).WithLoseLife(2).Build(),
			// The cheapest possible smoothing, and a little life against the aggressive decks.
			CardFactory
				.Spell("Lantern of the Mere", manaCost: 1)
				.WithDraw(1)
				.WithLifeGain(2)
				.Build(),
			// A second graveyard-hate card, because every deck here uses its yard.
			CardFactory
				.Spell("Purge the Vaults", manaCost: 2)
				.WithExileFromGraveyard()
				.WithExileFromGraveyard()
				.WithDraw(1)
				.Build(),
			// A cheap Taunt body — the format's only way to buy a turn against an attack.
			CardFactory
				.Creature("Chapel Ward", manaCost: 2, power: 1, toughness: 4)
				.WithSubtype(Hollowmere.Spirit)
				.WithTaunt()
				.Build(),
			// The bigger wall, for the decks that need to reach their top end.
			CardFactory
				.Creature("Hollowmere Bulwark", manaCost: 4, power: 2, toughness: 6)
				.WithSubtype(Hollowmere.Horror)
				.WithTaunt()
				.WithReach()
				.Build(),
		];
}
