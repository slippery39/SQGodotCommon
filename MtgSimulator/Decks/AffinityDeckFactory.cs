using MtgCore;

namespace MtgSimulator;

/// <summary>
/// Builds a simplified Affinity deck for a given player.
///
/// Affinity uses artifacts to generate tempo through cost reduction and drains the
/// opponent via Disciple of the Vault when artifacts are sacrificed to Arcbound Ravager.
/// Seat of the Synod acts as an artifact land proxy — each one played creates a Clue
/// token (a non-creature artifact), which counts toward affinity cost reduction.
///
/// Deck list (60 cards):
///   20x Plains               (basic land)
///    4x Seat of the Synod   (land: creates a Clue artifact token when played)
///    4x Disciple of the Vault (1B 1/1: whenever an artifact is sacrificed, opponent loses 1 life)
///    4x Arcbound Ravager     (2 1/1: sacrifice any permanent → +1/+1; unlimited activations)
///    4x Atog                 (2 1/2: sacrifice an artifact → +2/+2 until end of turn; unlimited activations)
///    4x Cranial Plating      (2 artifact equipment: equipped creature gets +X/+0, X = artifacts you control; equip {1})
///    4x Frogmite             (4 2/2: affinity — costs 1 less per artifact you control)
///    4x Myr Enforcer         (7 4/4: affinity — costs 1 less per artifact you control)
///    4x Thoughtcast          (5: draw 2; affinity — costs 1 less per artifact you control)
///    4x Mox                  (0 artifact: add 1 mana)
///    4x Sol Ring             (1 artifact: add 2 mana)
///    4x Exploration          (2 artifact: draw 1 card, play an additional land this turn)
///    4x Lightning Bolt       (1: deal 3 damage to any target)
/// </summary>
public static class AffinityDeckFactory
{
	public static IReadOnlyList<Card> Build(int ownerId)
	{
		var deck = new List<Card>();
		AddCopies(deck, ownerId, 14, CardLibrary.VaultOfIngenuity);
		AddCopies(deck, ownerId, 4, CardLibrary.DiscipleOfTheVault);
		AddCopies(deck, ownerId, 4, CardLibrary.ArcboundRavager);
		AddCopies(deck, ownerId, 4, CardLibrary.Atog);
		AddCopies(deck, ownerId, 4, CardLibrary.CranialPlating);
		AddCopies(deck, ownerId, 4, () => CardLibrary.GetByName("Thought Monitor"));
		AddCopies(deck, ownerId, 4, CardLibrary.Frogmite);
		AddCopies(deck, ownerId, 4, CardLibrary.MyrEnforcer);
		AddCopies(deck, ownerId, 4, CardLibrary.Thoughtcast);
		AddCopies(deck, ownerId, 4, CardLibrary.Mox);
		AddCopies(deck, ownerId, 4, CardLibrary.SolRing);
		AddCopies(deck, ownerId, 4, CardLibrary.AncestralRecall);
		AddCopies(deck, ownerId, 4, CardLibrary.LightningBolt);
		return deck;
	}

	private static void AddCopies(List<Card> deck, int ownerId, int count, Func<Card> template)
	{
		for (var i = 0; i < count; i++)
			deck.Add(template() with { OwnerId = ownerId, ControllerId = ownerId });
	}
}
