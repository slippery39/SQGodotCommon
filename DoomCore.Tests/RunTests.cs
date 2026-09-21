using System.Collections.Immutable;
using ImmutableGameObjects;

namespace DoomCore.Tests;

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
	/// Plays every unit it can afford, then ends the turn, until the doom has fired ONCE.
	///
	/// Not "until the battle is over": the doom recurs now, and a battle ends only when the Opponent
	/// dies. The turn cap makes a countdown that stops resetting fail loudly instead of hanging.
	/// </summary>
	private static GameState PlayOutBattle(GameState state, params int[] runCardIdsToPlay)
	{
		var wanted = runCardIdsToPlay.ToHashSet();

		for (var turn = 0; turn < 50; turn++)
		{
			if (state.GetBattle().DoomsFired >= 1 || state.GetBattle().IsOver)
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

		throw new InvalidOperationException("50 turns without a doom firing.");
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

		(var state, _) = run.StartBattle(DoomScenario.Zombie, countdown: 2, [enemy]);
		state = PlayOutBattle(state);

		var after = run.AfterBattle(state);

		Assert.That(after.Life, Is.EqualTo(22), "4 damage on each of 2 turns, no healing");
		Assert.That(after.Floor, Is.EqualTo(2));
	}

	[Test]
	public void ADeckBuiltIntoABattleKeepsItsRunCardIds()
	{
		var run = new Run().WithCards([Unit("A", 1, 1), Unit("B", 2, 2)]);
		(var state, _) = run.StartBattle(DoomScenario.Flood, countdown: 3, [Idler()]);

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
	/// than it was worth (see DoomJam.md). It then washed the board to Discard, which combat v3
	/// turned into a no-op — so it now takes the CARD out of circulation for two turns, inside the
	/// battle, and the run deck is still none of its business.
	/// </summary>
	[Test]
	public void FloodTakesTheBoardAndLeavesTheRunDeckUntouched()
	{
		var run = new Run { Life = 100, MaxLife = 100 }.WithCards(
			[Unit("Committed", 1, 1, cost: 0), Unit("Hoarded", 1, 1, cost: 0)]
		);
		var committedId = run.Deck[0].RunCardId;

		(var state, _) = run.StartBattle(
			DoomScenario.Flood,
			countdown: 2,
			[Idler()],
			opponentHealth: 500
		);
		state = PlayOutBattle(state, committedId);

		Assert.That(state.GetBattle().DoomsFired, Is.EqualTo(1), "it fired");
		Assert.That(state.Units().Any(u => u.Name == "Committed"), Is.False, "and took the board");
		Assert.That(
			state.CardsIn(ZoneType.Taken).Any(c => c.Name == "Committed"),
			Is.True,
			"TAKEN, not washed to Discard — the wash is what v3 made meaningless"
		);

		var after = run.AfterBattle(state);

		Assert.That(after.Deck.Count, Is.EqualTo(2), "both cards are still in the run");
		Assert.That(
			after.Deck.Select(c => c.Name).OrderBy(n => n),
			Is.EqualTo(new[] { "Committed", "Hoarded" }),
			"a battle doom costs tempo, not material"
		);
	}

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
			DoomScenario.Flood,
			countdown: 2,
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

	// ===== Nuclear =====

	[Test]
	public void NuclearIrradiatesWhatWasLeftOnTheFieldAndThatCardThenCostsLifeToDraw()
	{
		var run = new Run { Life = 40, MaxLife = 40 }.WithCards([Unit("Exposed", 3, 3, cost: 0)]);
		var exposedId = run.Deck[0].RunCardId;

		(var state, _) = run.StartBattle(DoomScenario.Nuclear, countdown: 2, [Idler()]);
		state = PlayOutBattle(state, exposedId);

		var after = run.AfterBattle(state);
		var card = after.Deck.Single();

		Assert.That(
			card.Power,
			Is.EqualTo(3 + DoomTransforms.IrradiatedBuff),
			"irradiated by the authored amount"
		);
		Assert.That(card.Toughness, Is.EqualTo(3 + DoomTransforms.IrradiatedBuff));
		Assert.That(card.HasTag(DoomTransforms.IrradiatedTag), Is.True);

		// The price is paid on the DRAW, so declining to play it does not dodge it.
		var (started, events) = after.StartBattle(DoomScenario.Zombie, countdown: 3, [Idler()]);

		Assert.That(events.OfType<IrradiatedDrawnEvent>().Count(), Is.EqualTo(1));
		Assert.That(
			started.GetPlayer().Life,
			Is.EqualTo(after.Life - DoomTransforms.IrradiatedDrawCost)
		);
	}

	[Test]
	public void NuclearSparesWhatWasNotOnTheFieldAtTheEnd()
	{
		var run = new Run { Life = 40, MaxLife = 40 }.WithCards([Unit("Sheltered", 3, 3, cost: 0)]);

		(var state, _) = run.StartBattle(DoomScenario.Nuclear, countdown: 2, [Idler()]);
		state = PlayOutBattle(state);

		var after = run.AfterBattle(state);

		Assert.That(after.Deck.Single().Power, Is.EqualTo(3), "never played, never irradiated");
		Assert.That(after.Deck.Single().HasTag(DoomTransforms.IrradiatedTag), Is.False);
	}

	// ===== Guards =====

	[Test]
	public void RaptureThrowsRatherThanSilentlyDoingNothing()
	{
		var run = new Run().WithCards([Unit("Offering", 1, 1)]);
		(var state, _) = run.StartBattle(DoomScenario.Rapture, countdown: 1, [Idler()]);
		state = PlayOutBattle(state);

		Assert.Throws<NotSupportedException>(() => run.AfterBattle(state));
	}

	[Test]
	public void ADeadPlayerTakesNoDoomTransformAndDoesNotAdvanceAFloor()
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

		(var state, _) = run.StartBattle(DoomScenario.Flood, countdown: 3, [enemy]);
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

		(var state, _) = run.StartBattle(DoomScenario.Flood, countdown: 2, [Idler()]);
		state = PlayOutBattle(state, survivorId);

		var after = run.AfterBattle(state);

		Assert.That(after.Floor, Is.EqualTo(Run.RunLength + 1));
		Assert.That(after.IsActComplete, Is.True);
		Assert.That(after.IsOver, Is.True);
		Assert.That(after.IsDead, Is.False, "finishing a run is not dying");
	}
}
