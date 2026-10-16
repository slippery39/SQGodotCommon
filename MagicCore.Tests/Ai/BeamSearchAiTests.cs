using ImmutableGameObjects;
using MagicCore.Actions;
using MagicCore.Ai;

namespace MagicCore.Tests.Ai;

public class BeamSearchAiTests
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

	GameState WithLife(GameState state, int player, int life) =>
		state.WithPlayer(state.GetPlayer(player) with { Life = life });

	/// <summary>The search decides for us, <paramref name="opponent"/> for them, until the turn passes <paramref name="lastTurn"/>.</summary>
	GameState PlayThrough(GameState state, int lastTurn, IAi opponent) =>
		TestGames.PlayAis(state, lastTurn, new BeamSearchAi(me), opponent);

	[Test]
	public void ItTakesLethal_OnBoard()
	{
		(s, _) = TestGames.Put(s, me, TestGames.Creature("Ours", "3/3"), ZoneType.Battlefield);
		s = WithLife(s, them, 3);

		s = PlayThrough(s, s.GetGame().TurnNumber, new GreedyAi(them));

		Assert.That(s.WinnerId(), Is.EqualTo(me));
	}

	// Their 4/4 attacks us at 3 life: the search must see that blocking with the 1/1 is survival.
	[Test]
	public void FacingLethal_ItChumpBlocks()
	{
		(s, _) = TestGames.Put(s, me, TestGames.Creature("Chump", "1/1"), ZoneType.Battlefield);
		(s, _) = TestGames.Put(s, them, TestGames.Creature("Theirs", "4/4"), ZoneType.Battlefield);
		s = WithLife(s, me, 3);

		s = PlayThrough(s, s.GetGame().TurnNumber + 1, new GreedyAi(them));

		Assert.That(s.GetPlayer(me).HasLost, Is.False);
	}

	[Test]
	public void TheSamePosition_GetsTheSameDecision()
	{
		(s, _) = TestGames.Put(s, me, TestGames.Creature("Ours", "3/3"), ZoneType.Battlefield);
		(s, _) = TestGames.Put(s, them, TestGames.Creature("Theirs", "2/4"), ZoneType.Battlefield);

		Assert.That(
			new BeamSearchAi(me).Decide(s).What,
			Is.EqualTo(new BeamSearchAi(me).Decide(s).What)
		);
	}

	// A planned move is re-found by MoveKey, so everything that tells two legal moves apart must be in
	// it — MtgSimulator replayed a planned X=4 as X=0 because X was not.
	[Test]
	public void MoveKey_TellsXApart()
	{
		int blast;
		(s, blast) = TestGames.Put(
			s,
			me,
			TestGames.Inline("Blast", "{X}{R}", "Sorcery"),
			ZoneType.Hand
		);

		var x1 = new CastSpellAction
		{
			PlayerId = me,
			CardId = blast,
			X = 1,
		};
		var x4 = new CastSpellAction
		{
			PlayerId = me,
			CardId = blast,
			X = 4,
		};

		Assert.That(s.MoveKey(x1), Is.Not.EqualTo(s.MoveKey(x4)));
		Assert.That(s.MoveKey(x1), Is.EqualTo(s.MoveKey(x1 with { })));
	}

	// A loyalty move's targets are an array, which a record compares by reference — a planned +1 or
	// −X was never re-found until MoveKey spelled it out.
	[Test]
	public void MoveKey_ReFindsALoyaltyMove_AndTellsXApart()
	{
		var planned = new ActivateLoyaltyAction
		{
			PlayerId = me,
			CardId = 7,
			AbilityIndex = 1,
			X = 2,
			Targets = [5],
		};
		var regenerated = planned with { Targets = [5] };

		Assert.That(s.MoveKey(regenerated), Is.EqualTo(s.MoveKey(planned)));
		Assert.That(s.MoveKey(planned with { X = 3 }), Is.Not.EqualTo(s.MoveKey(planned)));
	}

	// Deterministic, and a mirror pair is one game with the names swapped: exactly half.
	[Test, Explicit("plays 12 games of the search against itself")]
	public void TheSearchAgainstItself_ScoresExactlyHalf()
	{
		var result = AiMatch.Run("beam", "beam", games: 12);
		TestContext.Out.WriteLine(result.Report());

		Assert.That(result.Games.All(g => g.Finished), result.Report());
		Assert.That(result.WinRateA, Is.EqualTo(0.5), result.Report());
	}
}
