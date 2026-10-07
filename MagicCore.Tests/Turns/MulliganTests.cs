using MagicCore.Actions;
using MagicCore.Cards;

namespace MagicCore.Tests.Turns;

public class MulliganTests
{
	[Test]
	public void StartingPlayerDecidesFirst_KeepOrMulligan()
	{
		var s = TestGames.New();

		Assert.That(s.GetGame().Step, Is.EqualTo(Step.PreGame));
		Assert.That(s.DecidingPlayerId(), Is.EqualTo(s.GetGame().StartingPlayerId));
		Assert.That(
			s.LegalActions().Select(a => a.GetType()),
			Is.EquivalentTo(new[] { typeof(KeepHandAction), typeof(MulliganAction) })
		);
	}

	[Test]
	public void London_RedrawSeven_ThenBottomOnePerMulligan()
	{
		var s = TestGames.New();
		var me = s.DecidingPlayerId();

		s = s.Act(new MulliganAction { PlayerId = me });
		s = s.Act(new MulliganAction { PlayerId = me });
		Assert.That(s.CardsIn(me, ZoneType.Hand).Count(), Is.EqualTo(Mulligan.HandSize));

		s = s.Act(new KeepHandAction { PlayerId = me });
		var choice = s.GetPendingChoice()!;
		Assert.That(choice.MinChoices, Is.EqualTo(2));
		var bottom = choice.Options.Take(2).Select(o => o.Id).ToList();
		s = s.Choose(bottom);

		Assert.That(s.CardsIn(me, ZoneType.Hand).Count(), Is.EqualTo(Mulligan.HandSize - 2));
		Assert.That(
			s.CardsIn(me, ZoneType.Library).TakeLast(2).Select(c => c.Id),
			Is.EqualTo(bottom)
		);
		Assert.That(
			s.DecidingPlayerId(),
			Is.EqualTo(s.OpponentOf(me)),
			"now the other player decides"
		);
	}

	[Test]
	public void Bottoming_TheSameCardTwice_IsRefused()
	{
		var s = TestGames.New();
		var me = s.DecidingPlayerId();
		s = s.Act(new MulliganAction { PlayerId = me });
		s = s.Act(new MulliganAction { PlayerId = me });
		s = s.Act(new KeepHandAction { PlayerId = me });
		var card = s.GetPendingChoice()!.Options[0].Id;

		Assert.That(() => s.Choose([card, card]), Throws.InvalidOperationException);
	}

	[Test]
	public void BothKeep_FirstTurnBeginsAtUpkeep()
	{
		var s = TestGames.Started();

		Assert.That(s.GetGame().TurnNumber, Is.EqualTo(1));
		Assert.That(s.GetGame().Step, Is.EqualTo(Step.Upkeep));
		Assert.That(s.GetGame().PriorityPlayerId, Is.EqualTo(s.GetGame().StartingPlayerId));
	}

	[Test]
	public void SameSeed_SameGame()
	{
		Assert.That(Hand(TestGames.New(seed: 7)), Is.EqualTo(Hand(TestGames.New(seed: 7))));
		Assert.That(Hand(TestGames.New(seed: 7)), Is.Not.EqualTo(Hand(TestGames.New(seed: 8))));
	}

	[Test]
	public void CoinFlip_EitherPlayerCanStart()
	{
		var starters = Enumerable
			.Range(1, 20)
			.Select(seed => TestGames.New(seed).GetGame().StartingPlayerId);
		Assert.That(starters.Distinct().Count(), Is.EqualTo(2));
	}

	// The smoother's whole point: fewer land-flood and land-screw openers. Measured over the same
	// seeds with it on and off, so the deck and the shuffles are identical except for the smoother.
	[Test]
	public void HandSmoothing_PullsOpeningLandCountsTowardTheDeckAverage()
	{
		var seeds = Enumerable.Range(1, 300).ToList();
		double Spread(bool smoothing)
		{
			var expected =
				7.0
				* TestGames.Deck().Count(c => c.Data.TypeLine.Contains("Land"))
				/ TestGames.Deck().Count;
			return seeds.Average(seed =>
			{
				var s = TestGames.New(seed, smoothing);
				var lands = s.CardsIn(s.Player1Id(), ZoneType.Hand).Count(c => c.Is(CardType.Land));
				return Math.Abs(lands - expected);
			});
		}

		Assert.That(Spread(smoothing: true), Is.LessThan(Spread(smoothing: false) * 0.8));
	}

	static List<string> Hand(ImmutableGameObjects.GameState s) =>
		s.CardsIn(s.Player1Id(), ZoneType.Hand).Select(c => c.Name).ToList();
}
