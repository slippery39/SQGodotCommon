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
	// AI in both seats, so a win cannot come from one deck being stronger. 85% overall, not per
	// pairing: Red-Green loses ~1 in 6 to random White-Black's fliers — a one-ply ceiling, measured
	// in docs/findings/magic-ai.md. A drop below 85% is a regression; that weakness is not.
	[Test]
	public void BeatsRandomPlay_InNearlyEveryGame()
	{
		var decks = FraDecks.All.Keys.ToList();
		var (games, wins) = (0, 0);
		foreach (var aiDeck in decks)
		foreach (var randomDeck in decks.Where(d => d != aiDeck))
			for (var seed = 1; seed <= 5; seed++)
			{
				var s = MagicRules.NewGame(FraDecks.All[aiDeck], FraDecks.All[randomDeck], seed);
				var aiSeat = s.Player1Id();
				s = Play(s, new() { [aiSeat] = new GreedyAi(aiSeat) }, seed);
				games++;
				wins += s.WinnerId() == aiSeat ? 1 : 0;
			}

		Assert.That(wins, Is.GreaterThanOrEqualTo(games * 0.85), $"{wins}/{games}");
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

	[Test]
	public void AiVersusAi_Finishes([Range(1, 4)] int seed)
	{
		var s = MagicRules.NewGame(FraDecks.All["White-Black"], FraDecks.All["Red-Green"], seed);
		var (p1, p2) = (s.Player1Id(), s.Player2Id());

		s = Play(s, new() { [p1] = new GreedyAi(p1), [p2] = new GreedyAi(p2) }, seed);

		Assert.That(s.IsOver());
	}
}
