using MagicCore.Actions;
using MagicCore.Ai;
using MagicCore.Cards;
using MagicCore.Tests.Sets;
using Nu = NUnit.Framework.Is;

namespace MagicCore.Tests.Ai;

/// <summary>
/// MC-9: the cube's cards in the AI's hands — each item watched first (the position below is the one watched), then
/// fixed. A realistic board, not the base board's twenty basics: six lands.
/// </summary>
public class CubeAiTests : FraBoardTest
{
	void SixLands()
	{
		EmptyBoard();
		s = TestGames.EmptyHands(s);
		foreach (
			var land in new[] { "Swamp", "Swamp", "Swamp", "Mountain", "Mountain", "Mountain" }
		)
			Field(TestGames.Card(land), me);
	}

	int TheirCreatures() =>
		s.CardsIn(them, ZoneType.Battlefield).Count(c => c.Is(CardType.Creature));

	// MC-9.1, watched 2026-11-23: past BeamSearchAi.MaxPicks target sets the search took GreedyAi's one target, so Fire
	// Covenant was X = 4 on one 4/4 of twelve creatures. Now a kill set: each target the damage that kills it.
	[Test]
	public void FireCovenant_GreedyDividesItsXAmongWhatItKills()
	{
		SixLands();
		var small = new[] { "2/2", "1/1", "2/1" }
			.Select(size => Field(TestGames.Creature($"Small {size}", size), them))
			.ToList();
		var big = Field(TestGames.Creature("Giant", "4/4"), them);
		var covenant = Hand(Lsv("Fire Covenant"), me);
		var greedy = new GreedyAi(me);

		s = s.Act(Casts(covenant).Single(c => c.X == 4));
		var asked = ResolveAsking(choice => greedy.Answer(s, choice));

		Assert.That(small.All(InGraveyard), Nu.True, "2 + 1 + 1 = 4: three killed");
		Assert.That(Alive(big), Nu.True);
		Assert.That(
			asked.OfType<ChooseNumberChoice>().All(n => n.ShareOf > 0),
			Nu.True,
			"each share asked for its target"
		);
	}

	[Test]
	public void FireCovenant_TheSearchKillsSeveralOfTwelve()
	{
		SixLands();
		var sizes = new[] { "2/2", "3/3", "4/4", "1/1", "5/5", "2/1" };
		for (var i = 0; i < 12; i++)
			Field(TestGames.Creature($"C{i}", sizes[i % sizes.Length]), them);
		Hand(Lsv("Fire Covenant"), me);
		var turn = s.GetGame().TurnNumber;

		for (var i = 0; i < 20 && s.GetGame().TurnNumber == turn; i++)
		{
			s = s.AutoPass();
			if (s.GetGame().TurnNumber == turn)
				s = new BeamSearchAi(s.DecidingPlayerId()).Decide(s).State;
		}

		Assert.That(TheirCreatures(), Nu.LessThanOrEqualTo(12 - 2), "more than one killed");
	}
}
