using System.Linq;
using MagicCore;
using MagicCore.Limited;
using MagicCore.Sets.Fra;
using MagicTable;
using NUnit.Framework;

namespace SQGodotCommon.Tests;

/// <summary>Sealed deck building on the table (story LIM-1): pools, the deck from the pool, the suggestion, the setup.</summary>
public class SealedSessionTests
{
	[Test]
	public void ASeed_ReplaysYourPool_AndAGameSeed_ItsOpponent()
	{
		Assert.That(new SealedSession(11).Pool, Is.EqualTo(new SealedSession(11).Pool));
		Assert.That(
			SealedSession.OpponentFor(3).Main,
			Is.EqualTo(SealedSession.OpponentFor(3).Main)
		);
		Assert.That(SealedSession.OpponentFor(3).Main, Has.Count.EqualTo(SealedBuilder.DeckSize));
	}

	[Test]
	public void EachGame_FacesAFreshOpponent()
	{
		var s = new SealedSession(11);
		s.Suggest();
		Assert.That(
			s.ToSetup(search: false, gameSeed: 1).Theirs.Select(c => c.Data.Name),
			Is.Not.EquivalentTo(
				s.ToSetup(search: false, gameSeed: 2).Theirs.Select(c => c.Data.Name)
			)
		);
	}

	/// <summary>
	/// LIM-1.7: a seeded pool, exported as Arena does (part built, the rest in the sideboard) with a
	/// card from another set — read back whole, that card left out, and it builds and plays.
	/// </summary>
	[Test]
	public void YourArenaPool_Pasted_BuildsAndPlays_ACardNotInFraLeftOut()
	{
		var opened = new SealedSession(12).Pool;
		static string Lines(System.Collections.Generic.IEnumerable<string> cards) =>
			string.Join('\n', cards.GroupBy(c => c).Select(g => $"{g.Count()} {g.Key} (FRA) 1"));
		var export = $"""
			Deck
			{Lines(opened.Take(20))}
			9 Plains (FRA) 270

			Sideboard
			{Lines(opened.Skip(20))}
			1 Lightning Bolt (STA) 42
			""";

		var read = ArenaDeck.ReadPool(export);
		Assert.That(
			read.Pool,
			Is.EquivalentTo(opened.Where(c => !SealedSession.BasicNames.Contains(c)))
		);
		Assert.That(read.LeftOut, Is.EqualTo(new[] { "Lightning Bolt" }));

		var s = new SealedSession(read.Pool, read.LeftOut);
		Assert.That(s.Seed, Is.Null);
		s.Suggest();
		Assert.That(s.Ready);
		Assert.That(
			s.ToSetup(search: false, gameSeed: 5).Mine,
			Has.Count.EqualTo(SealedBuilder.DeckSize)
		);
	}

	[Test]
	public void TheSuggestion_IsTheBuildersDeck_AndReady()
	{
		var s = new SealedSession(4);
		var built = s.Suggest();
		Assert.That(s.Deck, Is.EquivalentTo(built.Main));
		Assert.That(s.Ready);
		Assert.That(s.Sideboard, Is.EquivalentTo(built.Sideboard));
	}

	/// <summary>LIM-3: every suggestion of a pool that offers a splash, taken in turn, is the builder's deck with its label.</summary>
	[Test]
	public void EachSuggestion_TakenInTurn_IsThatBuild_AndClearForgetsIt()
	{
		var s = Enumerable
			.Range(1, 30)
			.Select(seed => new SealedSession(seed))
			.First(x => x.Suggestions.Count >= 2);
		for (var i = 0; i < s.Suggestions.Count; i++)
		{
			var built = s.Suggest(i);
			Assert.That(built, Is.SameAs(s.Suggestions[i]));
			Assert.That(s.Deck, Is.EquivalentTo(built.Main), built.Label);
			Assert.That(s.Sideboard, Is.EquivalentTo(built.Sideboard), built.Label);
			Assert.That(s.Suggested, Is.EqualTo(built.Label));
			Assert.That(s.SuggestedIndex, Is.EqualTo(i));
			Assert.That(
				s.Measured(i),
				Is.EqualTo(Castability.Measure(built)),
				"the simulated draws, once"
			);
			// beside the best pair's two colours, named by its colours — except on that build itself;
			// and its early curve (LIM-3.6)
			var against = $"({s.Suggestions[0].Button} ";
			Assert.That(
				s.OnTimeText(i),
				i == 0 ? Does.Not.Contain(against) : Does.Contain(against)
			);
			Assert.That(
				s.OnTimeText(i),
				Does.Contain($"· {SealedBuilder.CheapSpells(built.Main)} spells at 1–3 mana")
			);
			// fixing's tempo, from the same games (LIM-3.7) — shown only when it ever happens
			Assert.That(
				s.OnTimeText(i),
				s.Measured(i).CyclesEarly >= SealedSession.CyclesShownFrom
					? Does.Contain($"in {s.Measured(i).CyclesEarly:P0} of games")
					: Does.Not.Contain("cycles")
			);
			Assert.That(s.Ready);
		}
		s.Clear();
		Assert.That(s.Suggested, Is.Null);
		Assert.That(s.SuggestedIndex, Is.Null);
		Assert.That(s.Count, Is.Zero);
	}

	[Test]
	public void ACardMovesBetweenPoolAndDeck_NeverMoreCopiesThanOpened()
	{
		var s = new SealedSession(6);
		var card = s.Pool.First(SealedSession.Playable);
		var opened = s.Pool.Count(c => c == card);

		for (var i = 0; i < opened; i++)
			Assert.That(s.Add(card));
		Assert.That(s.Add(card), Is.False, "every copy is already in the deck");
		Assert.That(s.FromPool.Count(c => c == card), Is.EqualTo(opened));
		Assert.That(s.Sideboard, Does.Not.Contain(card));

		Assert.That(s.Remove(card));
		Assert.That(s.Sideboard.Count + s.FromPool.Count, Is.EqualTo(s.Pool.Count));
	}

	/// <summary>
	/// Extrapolate the Impossible was the last (LIM-1.6): no FRA card is unscripted now, so no pool
	/// holds one to keep out — nor one of the Special Guests (MC-3). The guard stays for sets to come.
	/// </summary>
	[Test]
	public void EveryFraCard_IsPlayable() =>
		Assert.That(
			MagicCore
				.Cards.CardDataSet.Load("FRA")
				.Where(c => !SealedSession.Playable(c.Card.Name))
				.Select(c => c.Card.Name),
			Is.Empty
		);

	[Test]
	public void BasicsAreFree_AndCountTowardForty()
	{
		var s = new SealedSession(8);
		s.SetBasics("Forest", SealedBuilder.DeckSize);
		Assert.That(s.Ready);
		Assert.That(s.Deck.Count(c => c == "Forest"), Is.EqualTo(SealedBuilder.DeckSize));
		s.SetBasics("Forest", -3);
		Assert.That(s.Basics("Forest"), Is.Zero);
		Assert.That(s.Ready, Is.False);
	}

	[Test]
	public void TheSetup_PlaysYourDeck_AgainstTheAisOwnBuild_OnTheGamesSeed()
	{
		var s = new SealedSession(9);
		s.Suggest();
		var setup = s.ToSetup(search: true, gameSeed: 21);
		var opponent = SealedSession.OpponentFor(21);
		Assert.That(setup.Mine.Select(c => c.Data.Name), Is.EquivalentTo(s.Deck));
		Assert.That(setup.Theirs.Select(c => c.Data.Name), Is.EquivalentTo(opponent.Main));
		Assert.That(setup.Seed, Is.EqualTo(21));
		Assert.That(setup.AiVsAi, Is.False);
		Assert.That(
			setup.Back,
			Does.EndWith("SealedScene.tscn"),
			"Escape from the table returns to the pool"
		);
		Assert.That(
			setup.MySideboard.Select(c => c.Data.Name),
			Is.EquivalentTo(s.Sideboard.Where(SealedSession.Playable))
		);
		Assert.That(
			setup.TheirSideboard.Select(c => c.Data.Name),
			Is.EquivalentTo(opponent.Sideboard.Where(SealedSession.Playable))
		);

		var game = new MagicTableManager(
			setup.Mine,
			setup.Theirs,
			setup.Seed!.Value,
			sideboard1: setup.MySideboard,
			sideboard2: setup.TheirSideboard
		);
		Assert.That(
			game.State.CardsIn(game.HumanId, ZoneType.Sideboard).Select(c => c.Name),
			Is.EquivalentTo(s.Sideboard),
			"the table's game holds your sideboard outside the game"
		);
		Assert.That(
			game.Replay().State.CardsIn(game.OpponentId, ZoneType.Sideboard).Count(),
			Is.EqualTo(opponent.Sideboard.Count),
			"and a replay keeps both"
		);
	}

	[Test]
	public void ANameThatIsNoBasic_IsRefused() =>
		Assert.Throws<System.ArgumentException>(() => new SealedSession(3).SetBasics("Wastes", 1));

	// LIM-3.6 (Shayne): every pair within 1.5 points of the best is listed; showing one adds its own
	// builds (its two colours, and its best splash or greedy), in place of the last pair shown.
	[Test]
	public void ShowingAClosePair_AddsItsBuilds_InPlaceOfTheLastPairs()
	{
		var s = Enumerable
			.Range(1, 30)
			.Select(seed => new SealedSession(seed))
			.First(x => x.ClosePairs.Count >= 2);
		var best = s.Suggestions.Count;

		s.ShowPair(0);
		var first = s.Suggestions.Skip(best).ToList();
		Assert.That(first, Is.Not.Empty);
		Assert.That(
			first[0].Colors,
			Is.EquivalentTo(s.ClosePairs[0].Pair),
			"its two colours first"
		);
		Assert.That(s.ShownPair, Is.EqualTo(0));

		s.Suggest(best); // a deck from the first pair's build
		s.ShowPair(1);
		Assert.That(
			s.Suggestions.Skip(best).Select(b => b.Colors.Take(2).Order()),
			Has.All.EqualTo(s.ClosePairs[1].Pair.Order()),
			"in place of the last pair's"
		);
		Assert.That(s.SuggestedIndex, Is.Null, "its build is gone — the deck stays");
		Assert.That(s.Count, Is.EqualTo(SealedBuilder.DeckSize));
	}

	// LIM-3.4 (Shayne): the Sealed screen gives the console command that compares everything it offers.
	[Test]
	public void TheSimCommand_NamesThePool_AndHowManyBuildsItPlays()
	{
		var seeded = new SealedSession(3);
		var (command, note) = seeded.SimCommand(null);
		Assert.That(command, Does.EndWith("--compare=seed:3"));
		Assert.That(
			note,
			Does.StartWith(
				$"{SealedBuilder.AllBuilds(seeded.Pool, CardRatings.Of("FRA")).Count} builds"
			)
		);

		var pasted = new SealedSession(seeded.Pool);
		Assert.That(
			pasted.SimCommand("pools/mine.txt").Command,
			Does.EndWith("--compare=\"pools/mine.txt\"")
		);
		Assert.Throws<System.InvalidOperationException>(
			() => pasted.SimCommand(null),
			"a pasted pool needs its file"
		);
	}
}
