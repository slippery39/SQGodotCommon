using System.Linq;
using MagicCore.Limited;
using MagicTable;
using NUnit.Framework;

namespace SQGodotCommon.Tests;

/// <summary>A Quick Draft on the table (story LIM-2.3): your picks, the deck builder, the run against the bots.</summary>
public class DraftSessionTests
{
	static DraftSession Drafted(int seed = 5)
	{
		var d = new DraftSession(seed);
		while (!d.IsComplete)
			d.Pick(0);
		return d;
	}

	[Test]
	public void APick_IsYours_AndTheDraftMovesOn()
	{
		var d = new DraftSession(5);
		Assert.That(d.Where, Is.EqualTo("Pack 1 · Pick 1"));
		var taken = d.Pack[3];
		d.Pick(3);
		Assert.That(d.Picks, Is.EqualTo(new[] { taken }));
		Assert.That(d.Pack, Has.Count.EqualTo(d.PackSize - 1));
		Assert.That(d.Where, Is.EqualTo("Pack 1 · Pick 2"));
	}

	[Test]
	public void TheDraftEnds_AndYourPicksAreTheDeckBuildersPool()
	{
		var d = Drafted();
		Assert.That(d.Picks, Has.Count.EqualTo(d.PackSize * Draft.PackCount));
		Assert.That(d.Builder.Pool, Is.EqualTo(d.Picks));
		Assert.That(d.Builder, Is.SameAs(d.Builder), "one builder: your deck survives each game");
	}

	[Test]
	public void TheRun_FacesEachBotInTurn_UntilSevenWinsOrThreeLosses()
	{
		var d = Drafted();
		var faced = new System.Collections.Generic.List<int>();
		for (var i = 0; i < DraftSession.WinsToEnd; i++)
		{
			faced.Add(d.NextBot);
			d.Record(won: true);
		}
		Assert.That(faced, Is.EqualTo(Enumerable.Range(1, Draft.SeatCount - 1)), "every bot once");
		Assert.That(d.RunOver);
		d.Record(won: false);
		Assert.That(
			(d.Wins, d.Losses),
			Is.EqualTo((DraftSession.WinsToEnd, 0)),
			"nothing counts after"
		);

		var lost = Drafted();
		for (var i = 0; i < DraftSession.LossesToEnd; i++)
			lost.Record(won: false);
		Assert.That(lost.RunOver);
	}

	[Test]
	public void AGame_IsAgainstTheNextBotsDeck_AndReportsItsResult()
	{
		var d = Drafted();
		d.Builder.Suggest();
		var setup = d.ToSetup(search: false, gameSeed: 9);
		var bot = d.BotDeck(d.NextBot);
		Assert.That(bot.Main, Has.Count.EqualTo(SealedBuilder.DeckSize));
		Assert.That(setup.Theirs.Select(c => c.Data.Name), Is.EqualTo(bot.Main));
		Assert.That(
			bot.Main.Where(c => !SealedBuilder.Basics.Contains(c)),
			Is.SubsetOf(d.State.Seats[d.NextBot].Picks),
			"built from that bot's own picks"
		);
		setup.Finished!(true);
		Assert.That(d.Wins, Is.EqualTo(1));
	}
}
