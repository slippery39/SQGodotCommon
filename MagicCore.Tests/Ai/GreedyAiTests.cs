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

	// The AI is measured by what it achieves, not by inspecting its picks. Both deck pairings and
	// both seats, so a win cannot come from one deck being stronger.
	[Test]
	public void BeatsRandomPlay_InNearlyEveryGame()
	{
		var (games, wins) = (0, 0);
		foreach (
			var (aiDeck, randomDeck) in new[]
			{
				("White-Black", "Red-Green"),
				("Red-Green", "White-Black"),
			}
		)
			for (var seed = 1; seed <= 6; seed++)
			{
				var s = MagicRules.NewGame(FraDecks.All[aiDeck], FraDecks.All[randomDeck], seed);
				var aiSeat = s.Player1Id();
				s = Play(s, new() { [aiSeat] = new GreedyAi(aiSeat) }, seed);
				games++;
				wins += s.WinnerId() == aiSeat ? 1 : 0;
			}

		Assert.That(wins, Is.GreaterThanOrEqualTo(games * 0.9), $"{wins}/{games}");
	}

	// A creature saved by a pump is alive; it was once scored as dead from its PRINTED toughness.
	[Test]
	public void Evaluate_ACreatureSavedByAPump_StillCounts()
	{
		var s = TestGames.InFirstMain();
		var me = s.GetGame().StartingPlayerId;
		var ai = new GreedyAi(me);
		var before = ai.Evaluate(s);
		int bear;
		(s, bear) = TestGames.Put(s, me, TestGames.Creature("Bear", "2/2"), ZoneType.Battlefield);
		var pumped = (Card)s.GetCard(bear).WithComponent(new UntilEndOfTurn(2, 2)) with
		{
			Damage = 3,
		};
		s = s.UpdateObject(bear, pumped);

		Assert.That(ai.Evaluate(s), Is.GreaterThan(before));
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
