using ImmutableGameObjects;
using MagicCore.Actions;
using MagicCore.Ai;
using MagicCore.Cards;
using MagicCore.Tests.Sets;
using Nu = NUnit.Framework.Is;

namespace MagicCore.Tests.Ai;

/// <summary>
/// MC-9: the cube's cards in the AI's hands — each item watched first (the position below is the one watched), then
/// fixed. A realistic board, not the base board's twenty basics: six lands, two each of Swamp, Island and Mountain.
/// </summary>
public class CubeAiTests : FraBoardTest
{
	void SixLands()
	{
		EmptyBoard();
		s = TestGames.EmptyHands(s);
		foreach (var land in new[] { "Swamp", "Swamp", "Island", "Island", "Mountain", "Mountain" })
			Field(TestGames.Card(land), me);
	}

	/// <summary>The search plays the rest of this turn, both seats; what each decision was, in order.</summary>
	List<string> SearchPlaysTheTurn()
	{
		var (turn, played) = (s.GetGame().TurnNumber, new List<string>());
		for (var i = 0; i < 30 && s.GetGame().TurnNumber == turn && !s.IsOver(); i++)
		{
			s = s.AutoPass();
			if (s.GetGame().TurnNumber != turn || s.IsOver())
				break;
			var decision = new BeamSearchAi(s.DecidingPlayerId()).Decide(s);
			(s, played) = (decision.State, [.. played, decision.What]);
		}
		return played;
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
		SearchPlaysTheTurn();

		Assert.That(TheirCreatures(), Nu.LessThanOrEqualTo(12 - 2), "more than one killed");
	}

	// MC-9.2, watched 2026-11-23: with no noncreature spell to grow her first, the search took Vivi's {0} mana move at
	// power 0 after its last spell - no mana, and the turn's one activation used.
	[Test]
	public void Vivi_AtPowerZero_HerManaMoveIsLeftOutOfTheSearch_NotTheRules()
	{
		SixLands();
		var vivi = Field(Lsv("Vivi Ornitier"), me);
		var bolt = Hand(Lsv("Lightning Bolt"), me);
		bool Offered(IEnumerable<GameAction> moves) =>
			moves.OfType<TapForManaAction>().Any(m => m.CardId == vivi);

		Assert.That(Offered(s.LegalActions()), Nu.True, "the rules offer it");
		Assert.That(Offered(s.SearchMoves()), Nu.False, "at power 0 it adds nothing");
		Cast(bolt, [], them); // "whenever you cast a noncreature spell, put a +1/+1 counter on Vivi"
		Assert.That(s.Power(s.GetCard(vivi)), Nu.GreaterThan(0), "setup: she grew");
		Assert.That(Offered(s.SearchMoves()), Nu.True, "with power, offered");
	}

	[Test]
	public void Vivi_TheSearchsTurnWithoutASpell_NeverTakesHerManaMove()
	{
		SixLands();
		Field(Lsv("Vivi Ornitier"), me);
		Hand(TestGames.CreatureCosting("{4}{U}", "Big Blue", "5/5"), me);

		var played = SearchPlaysTheTurn();

		Assert.That(played, Has.Some.StartsWith("Cast Big Blue"), "setup: the turn was played");
		Assert.That(played, Has.None.StartsWith("Vivi Ornitier"));
	}
}
