using ImmutableGameObjects;
using MagicCore.Ai;
using MagicCore.Sets.Fra;

namespace MagicCore.Tests.Ai;

/// <summary>
/// Bad plays reported from real games, rebuilt as positions the search AI must handle (AI-2.6). Each
/// test names who reported it and when. The real cards are used — the report is about them.
/// Each position has a CONTROL: the AI that made the bad play still makes it there, so a position
/// that stops provoking it (a setup change, a card change) fails loudly instead of passing for nothing.
/// </summary>
public class AiPositionTests
{
	GameState s = null!;
	int me;
	int them;

	[SetUp]
	public void OurFirstMain_EmptyHands()
	{
		s = TestGames.EmptyHands(TestGames.InFirstMain());
		me = s.GetGame().StartingPlayerId;
		them = s.OpponentOf(me);
	}

	// Shayne, 2026-10-09 (AI-1): the AI cast Prophesied End ("Destroy target creature. If it wasn't
	// attacking, its controller draws a card.") on a NON-attacking Rank Rat — premium removal on a
	// 1/1, and a card for the opponent. Rebuilt: two Plains and Prophesied End against their Rat.
	GameState RankRatPosition()
	{
		var state = s;
		for (var i = 0; i < 2; i++)
			(state, _) = TestGames.Put(state, me, TestGames.Card("Plains"), ZoneType.Battlefield);
		(state, _) = TestGames.Put(state, me, FraCards.Get("Prophesied End"), ZoneType.Hand);
		(state, _) = TestGames.Put(state, them, FraCards.Get("Rank Rat"), ZoneType.Battlefield);
		return state;
	}

	/// <summary>
	/// Plays our turn and theirs — Prophesied End is an instant, so waiting for their upkeep to kill
	/// the Rat is the same bad play — and says whether the Rat ever died while NOT attacking.
	/// </summary>
	bool KillsTheRatWhileItIsNotAttacking(IAi ai)
	{
		var bad = false;
		TestGames.PlayAis(
			RankRatPosition(),
			s.GetGame().TurnNumber + 1,
			ai,
			new GreedyAi(them),
			(before, after) =>
			{
				var rat = before
					.CardsIn(them, ZoneType.Battlefield)
					.FirstOrDefault(c => c.Name == "Rank Rat");
				var gone = !after
					.CardsIn(them, ZoneType.Battlefield)
					.Any(c => c.Name == "Rank Rat");
				bad |= rat is { AttackingId: 0 } && gone;
			}
		);
		return bad;
	}

	[Test]
	public void RankRat_Control_GreedyAiStillMakesTheBadPlay() =>
		Assert.That(KillsTheRatWhileItIsNotAttacking(new GreedyAi(me)), Is.True);

	[
		Test,
		Ignore(
			"AI-1: the search fires it too — a held card scores only Evaluator's flat hand value, and the rollout's opponent never casts a better target"
		)
	]
	public void RankRat_TheSearch_HoldsItsRemoval() =>
		Assert.That(KillsTheRatWhileItIsNotAttacking(new BeamSearchAi(me)), Is.False);
}
