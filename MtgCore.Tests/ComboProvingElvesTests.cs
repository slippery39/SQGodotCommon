using System.Collections.Immutable;
using ImmutableGameObjects;
using NUnit.Framework;

namespace MtgCore.Tests;

/// <summary>
/// **The Elf mana engine, asserted as a mechanism.** Real set cards rather than stand-ins, for the
/// same reason as <see cref="ComboProvingTwinTests"/>: what is under test is whether these printed
/// cards form an engine, so a test on inline copies would pass while the shipped set did nothing.
///
/// Nothing here asserts a rate, so card balance cannot break it.
/// </summary>
[TestFixture]
public class ComboProvingElvesTests
{
	private GameState _state;
	private MtgGameIds _ids;

	[SetUp]
	public void Setup() => (_state, _ids) = MtgGameFactory.CreateForTesting();

	private static Card Card(string name) => ComboProving.Cards.Single(c => c.Name == name);

	/// <summary>
	/// The engine's premise: one Elf taps for one mana and is break-even, and it is the SECOND and
	/// THIRD Elf that make the first one worth playing. Scaling is the whole card.
	/// </summary>
	[Test]
	public void TheConduitTapsForOneManaPerElf()
	{
		var (s, conduit) = Play(_state, Card("Wirewood Conduit"));
		var (s2, _) = Play(s, Card("Wirewood Herald"));
		var (s3, _) = Play(s2, Card("Timberwatch Elder"));

		var before = Mana(s3);
		var after = Activate(s3, conduit.Id);

		// Three Elves on the battlefield, the Conduit itself included.
		Assert.That(Mana(after) - before, Is.EqualTo(3));
	}

	/// <summary>
	/// **The engine step.** The untapper readies the Conduit so it taps a second time in one turn —
	/// which is the only reason a one-mana 1/1 that readies things belongs in a mana deck.
	/// </summary>
	[Test]
	public void TheUntapperBuysASecondActivation()
	{
		var (s, conduit) = Play(_state, Card("Wirewood Conduit"));
		var (s2, symbiont) = Play(s, Card("Wirewood Symbiont"));

		var before = Mana(s2);
		var once = Activate(s2, conduit.Id);
		Assert.That(IsExhausted(once, conduit.Id), Is.True, "the Conduit spent its tap");

		var readied = Activate(once, symbiont.Id, conduit.Id);
		Assert.That(IsExhausted(readied, conduit.Id), Is.False, "the Symbiont readied it");

		var twice = Activate(readied, conduit.Id);

		// Two Elves, tapped twice: 2 + 2.
		Assert.That(Mana(twice) - before, Is.EqualTo(4), "it produced mana twice in one turn");
	}

	/// <summary>
	/// The card engine. Its trigger `Filter` is a real `TargetSpecification`, which is also what
	/// makes it the most discoverable card in the package.
	/// </summary>
	[Test]
	public void TheHeraldDrawsWhenAnotherElfArrives()
	{
		// **The library has to be stocked or this reports the card as inert.** CreateForTesting
		// leaves it empty, and DrawCardsAction on an empty library draws nothing — so a trigger
		// that fires perfectly still shows a hand that did not grow.
		var s = StockLibrary(_state, 3);
		(s, _) = Play(s, Card("Wirewood Herald"));
		var before = HandSize(s);

		var (after, _) = Play(s, Card("Wirewood Conduit"));

		Assert.That(HandSize(after) - before, Is.EqualTo(1));
	}

	/// <summary>
	/// The control: the Herald's trigger is filtered on Elf, so a non-Elf entering must NOT draw.
	/// Without this the test above would pass on a trigger that fired for every creature.
	/// </summary>
	[Test]
	public void TheHeraldDoesNotDrawForANonElf()
	{
		var s = StockLibrary(_state, 3);
		(s, _) = Play(s, Card("Wirewood Herald"));
		var before = HandSize(s);

		var (after, _) = Play(s, Card("Hoofthunder Colossus"));

		Assert.That(HandSize(after) - before, Is.EqualTo(0), "the Colossus is a Beast, not an Elf");
	}

	/// <summary>
	/// **The payoff that converts a wide board into a win.** Counting is what an Elf deck needs —
	/// greatest-power (Overwhelming Stampede's number) reads +1/+1 off a field of 1/1s and does
	/// nothing, which is why `CountedIdsOutputKey` had to exist.
	///
	/// Also asserts the readying, which is not decoration: the Elves were tapped for mana to cast
	/// this, so without it the team that paid for the card cannot attack.
	/// </summary>
	[Test]
	public void TheColossusReadiesPumpsAndHastesTheWholeBoard()
	{
		var (s, elfA) = Play(_state, Card("Wirewood Conduit"));
		var (s2, elfB) = Play(s, Card("Wirewood Symbiont"));

		// Tapped out, exactly as a deck that just cast a seven-drop would be.
		var tapped = Exhaust(Exhaust(s2, elfA.Id), elfB.Id);

		var (after, colossus) = Play(tapped, Card("Hoofthunder Colossus"));

		var stats = after.GetEffectiveStats(elfA.Id);

		Assert.Multiple(() =>
		{
			Assert.That(IsExhausted(after, elfA.Id), Is.False, "the board was readied");
			Assert.That(IsExhausted(after, elfB.Id), Is.False);
			// Three creatures counted, so each 1/1 Elf becomes a 4/4.
			Assert.That(stats.Power, Is.EqualTo(4), "+X/+X where X is the creature count");
			Assert.That(stats.HasHaste, Is.True);
			Assert.That(stats.HasTrample, Is.True);
			Assert.That(
				after.GetEffectiveStats(colossus.Id).Power,
				Is.EqualTo(8),
				"the Colossus counts itself — 5/5 base plus three creatures"
			);
		});
	}

	// ===== helpers =====

	private static bool IsExhausted(GameState state, int cardId) =>
		((Card)state.GetObject(cardId)).GetComponent<CreatureComponent>()!.IsExhausted;

	private int Mana(GameState state) => ((MtgPlayer)state.GetObject(_ids.Player1Id)).CurrentMana;

	private int HandSize(GameState state) => state.GetChildrenIds(_ids.Player1HandId).Count();

	private GameState StockLibrary(GameState state, int count)
	{
		for (var i = 0; i < count; i++)
			(state, _) = state.AddObject(
				new Card
				{
					Name = $"Filler {i}",
					OwnerId = _ids.Player1Id,
					ControllerId = _ids.Player1Id,
				},
				parentId: _ids.Player1LibraryId
			);
		return state;
	}

	private static GameState Exhaust(GameState state, int cardId)
	{
		var (next, _) = state
			.AddAction(new ExhaustCreatureAction { TargetIds = ImmutableList.Create(cardId) })
			.ProcessAllActions();
		return next;
	}

	private (GameState, Card) Play(GameState state, Card card)
	{
		// Through PutIntoBattlefieldAction, not AddObject: the ETB ceremony is what fires the
		// Herald's trigger, and a test that placed cards directly would report it as inert.
		var (next, _) = state
			.AddAction(
				new PutIntoBattlefieldAction
				{
					CardTemplate = card with
					{
						OwnerId = _ids.Player1Id,
						ControllerId = _ids.Player1Id,
					},
				}
			)
			.ProcessAllActions();

		var placed = next.GetCardsInZone(_ids.Player1BattlefieldId).Last(c => c.Name == card.Name);
		var creature = placed.GetComponent<CreatureComponent>();
		if (creature is not null)
			next = next.UpdateObject(
				placed.Id,
				placed.WithComponentReplaced(creature with { HasSummoningSickness = false })
			);

		return (next, (Card)next.GetObject(placed.Id));
	}

	private GameState Activate(GameState state, int sourceId, int targetId = 0)
	{
		var action = new ActivateAbilityAction
		{
			CardId = sourceId,
			AbilityIndex = 0,
			ActivatingPlayerId = _ids.Player1Id,
			TargetIds = targetId == 0 ? ImmutableList<int>.Empty : ImmutableList.Create(targetId),
		};

		var (next, _) = state.AddAction(action).ProcessAllActions();
		return next;
	}
}
