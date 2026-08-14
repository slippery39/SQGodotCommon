using MtgCore.Cards.Builders;
using static MtgCore.Cards.Builders.TargetBuilder;

namespace MtgCore;

/// <summary>
/// Format glue â€” cards that belong to no theme but that the format needs to function:
/// removal, and an answer to the graveyard theme. Gothic-horror flavoured, so they still
/// read as part of the set.
///
/// Removal is at a premium in this combat model. With no blockers you cannot trade by
/// blocking, so a resolved threat stays a threat until a card answers it â€” which is why
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
			// Exile removal â€” the only clean answer to the set's many recursion threats.
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
			// A cheap Taunt body â€” the format's only way to buy a turn against an attack.
			CardFactory
				.Creature("Chapel Ward", manaCost: 2, power: 1, toughness: 4)
				.WithSubtype(Hollowmere.Spirit)
				.WithTaunt()
				.Build(),
			// The bigger wall, for the decks that need to reach their top end. Reach matters
			// now that Flying keeps a creature off the ground entirely.
			CardFactory
				.Creature("Hollowmere Bulwark", manaCost: 4, power: 2, toughness: 6)
				.WithSubtype(Hollowmere.Horror)
				.WithTaunt()
				.WithReach()
				.Build(),
			// ===== REMOVAL PASS =====
			// Scales with the target instead of being a flat destroy, and doubles as a trick.
			CardFactory.Spell("Wither the Vein", manaCost: 2).WithWeaken(3, 3).Build(),
			// Cheap interaction that answers a big threat for one mana at the right moment.
			CardFactory
				.Spell("Grasping Silt", manaCost: 1)
				.WithWeaken(2, 2)
				.WithFlashback(3)
				.Build(),
			// Answers Hexproof and Shroud, which nothing else in the set can touch, and it
			// takes their best creature rather than their worst.
			CardFactory.Spell("Toll of the Mere", manaCost: 3).WithEdict().WithDraw(1).Build(),
			// Bounce answers what destroy cannot: a recursive threat comes back as a card to
			// re-cast rather than as fuel in the graveyard.
			CardFactory.Spell("Drag to the Depths", manaCost: 2).WithBounce().WithMill(2).Build(),
			// Removal that costs no card but risks the fighter â€” and reaches flyers a ground
			// creature could never attack.
			CardFactory
				.Spell("Set Upon the Pack", manaCost: 2)
				.WithFight()
				.WithFlashback(4)
				.Build(),
			// A creature that is itself removal, so the aggressive decks have interaction too.
			CardFactory
				.Creature("Mere-Bank Hunter", manaCost: 4, power: 3, toughness: 3)
				.WithSubtype(Hollowmere.Human)
				.WithEtbTrigger("Run It Down", eb => eb.WithAutoFight())
				.Build(),
			// Reach on a body plus a shrink â€” the answer to the Flying decks.
			CardFactory
				.Creature("Chapel Longbowman", manaCost: 3, power: 2, toughness: 3)
				.WithSubtype(Hollowmere.Human)
				.WithSubtype(Hollowmere.Soldier)
				.WithReach()
				.WithEtbTrigger("Loose an Arrow", eb => eb.WithAutoWeaken(2, 2))
				.Build(),
			// Finds the answer or the payoff, which is what stops a synergy deck flooding.
			CardFactory.Spell("Search the Parish", manaCost: 2).WithDig(4).Build(),
		];
}
