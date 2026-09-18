using System.Collections.Immutable;
using ImmutableGameObjects;

namespace DoomCore.Tests;

/// <summary>
/// TAG ALONG. The companion is the one thing no apocalypse can take, and the one thing that
/// remembers every apocalypse it survived.
/// </summary>
public class CompanionTests
{
	private static (GameState State, ImmutableList<GameEvent> Events) Do(
		GameState state,
		GameAction action
	) => state.AddAction(action).ProcessAllActions();

	private static Run RunWith(Companion companion, params RunCard[] deck) =>
		new Run
		{
			Life = 60,
			MaxLife = 60,
			Companion = companion,
		}.WithCards(deck);

	private static Companion Dog =>
		new()
		{
			Name = "Ash",
			BasePower = 1,
			BaseToughness = 3,
		};

	private static RunCard Unit(string name, int power, int toughness, int cost = 0) =>
		new()
		{
			Name = name,
			Cost = cost,
			IsUnit = true,
			Power = power,
			Toughness = toughness,
		};

	/// <summary>
	/// Defaults to the COMPANION'S lane, because every test here is about the companion. An enemy
	/// parked anywhere else would never meet it and the test would pass by not happening.
	/// </summary>
	private static Enemy Enemy(int attack, int health = 50, int lane = DoomBattle.LaneCount / 2) =>
		new()
		{
			Name = attack > 0 ? "Wretch" : "Idler",
			Health = health,
			MaxHealth = health,
			Intent = attack > 0 ? IntentKind.Attack : IntentKind.Wait,
			IntentAmount = attack,
			Lane = lane,
		};

	/// <summary>
	/// Ends turns until the doom has fired once.
	///
	/// NOT "until the battle is over" — the doom recurs and a battle only ends when the Opponent
	/// dies, so that loop never terminates. The cap turns a rules regression into a failed test
	/// rather than a hung suite, which is how this was found in the first place.
	/// </summary>
	private static GameState PlayOut(GameState state, int firings = 1)
	{
		for (var turn = 0; turn < 50; turn++)
		{
			if (state.GetBattle().DoomsFired >= firings || state.GetBattle().IsOver)
				return state;

			(state, _) = Do(state, new EndTurnAction());
		}

		throw new InvalidOperationException(
			$"50 turns without {firings} doom firing(s) — the countdown is not resetting."
		);
	}

	[Test]
	public void TheCompanionIsOnTheFieldFromTheFirstTurnWithoutBeingPlayed()
	{
		var run = RunWith(Dog, Unit("Filler", 1, 1));
		var (state, _) = run.StartBattle(DoomScenario.Zombie, countdown: 3, [Enemy(0)]);

		var companion = state.Units().Single(u => u.HasComponent<CompanionComponent>());

		Assert.That(companion.Name, Does.StartWith("Ash"));
		Assert.That(companion.Unit().Power, Is.EqualTo(Dog.BasePower));
		Assert.That(companion.Unit().Toughness, Is.EqualTo(Dog.BaseToughness));
		Assert.That(state.CardsIn(ZoneType.Hand).Any(c => c.Name.StartsWith("Ash")), Is.False);
	}

	// ===== The doom cannot touch it =====

	/// <summary>
	/// Flood sweeps every unit off the board. The companion stays: it is not a card and has nowhere
	/// to be discarded TO — sending it to Discard would make it drawable.
	/// </summary>
	// **Six tests lived here and were cut with the mechanic they described (2026-09-18):** marks
	// accumulating, being carried into the next battle's stat line, and being collapsed in the
	// companion's name. Every apocalypse survived used to stamp a permanent +N/+N on the companion.
	// It was cut on playtest feedback — "I never liked this mechanic" — and it was a stat trickle
	// nobody chose attached to a name that grew until it had to be collapsed to stay on screen.
	//
	// What SURVIVES below is everything about the companion that was never about marks: it is on the
	// board free, no transform can touch it, and its death is not a deck event.

	[Test]
	public void FloodCannotWashAwayTheCompanion()
	{
		var run = RunWith(Dog, Unit("Hoarded", 1, 1));

		var (state, _) = run.StartBattle(DoomScenario.Flood, countdown: 2, [Enemy(0)]);
		state = PlayOut(state);

		Assert.That(state.GetBattle().DoomsFired, Is.EqualTo(1), "it fired");
		Assert.That(
			state.Units().Any(u => u.HasComponent<CompanionComponent>()),
			Is.True,
			"the water took the board; it did not take Ash"
		);

		var after = run.AfterBattle(state);
		Assert.That(after.Companion.Name, Is.EqualTo("Ash"));
		Assert.That(after.Companion.BaseToughness, Is.EqualTo(Dog.BaseToughness));
	}

	[Test]
	public void NuclearCannotIrradiateTheCompanion()
	{
		var run = RunWith(Dog, Unit("Exposed", 2, 2));

		var (state, _) = run.StartBattle(DoomScenario.Nuclear, countdown: 2, [Enemy(0)]);
		var after = run.AfterBattle(PlayOut(state));

		// The companion was on the field the whole time — exactly what Nuclear reads — and it must
		// still be untouched by it. **This is the assertion that mattered all along**: the mark it
		// used to take instead was the decoration on top.
		Assert.That(after.Deck.Any(c => c.HasTag(DoomTransforms.IrradiatedTag)), Is.False);
		Assert.That(
			after.Companion.Power,
			Is.EqualTo(Dog.BasePower),
			"the apocalypse changed the companion's stats, which nothing may do"
		);
	}

	[Test]
	public void ADeadCompanionDoesNotFeedZombieAndReturnsNextBattle()
	{
		var run = RunWith(Dog, Unit("Filler", 1, 1));

		// 9 damage against a 1/3 companion blocking: it dies every time.
		var (state, _) = run.StartBattle(DoomScenario.Zombie, countdown: 2, [Enemy(9)]);

		var companion = state.Units().Single(u => u.HasComponent<CompanionComponent>());

		(state, var events) = Do(state, new EndTurnAction());

		Assert.That(events.OfType<UnitDiedEvent>().Any(e => e.CardName.StartsWith("Ash")), Is.True);
		Assert.That(
			state.GetBattle().DiedRunCardIds,
			Is.Empty,
			"a companion death is not a deck event"
		);
		Assert.That(state.HasObject(companion.Id), Is.False, "it left the battle, not to Discard");

		var after = run.AfterBattle(PlayOut(state));

		Assert.That(
			after.Deck.Any(c => c.Name == "Zombie"),
			Is.False,
			"no free Zombie from it dying"
		);

		var (next, _) = after.StartBattle(DoomScenario.Zombie, countdown: 2, [Enemy(0)]);
		Assert.That(
			next.Units().Any(u => u.HasComponent<CompanionComponent>()),
			Is.True,
			"it comes back"
		);
	}

	[Test]
	public void TheCompanionBlocksLikeAnyUnitSoABadDrawIsNeverAnEmptyBoard()
	{
		var run = RunWith(Dog);
		Assert.That(run.Deck, Is.Empty, "no cards at all");

		var (state, _) = run.StartBattle(DoomScenario.Zombie, countdown: 3, [Enemy(5)]);

		var companion = state.Units().Single(u => u.HasComponent<CompanionComponent>());
		(state, _) = Do(state, new EndTurnAction());

		Assert.That(state.GetPlayer().Life, Is.EqualTo(58), "3 toughness absorbed 3 of 5");
	}
}
