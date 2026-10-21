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
	public void ASeed_OpensYourPoolAndTheAis_AndReplaysBoth()
	{
		var a = new SealedSession(11);
		var b = new SealedSession(11);
		Assert.That(a.Pool, Is.EqualTo(b.Pool));
		Assert.That(a.OpponentPool, Is.EqualTo(b.OpponentPool));
		Assert.That(a.OpponentPool, Is.Not.EqualTo(a.Pool));
		Assert.That(a.Opponent.Main, Has.Count.EqualTo(SealedBuilder.DeckSize));
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
	/// holds one to keep out. The guard stays for sets to come (and MC-3's Special Guests).
	/// </summary>
	[Test]
	public void EveryFraCard_IsPlayable() =>
		Assert.That(
			MagicCore
				.Cards.CardDataSet.Load("FRA")
				.Where(c => !SealedSession.Playable(c.Name))
				.Select(c => c.Name),
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
	public void TheSetup_PlaysYourDeck_AgainstTheAisOwnBuild_OnTheEventsSeed()
	{
		var s = new SealedSession(9);
		s.Suggest();
		var setup = s.ToSetup(search: true);
		Assert.That(setup.Mine.Select(c => c.Data.Name), Is.EquivalentTo(s.Deck));
		Assert.That(setup.Theirs.Select(c => c.Data.Name), Is.EquivalentTo(s.Opponent.Main));
		Assert.That(setup.Seed, Is.EqualTo(9));
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
			Is.EquivalentTo(s.Opponent.Sideboard.Where(SealedSession.Playable))
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
			Is.EqualTo(s.Opponent.Sideboard.Count),
			"and a replay keeps both"
		);
	}

	[Test]
	public void ANameThatIsNoBasic_IsRefused() =>
		Assert.Throws<System.ArgumentException>(() => new SealedSession(3).SetBasics("Wastes", 1));
}
