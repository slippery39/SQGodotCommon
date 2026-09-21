using System.Collections.Immutable;
using ImmutableGameObjects;

namespace KinCore.Tests;

/// <summary>
/// The run layer: a deck and a life total that outlive any single battle, and the doom transforms
/// that rewrite them. Cards are defined inline, never from a library.
/// </summary>
public class RunTests
{
	private static (GameState State, ImmutableList<GameEvent> Events) Do(
		GameState state,
		GameAction action
	) => state.AddAction(action).ProcessAllActions();

	private static RunCard Unit(string name, int power, int toughness, int cost = 1) =>
		new()
		{
			Name = name,
			Cost = cost,
			IsUnit = true,
			Power = power,
			Toughness = toughness,
		};

	private static Enemy Idler(int health = 50) =>
		new()
		{
			Name = "Idler",
			Health = health,
			MaxHealth = health,
			Intent = IntentKind.Wait,
		};

	/// <summary>
	/// Plays every unit it can afford, then ends the turn, for TWO turns.
	///
	/// **Two is not arbitrary and is not a timeout.** These tests used to run until the doom had
	/// fired once, with every caller passing `countdown: 2` — so the loop always ran exactly twice
	/// and the assertions ("4 damage on each of 2 turns") were written against that. The doom is
	/// gone; the number it stood for is stated directly.
	/// </summary>
	private static GameState PlayOutBattle(GameState state, params int[] runCardIdsToPlay)
	{
		var wanted = runCardIdsToPlay.ToHashSet();

		for (var turn = 0; turn < 2 && !state.GetBattle().IsOver; turn++)
		{
			if (state.GetBattle().IsOver)
				return state;

			foreach (var card in state.CardsIn(ZoneType.Hand).ToList())
			{
				if (!wanted.Contains(card.RunCardId))
					continue;

				// First open lane. Which lane is not what these tests are about — they care that
				// the unit reached the Field at all.
				var lane = state.OpenLanes().FirstOrDefault(-1);
				if (lane < 0)
					continue;

				var (next, ok) = state.TryAddAction(
					new PlayCardAction { CardId = card.Id, Lane = lane }
				);
				if (ok)
					(state, _) = next.ProcessAllActions();
			}

			(state, _) = Do(state, new EndTurnAction());
		}

		return state;
	}

	// ===== The run outlives the battle =====

	[Test]
	public void LifeCarriesOutOfABattleAndDoesNotHealBetweenThem()
	{
		var run = new Run { Life = 30, MaxLife = 30 }.WithCards([Unit("Stray", 1, 1)]);

		var enemy = new Enemy
		{
			Name = "Biter",
			Health = 50,
			MaxHealth = 50,
			Intent = IntentKind.Attack,
			IntentAmount = 4,
		};

		(var state, _) = run.StartBattle([enemy]);
		state = PlayOutBattle(state);

		var after = run.AfterBattle(state);

		Assert.That(after.Life, Is.EqualTo(22), "4 damage on each of 2 turns, no healing");
		Assert.That(after.Floor, Is.EqualTo(2));
	}

	[Test]
	public void ADeckBuiltIntoABattleKeepsItsRunCardIds()
	{
		var run = new Run().WithCards([Unit("A", 1, 1), Unit("B", 2, 2)]);
		(var state, _) = run.StartBattle([Idler()]);

		var ids = state
			.CardsIn(ZoneType.Draw)
			.Concat(state.CardsIn(ZoneType.Hand))
			.Select(c => c.RunCardId)
			.OrderBy(x => x);

		Assert.That(ids, Is.EqualTo(new[] { 1, 2 }));
	}

	// ===== Flood: now a BATTLE-scope board wash =====

	/// <summary>
	/// Flood TAKES what is standing and leaves the RUN deck alone. It used to delete never-summoned
	/// units from the run and duplicate the committed ones; permanent removal caused more trouble
	/// than it was worth (see KinJam.md). It then washed the board to Discard, which combat v3
	/// turned into a no-op — so it now takes the CARD out of circulation for two turns, inside the
	/// battle, and the run deck is still none of its business.
	/// </summary>

	[Test]
	public void ATakenUnitComesBackWholeRatherThanDamaged()
	{
		var run = new Run { Life = 100, MaxLife = 100 }.WithCards(
			[Unit("Committed", 1, 4, cost: 0)]
		);
		var committedId = run.Deck[0].RunCardId;

		// An enemy that chips it before the water takes it.
		var biter = new Enemy
		{
			Name = "Biter",
			Health = 500,
			MaxHealth = 500,
			Intent = IntentKind.Attack,
			IntentAmount = 1,
			Lane = 0,
		};

		(var state, _) = run.StartBattle(
			[biter],
			opponentHealth: 500
		);
		state = PlayOutBattle(state, committedId);

		// Taken is searched too: the water now holds the card for two turns before handing it back,
		// and this test is about what the card is like when it RETURNS TO PLAY, not about which
		// pile it waited in.
		var washed = state
			.CardsIn(ZoneType.Discard)
			.Concat(state.CardsIn(ZoneType.Draw))
			.Concat(state.CardsIn(ZoneType.Hand))
			.Concat(state.CardsIn(ZoneType.Taken))
			.FirstOrDefault(c => c.RunCardId == committedId);

		Assert.That(washed, Is.Not.Null, "it left the field");

		// Asserted where it MATTERS — back in play — rather than on the card sitting in Discard.
		// Damage is cleared by PlayCardAction, at the single point every board unit enters through.
		// It used to be cleared here on the way out instead, which left the death path never doing
		// it: a unit that died once arrived back on the Field already dead and invisible.
		state = state.MoveObject(washed!.Id, state.ZoneId(ZoneType.Hand));
		var lane = state.OpenLanes().First();
		(state, _) = state
			.AddAction(new PlayCardAction { CardId = washed.Id, Lane = lane })
			.ProcessAllActions();

		var replayed = state.UnitInLane(lane);
		Assert.That(replayed, Is.Not.Null, "it must actually reach the lane");
		Assert.That(
			replayed!.Unit().Damage,
			Is.Zero,
			"what returns from Discard is the card, not the body that stood in the lane"
		);
	}

	// ===== Guards =====

	[Test]
	public void ADeadPlayerDoesNotAdvanceAFloor()
	{
		var run = new Run { Life = 3, MaxLife = 3 }.WithCards([Unit("Doomed", 1, 1, cost: 0)]);

		var enemy = new Enemy
		{
			Name = "Executioner",
			Health = 50,
			MaxHealth = 50,
			Intent = IntentKind.Attack,
			IntentAmount = 3,
		};

		(var state, _) = run.StartBattle([enemy]);
		state = PlayOutBattle(state);

		var after = run.AfterBattle(state);

		Assert.That(after.IsOver, Is.True);
		Assert.That(after.Floor, Is.EqualTo(1), "the run stopped where it died");
		Assert.That(after.Deck.Count, Is.EqualTo(1), "Flood never ran");
	}

	// ===== A run has to be able to END =====

	[Test]
	public void AnEmptyDeckEndsTheRunRatherThanLeavingItUnwinnable()
	{
		// NOTHING CURRENTLY EMPTIES A DECK. Flood used to, and was the only thing that did; it is a
		// board wash now. The rule is kept because it is the floor under any future scenario that
		// removes cards, so it is asserted directly rather than through a doom that cannot cause it.
		var after = new Run { Life = 50, MaxLife = 50 };

		Assert.That(after.Deck, Is.Empty);
		Assert.That(after.IsDead, Is.False, "still alive, but with nothing to play");
		Assert.That(after.IsOver, Is.True);
		Assert.That(after.OverReason, Does.Contain("deck"));
	}

	/// <summary>
	/// **The RUN ends after the last floor of the LAST act.** A run used to be one act; it is all
	/// three now, so clearing floor 15 finishes act 1 and walks you into act 2. `IsActComplete`
	/// keeps its name and means "there is no floor below this one" — what every caller already
	/// wanted it for.
	/// </summary>
	[Test]
	public void TheRunEndsAfterTheLastFloorOfTheLastAct()
	{
		var run = new Run { Floor = Run.RunLength }.WithCards([Unit("Survivor", 1, 1, cost: 0)]);
		var survivorId = run.Deck[0].RunCardId;

		(var state, _) = run.StartBattle([Idler()]);
		state = PlayOutBattle(state, survivorId);

		var after = run.AfterBattle(state);

		Assert.That(after.Floor, Is.EqualTo(Run.RunLength + 1));
		Assert.That(after.IsActComplete, Is.True);
		Assert.That(after.IsOver, Is.True);
		Assert.That(after.IsDead, Is.False, "finishing a run is not dying");
	}
}
