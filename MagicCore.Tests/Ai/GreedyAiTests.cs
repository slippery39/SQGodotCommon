using ImmutableGameObjects;
using MagicCore.Ai;
using MagicCore.Cards;
using MagicCore.Sets.Fra;

namespace MagicCore.Tests.Ai;

public class GreedyAiTests
{
	/// <summary>Plays one game; <paramref name="ai"/> decides for its seat, random play for the other.</summary>
	static GameState Play(GameState s, Dictionary<int, GreedyAi> ais, int seed)
	{
		var rng = new Random(seed);
		for (var i = 0; i < 5_000 && !s.IsOver(); i++)
		{
			s = s.AutoPass();
			if (s.IsOver())
				break;
			var who = s.DecidingPlayerId();
			if (ais.TryGetValue(who, out var ai))
				s = ai.Decide(s).State;
			else if (s.GetPendingChoice() is { } choice)
				s = s.Choose(
					choice
						.Options.OrderBy(_ => rng.Next())
						.Take(choice.MinChoices)
						.Select(o => o.Id)
				);
			else
			{
				var pool = s.LegalActions().Where(a => a is not Actions.MulliganAction).ToList();
				s = s.Act(pool[rng.Next(pool.Count)]);
			}
		}
		return s;
	}

	// The AI is measured by what it achieves, not by inspecting its picks. Every deck pairing, the
	// AI in both seats, so a win cannot come from one deck being stronger. 90% overall AND 6 of 10
	// per pairing: the overall bar alone let one matchup collapse unseen. Red-Green vs White-Black
	// is the weakest pairing — measured in docs/findings/magic-ai.md, not hidden here.
	[Test]
	public void BeatsRandomPlay_InNearlyEveryGame()
	{
		var decks = FraDecks.All.Keys.ToList();
		var results = new List<(string Pairing, int Wins, int Games)>();
		foreach (var aiDeck in decks)
		foreach (var randomDeck in decks.Where(d => d != aiDeck))
		{
			var wins = 0;
			for (var seed = 1; seed <= 10; seed++)
			{
				var s = MagicRules.NewGame(FraDecks.All[aiDeck], FraDecks.All[randomDeck], seed);
				var aiSeat = s.Player1Id();
				s = Play(s, new() { [aiSeat] = new GreedyAi(aiSeat) }, seed);
				wins += s.WinnerId() == aiSeat ? 1 : 0;
			}
			results.Add(($"{aiDeck} vs {randomDeck}", wins, 10));
		}

		var summary = string.Join("; ", results.Select(r => $"{r.Pairing} {r.Wins}/{r.Games}"));
		Assert.That(
			results.Sum(r => r.Wins),
			Is.GreaterThanOrEqualTo(results.Sum(r => r.Games) * 0.9),
			summary
		);
		Assert.That(results.Where(r => r.Wins < 6), Is.Empty, summary);
	}

	// A creature saved by a pump is alive; it was once scored as dead from its PRINTED toughness.
	[Test]
	public void Evaluate_ACreatureSavedByAPump_StillCounts()
	{
		var s = TestGames.InFirstMain();
		var me = s.GetGame().StartingPlayerId;
		var before = Evaluator.Evaluate(s, me);
		int bear;
		(s, bear) = TestGames.Put(s, me, TestGames.Creature("Bear", "2/2"), ZoneType.Battlefield);
		var pumped = (Card)s.GetCard(bear).WithComponent(new UntilEndOfTurn(2, 2)) with
		{
			Damage = 3,
		};
		s = s.UpdateObject(bear, pumped);

		Assert.That(Evaluator.Evaluate(s, me), Is.GreaterThan(before));
	}

	// Lethal damage doesn't kill an indestructible creature, so the AI must not score it as dead.
	[Test]
	public void Evaluate_ADamagedIndestructibleCreature_StillCounts()
	{
		var s = TestGames.InFirstMain();
		var me = s.GetGame().StartingPlayerId;
		var before = Evaluator.Evaluate(s, me);
		int bear;
		(s, bear) = TestGames.Put(s, me, TestGames.Creature("Bear", "2/2"), ZoneType.Battlefield);
		var indestructible = (Card)
			s.GetCard(bear).WithComponent(new UntilEndOfTurn(Keyword: "Indestructible")) with
		{
			Damage = 5,
		};
		s = s.UpdateObject(bear, indestructible);

		Assert.That(Evaluator.Evaluate(s, me), Is.GreaterThan(before));
	}

	// At 0.8 per loyalty counter the AI valued a counter at two thirds of a card, so it never spent
	// Jace's loyalty on anything — the Jace deck's engine sat idle in AI hands.
	[Test]
	public void SpendsJacesLoyalty_WhenItHasNothingBetterToDo()
	{
		var s = TestGames.InFirstMain();
		var me = s.GetGame().StartingPlayerId;
		int jace;
		(s, jace) = TestGames.Put(
			s,
			me,
			MagicCore.Sets.Fra.EmpowerJace.JaceToken,
			ZoneType.Battlefield
		);
		s = s.UpdateObject(jace, s.GetCard(jace) with { Loyalty = 6, IsToken = true });
		var ai = new GreedyAi(me);

		for (var i = 0; i < 3 && s.GetCard(jace).Loyalty == 6; i++)
			s = s.AutoPass().DecidingPlayerId() == me ? ai.Decide(s).State : s;

		Assert.That(s.GetCard(jace).Loyalty, Is.LessThan(6));
	}

	// Life was capped at 20 in the evaluation: above it, damage scored zero, so the AI declined a
	// free attack into a board of tapped creatures (docs/findings/magic-ai.md).
	[Test]
	public void AttacksPastTappedBlockers_EvenWhenTheOpponentIsAboveTwenty()
	{
		var s = TestGames.InFirstMain();
		var (me, them) = (s.GetGame().StartingPlayerId, s.OpponentOf(s.GetGame().StartingPlayerId));
		int bear,
			wall;
		(s, bear) = TestGames.Put(s, me, TestGames.Creature("Bear", "2/2"), ZoneType.Battlefield);
		(s, wall) = TestGames.Put(s, them, TestGames.Creature("Wall", "1/4"), ZoneType.Battlefield);
		s = s.UpdateObject(wall, s.GetCard(wall) with { IsTapped = true }); // TEST SETUP: it attacked last turn
		s = s.WithPlayer(s.GetPlayer(them) with { Life = 29 });

		s = TestGames.PassUntil(s, x => x.IsWaitingForChoice);
		var (_, what) = new GreedyAi(me).Decide(s);

		Assert.That(what, Does.EndWith(s.GetPlayer(them).Name), what);
	}

	// Lands were capped at 7 in the evaluation, so an 8th scored less in play than in hand and the
	// AI hoarded lands — no landfall, no Koth, three or four dead cards at the end of every game.
	[Test]
	public void PlaysASurplusLand()
	{
		var s = TestGames.InFirstMain();
		var me = s.GetGame().StartingPlayerId;
		for (var i = 0; i < 7; i++)
			(s, _) = TestGames.Put(s, me, TestGames.Card("Forest"), ZoneType.Battlefield);
		foreach (var card in s.CardsIn(me, ZoneType.Hand).ToList())
			s = s.MoveCard(card.Id, s.ZoneId(me, ZoneType.Library)); // TEST SETUP: a hand of one land
		(s, _) = TestGames.Put(s, me, TestGames.Card("Forest"), ZoneType.Hand);

		var (_, what) = new GreedyAi(me).Decide(s);

		Assert.That(what, Is.EqualTo("Play Forest"));
	}

	[Test]
	public void AiVersusAi_Finishes([Range(1, 4)] int seed)
	{
		var s = MagicRules.NewGame(FraDecks.All["White-Black"], FraDecks.All["Red-Green"], seed);
		var (p1, p2) = (s.Player1Id(), s.Player2Id());

		s = Play(s, new() { [p1] = new GreedyAi(p1), [p2] = new GreedyAi(p2) }, seed);

		Assert.That(s.IsOver());
	}
}
