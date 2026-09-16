using System.Collections.Immutable;
using DoomCore;

namespace DoomCore.Tests;

/// <summary>
/// The permanent-doom language, verb by verb.
///
/// **Every verb is tested even though only two are used today.** `Duplicate`, `Delete` and the
/// absolute setters exist for apocalypses not yet written, and an unexercised verb that quietly
/// does nothing is indistinguishable from one that works — this codebase has lost four bugs
/// exactly that way. A doom authored against a broken verb would look authored and do nothing.
/// </summary>
public class DoomTransformTests
{
	private static RunCard Unit(string name, int power = 2, int toughness = 2, int cost = 1) =>
		new()
		{
			Name = name,
			IsUnit = true,
			Power = power,
			Toughness = toughness,
			Cost = cost,
		};

	private static RunCard Rite(string name) =>
		new()
		{
			Name = name,
			IsUnit = false,
			Cost = 1,
		};

	/// <summary>A run of the given cards, and the ids they were assigned.</summary>
	private static (Run Run, int[] Ids) RunOf(params RunCard[] cards)
	{
		var run = new Run { Life = 100, MaxLife = 100 }.WithCards(cards);
		return (run, [.. run.Deck.Select(c => c.RunCardId)]);
	}

	[Test]
	public void PerNRateLimitsWhatAMintingDoomHandsOut()
	{
		var (run, ids) = RunOf(Unit("A"), Unit("B"), Unit("C"));

		var every = new DoomTransform
		{
			Reads = FiringRead.DiedThisTurn,
			Does = TransformVerb.AddCopies,
			Template = DoomTransforms.ZombieBody,
		};

		Assert.That(
			every.Apply(run, FiringThisTurn(ids)).Deck.Count(c => c.Name == "Zombie"),
			Is.EqualTo(3),
			"one per death by default"
		);

		var everySecond = every with { PerN = 2 };
		Assert.That(
			everySecond.Apply(run, FiringThisTurn(ids)).Deck.Count(c => c.Name == "Zombie"),
			Is.EqualTo(1),
			"three deaths at one per two is one, rounded down"
		);

		Assert.That(
			everySecond.Apply(run, FiringThisTurn([ids[0]])).Deck.Count(c => c.Name == "Zombie"),
			Is.Zero,
			"a single death under a rate limit of two mints nothing at all"
		);
	}

	/// <summary>
	/// The two death windows are different lists and must not be confused. `Died` holds everything
	/// since the previous firing; `DiedThisTurn` only what the doom caught fresh.
	/// </summary>
	[Test]
	public void TheTwoDeathWindowsAreReadSeparately()
	{
		var (run, ids) = RunOf(Unit("Old"), Unit("Fresh"));

		var firing = new DoomFiring
		{
			Scenario = DoomScenario.Zombie,
			TurnNumber = 3,
			DiedRunCardIds = [.. ids],
			DiedThisTurnRunCardIds = [ids[1]],
		};

		var mint = new DoomTransform
		{
			Does = TransformVerb.AddCopies,
			Template = DoomTransforms.ZombieBody,
		};

		Assert.That(
			(mint with { Reads = FiringRead.Died })
				.Apply(run, firing)
				.Deck.Count(c => c.Name == "Zombie"),
			Is.EqualTo(2)
		);
		Assert.That(
			(mint with { Reads = FiringRead.DiedThisTurn })
				.Apply(run, firing)
				.Deck.Count(c => c.Name == "Zombie"),
			Is.EqualTo(1),
			"the narrow window caught only the fresh one"
		);
	}

	private static DoomFiring FiringThisTurn(IEnumerable<int> died) =>
		new()
		{
			Scenario = DoomScenario.Zombie,
			TurnNumber = 1,
			DiedThisTurnRunCardIds = [.. died],
		};

	private static DoomFiring Firing(
		IEnumerable<int>? standing = null,
		IEnumerable<int>? died = null,
		IEnumerable<int>? summoned = null
	) =>
		new()
		{
			Scenario = DoomScenario.Nuclear,
			TurnNumber = 1,
			OnFieldRunCardIds = (standing ?? []).ToImmutableHashSet(),
			DiedRunCardIds = (died ?? []).ToImmutableList(),
			SummonedRunCardIds = (summoned ?? []).ToImmutableHashSet(),
		};

	[Test]
	public void DuplicateAddsAFreshCopyOfEachCardRead()
	{
		var (run, ids) = RunOf(Unit("Kept"), Unit("Left"));

		var after = new DoomTransform
		{
			Reads = FiringRead.Summoned,
			Does = TransformVerb.Duplicate,
		}.Apply(run, Firing(summoned: [ids[0]]));

		Assert.That(
			after.Deck.Count(c => c.Name == "Kept"),
			Is.EqualTo(2),
			"it should have doubled"
		);
		Assert.That(after.Deck.Count(c => c.Name == "Left"), Is.EqualTo(1), "and left the other");

		// A duplicate is a NEW deck entry. Sharing an id would make the next doom read one card
		// and rewrite two, which is the bug this assertion exists to catch.
		Assert.That(after.Deck.Select(c => c.RunCardId), Is.Unique);
	}

	[Test]
	public void DeleteRemovesOnlyWhatItRead()
	{
		var (run, ids) = RunOf(Unit("Committed"), Unit("Abandoned"));

		var after = new DoomTransform
		{
			Reads = FiringRead.NeverSummoned,
			Does = TransformVerb.Delete,
		}.Apply(run, Firing(summoned: [ids[0]]));

		Assert.That(after.Deck.Select(c => c.Name), Is.EquivalentTo(new[] { "Committed" }));
	}

	/// <summary>
	/// The trap in `NeverSummoned`: a rite is NEVER summoned, so a Delete that ignored
	/// <see cref="DoomTransform.UnitsOnly"/> would eat the whole non-unit half of the deck the
	/// first time any apocalypse fired.
	/// </summary>
	[Test]
	public void UnitsOnlyKeepsRitesOutOfAReadThatWouldOtherwiseTakeThem()
	{
		var (run, _) = RunOf(Unit("Body"), Rite("Field Dressing"));

		var greedy = new DoomTransform
		{
			Reads = FiringRead.NeverSummoned,
			Does = TransformVerb.Delete,
			UnitsOnly = true,
		}.Apply(run, Firing());

		Assert.That(
			greedy.Deck.Select(c => c.Name),
			Is.EquivalentTo(new[] { "Field Dressing" }),
			"the flood takes the living"
		);

		var indiscriminate = new DoomTransform
		{
			Reads = FiringRead.NeverSummoned,
			Does = TransformVerb.Delete,
			UnitsOnly = false,
		}.Apply(run, Firing());

		Assert.That(
			indiscriminate.Deck,
			Is.Empty,
			"and UnitsOnly false really does take everything"
		);
	}

	[Test]
	public void AbsoluteSettersOverrideStatsRatherThanAdjustingThem()
	{
		var (run, ids) = RunOf(Unit("Veteran", power: 9, toughness: 7, cost: 3));

		var after = new DoomTransform
		{
			Reads = FiringRead.Standing,
			Does = TransformVerb.Modify,
			SetPower = 6,
			SetToughness = 6,
			SetCost = 0,
			Tag = "Assimilated",
		}.Apply(run, Firing(standing: ids));

		var card = after.Deck.Single();
		Assert.That((card.Power, card.Toughness, card.Cost), Is.EqualTo((6, 6, 0)));
		Assert.That(card.HasTag("Assimilated"), Is.True);
	}

	[Test]
	public void ModifyNeverDrivesAUnitBelowOneToughness()
	{
		var (run, ids) = RunOf(Unit("Frail", power: 1, toughness: 2));

		var after = new DoomTransform
		{
			Reads = FiringRead.Standing,
			Does = TransformVerb.Modify,
			PowerDelta = -9,
			ToughnessDelta = -9,
		}.Apply(run, Firing(standing: ids));

		var card = after.Deck.Single();
		Assert.That(card.Power, Is.Zero, "0 power is a legal body — Bulwark is one");
		Assert.That(
			card.Toughness,
			Is.GreaterThan(0),
			"a 0-toughness card is dead on arrival and would be unplayable forever"
		);
	}

	/// <summary>
	/// The ordering hazard, made concrete. Duplicates are minted with new ids, so they fall outside
	/// the read a later Delete was given — run the same two transforms the other way round and the
	/// deck ends up completely different.
	/// </summary>
	[Test]
	public void OrderWithinAScenarioChangesTheResult()
	{
		var (run, ids) = RunOf(Unit("Committed"), Unit("Abandoned"));
		var firing = Firing(summoned: [ids[0]]);

		var delete = new DoomTransform
		{
			Reads = FiringRead.NeverSummoned,
			Does = TransformVerb.Delete,
		};
		var duplicate = new DoomTransform
		{
			Reads = FiringRead.Summoned,
			Does = TransformVerb.Duplicate,
		};

		var deleteFirst = duplicate.Apply(delete.Apply(run, firing), firing);
		var duplicateFirst = delete.Apply(duplicate.Apply(run, firing), firing);

		Assert.That(deleteFirst.Deck.Count, Is.EqualTo(2), "Committed, twice");

		// One card, not two: the copy is minted with a NEW id, so the later Delete — which reads
		// "everything not in the summoned set" — takes the copy as well as the abandoned card.
		// Getting this order wrong turns a doom that doubles your deck into one that guts it.
		Assert.That(
			duplicateFirst.Deck.Count,
			Is.EqualTo(1),
			"the copy was minted outside the read and the Delete took it straight back out"
		);
		Assert.That(
			deleteFirst.Deck.Select(c => c.Name),
			Is.EquivalentTo(new[] { "Committed", "Committed" })
		);
	}

	/// <summary>
	/// Zombie reads a LIST, so a card that died twice pays twice. If the read were treated as a set
	/// anywhere in the language this would silently halve the apocalypse.
	/// </summary>
	[Test]
	public void DiedIsCountedPerDeathNotPerCard()
	{
		var (run, ids) = RunOf(Unit("Recurring"));

		var after = new DoomTransform
		{
			Reads = FiringRead.Died,
			Does = TransformVerb.AddCopies,
			Template = DoomTransforms.ZombieBody,
		}.Apply(run, Firing(died: [ids[0], ids[0]]));

		Assert.That(after.Deck.Count(c => c.Name == "Zombie"), Is.EqualTo(2));
	}
}
