using MagicCore.Actions;
using MagicCore.Cards;
using MagicCore.Turns;

namespace MagicCore.Tests.Turns;

public class TurnStructureTests
{
	[Test]
	public void BothPassing_WalksEveryStepThatGivesPriority_ThenTheOpponentsTurn()
	{
		var s = TestGames.Started();
		var first = s.GetGame().StartingPlayerId;
		var seen = new List<(int Turn, Step Step, int Priority)>();

		while (s.GetGame().TurnNumber < 3)
		{
			var game = s.GetGame();
			if (!s.IsWaitingForChoice)
				seen.Add((game.TurnNumber, game.Step, game.PriorityPlayerId));
			s = TestGames.PassOrChoose(s);
		}

		Step[] withPriority =
		[
			Step.Upkeep,
			Step.Draw,
			Step.PreCombatMain,
			Step.BeginCombat,
			Step.DeclareAttackers,
			Step.EndCombat,
			Step.PostCombatMain,
			Step.End,
		];
		var second = s.OpponentOf(first);
		var expected = withPriority
			.SelectMany(step => new[] { (1, step, first), (1, step, second) })
			.Concat(withPriority.SelectMany(step => new[] { (2, step, second), (2, step, first) }));
		Assert.That(seen, Is.EqualTo(expected));
	}

	[Test]
	public void StartingPlayerSkipsTheFirstDraw_TheOtherPlayerDraws()
	{
		var s = TestGames.Started();
		var first = s.GetGame().StartingPlayerId;
		var second = s.OpponentOf(first);

		s = TestGames.PassUntil(s, x => x.GetGame().Step == Step.PreCombatMain);
		Assert.That(s.CardsIn(first, ZoneType.Hand).Count(), Is.EqualTo(Mulligan.HandSize));

		s = TestGames.PassUntil(
			s,
			x => x.GetGame().TurnNumber == 2 && x.GetGame().Step == Step.PreCombatMain
		);
		Assert.That(s.CardsIn(second, ZoneType.Hand).Count(), Is.EqualTo(Mulligan.HandSize + 1));
	}

	[Test]
	public void Land_OnlyInYourMainPhase_OncePerTurn_AndYouKeepPriority()
	{
		var s = TestGames.Started();
		var me = s.GetGame().StartingPlayerId;
		Assert.That(s.GetGame().Step, Is.EqualTo(Step.Upkeep));
		Assert.That(s.LegalActions().OfType<PlayLandAction>(), Is.Empty);

		s = TestGames.PassUntil(s, x => x.GetGame().Step == Step.PreCombatMain);
		s = s.Act(new PassPriorityAction { PlayerId = me });
		Assert.That(
			s.LegalActions().OfType<PlayLandAction>(),
			Is.Empty,
			"opponent's priority, my turn"
		);
		s = s.Act(new PassPriorityAction { PlayerId = s.OpponentOf(me) });
		s = TestGames.PassUntil(s, x => x.GetGame().Step == Step.PostCombatMain);

		var land = s.LegalActions().OfType<PlayLandAction>().First();
		s = s.Act(land);

		Assert.That(s.IsIn(land.CardId, me, ZoneType.Battlefield));
		Assert.That(s.GetGame().PriorityPlayerId, Is.EqualTo(me));
		Assert.That(s.GetGame().PassesInSuccession, Is.Zero);
		Assert.That(s.LegalActions().OfType<PlayLandAction>(), Is.Empty, "second land");
	}

	[Test]
	public void TappedLand_AddsMana_PoolEmptiesAtEndOfStep_LandUntapsNextTurn()
	{
		var s = TestGames.Started();
		var me = s.GetGame().StartingPlayerId;
		s = TestGames.PassUntil(s, x => x.GetGame().Step == Step.PreCombatMain);
		var land = s.LegalActions().OfType<PlayLandAction>().First();
		s = s.Act(land);

		s = s.Act(
			new TapForManaAction
			{
				PlayerId = me,
				CardId = land.CardId,
				Color = ManaColor.Green,
			}
		);
		Assert.That(s.GetPlayer(me).ManaPool.Green, Is.EqualTo(1));
		Assert.That(
			s.TryAddAction(
				new TapForManaAction
				{
					PlayerId = me,
					CardId = land.CardId,
					Color = ManaColor.Green,
				}
			).Success,
			Is.False,
			"already tapped"
		);

		s = s.Act(new PassPriorityAction { PlayerId = me });
		s = s.Act(new PassPriorityAction { PlayerId = s.OpponentOf(me) });
		Assert.That(s.GetPlayer(me).ManaPool, Is.EqualTo(ManaPool.Empty));

		s = TestGames.PassUntil(s, x => x.GetGame().TurnNumber == 3);
		Assert.That(s.GetCard(land.CardId).IsTapped, Is.False);
	}

	[Test]
	public void Forest_CannotTapForBlue()
	{
		var s = TestGames.Started();
		var me = s.GetGame().StartingPlayerId;
		s = TestGames.PassUntil(s, x => x.GetGame().Step == Step.PreCombatMain);
		var land = s.LegalActions().OfType<PlayLandAction>().First();
		s = s.Act(land);

		var blue = new TapForManaAction
		{
			PlayerId = me,
			CardId = land.CardId,
			Color = ManaColor.Blue,
		};
		Assert.That(s.TryAddAction(blue).Success, Is.False);
	}

	[Test]
	public void Cleanup_DiscardsDownToSeven_ByTheActivePlayersChoice()
	{
		var s = TestGames.Started();
		var second = s.OpponentOf(s.GetGame().StartingPlayerId);
		s = TestGames.PassUntil(s, x => x.IsWaitingForChoice);

		Assert.That(s.GetGame().Step, Is.EqualTo(Step.Cleanup));
		Assert.That(s.DecidingPlayerId(), Is.EqualTo(second));
		// Nobody holds priority during the choice — this once let "player 0" pass.
		Assert.That(s.LegalActions(), Is.Empty);
		Assert.That(s.TryAddAction(new PassPriorityAction { PlayerId = 0 }).Success, Is.False);
		Assert.That(s.TryAddAction(new PassPriorityAction { PlayerId = second }).Success, Is.False);
		var discard = s.GetPendingChoice()!.Options[0].Id;
		s = s.Choose([discard]);

		Assert.That(s.IsIn(discard, second, ZoneType.Graveyard));
		Assert.That(
			s.CardsIn(second, ZoneType.Hand).Count(),
			Is.EqualTo(TurnStructure.MaxHandSize)
		);
		Assert.That(s.GetGame().Step, Is.EqualTo(Step.Upkeep));
	}

	[Test]
	public void LandGoBots_PlayEveryLand_AndThePlayerOnTheDrawDecksFirst()
	{
		var s = TestGames.New();
		for (var i = 0; i < 10_000 && !s.IsOver(); i++)
			s = TestGames.LandGoBotStep(s);

		var first = s.GetGame().StartingPlayerId;
		var landsInDeck = TestGames.Deck().Count(c => c.Data.TypeLine.Contains("Land"));
		Assert.That(s.IsOver());
		Assert.That(s.WinnerId(), Is.EqualTo(first));
		Assert.That(s.GetPlayer(s.OpponentOf(first)).AttemptedDrawFromEmptyLibrary);
		Assert.That(s.CardsIn(first, ZoneType.Battlefield).Count(), Is.EqualTo(landsInDeck));
		Assert.That(s.LegalActions(), Is.Empty);
	}
}
