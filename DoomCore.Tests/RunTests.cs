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

	/// <summary>Plays every unit it can afford, then ends the turn, until the doom resolves.</summary>
	private static GameState PlayOutBattle(GameState state, params int[] runCardIdsToPlay)
	{
		var wanted = runCardIdsToPlay.ToHashSet();

		while (!state.GetBattle().IsOver)
		{
			foreach (var card in state.CardsIn(ZoneType.Hand).ToList())
			{
				if (!wanted.Contains(card.RunCardId))
					continue;

				var (next, ok) = state.TryAddAction(new PlayCardAction { CardId = card.Id });
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

	// ===== Flood: the flagship =====

	[Test]
	public void FloodDuplicatesWhatYouSummonedAndDeletesWhatYouDidNot()
	{
		var run = new Run().WithCards(
			[Unit("Committed", 1, 1, cost: 0), Unit("Hoarded", 1, 1, cost: 0)]
		);
		var committedId = run.Deck[0].RunCardId;

		(var state, _) = run.StartBattle(DoomScenario.Flood, countdown: 2, [Idler()]);
		state = PlayOutBattle(state, committedId);

		var after = run.AfterBattle(state);

		Assert.That(after.Deck.Count, Is.EqualTo(2), "one survivor, duplicated");
		Assert.That(after.Deck.Select(c => c.Name), Is.All.EqualTo("Committed"));
		Assert.That(
			after.Deck.Select(c => c.RunCardId).Distinct().Count(),
			Is.EqualTo(2),
			"the duplicate is a NEW deck entry, not a shared id"
		);
	}

	[Test]
	public void FloodLeavesNonUnitsAlone()
	{
		var run = new Run().WithCards(
			[
				Unit("Drowned", 1, 1, cost: 0),
				new RunCard
				{
					Name = "Rite",
					Cost = 0,
					IsUnit = false,
				},
			]
		);

		(var state, _) = run.StartBattle(DoomScenario.Flood, countdown: 2, [Idler()]);
		state = PlayOutBattle(state);

		var after = run.AfterBattle(state);

		Assert.That(after.Deck.Count, Is.EqualTo(1), "the unit drowned, the rite did not");
		Assert.That(after.Deck.Single().Name, Is.EqualTo("Rite"));
	}

	// ===== Zombie =====

	[Test]
	public void ZombiePaysPerDeathNotPerCard()
	{
		var run = new Run { Life = 40, MaxLife = 40 }.WithCards([Unit("Fragile", 0, 1, cost: 0)]);

		var enemy = new Enemy
		{
			Name = "Crusher",
			Health = 50,
			MaxHealth = 50,
			Intent = IntentKind.Attack,
			IntentAmount = 3,
		};

		(var state, _) = run.StartBattle(DoomScenario.Zombie, countdown: 3, [enemy]);

		// Block with Fragile every turn it is in hand — it dies, cycles back, and dies again.
		while (!state.GetBattle().IsOver)
		{
			var inHand = state.CardsIn(ZoneType.Hand).FirstOrDefault(c => c.Name == "Fragile");
			if (inHand is not null)
			{
				(state, _) = Do(state, new PlayCardAction { CardId = inHand.Id });
				var unit = state.Units().First(u => u.Name == "Fragile");
				(state, _) = Do(
					state,
					new AssignAction
					{
						UnitId = unit.Id,
						EnemyId = state.LivingEnemies().First().Id,
						Assignment = Assignment.Block,
					}
				);
			}

			(state, _) = Do(state, new EndTurnAction());
		}

		var deaths = state.GetBattle().DiedRunCardIds.Count;
		var after = run.AfterBattle(state);

		Assert.That(deaths, Is.GreaterThan(0), "it should have died at least once");
		Assert.That(after.Deck.Count(c => c.Name == "Zombie"), Is.EqualTo(deaths));
		Assert.That(
			after.Deck.Where(c => c.Name == "Zombie"),
			Is.All.Matches<RunCard>(z => z.Power == 1)
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

		Assert.That(card.Power, Is.EqualTo(5), "+2/+2");
		Assert.That(card.Toughness, Is.EqualTo(5));
		Assert.That(card.HasTag(DoomTransforms.IrradiatedTag), Is.True);

		// The price is paid on the DRAW, so declining to play it does not dodge it.
		var (started, events) = after.StartBattle(DoomScenario.Zombie, countdown: 3, [Idler()]);

		Assert.That(events.OfType<IrradiatedDrawnEvent>().Count(), Is.EqualTo(1));
		Assert.That(started.GetPlayer().Life, Is.EqualTo(after.Life - 1));
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
		var run = new Run { Life = 50, MaxLife = 50 }.WithCards([Unit("Hoarded", 1, 1, cost: 0)]);

		// Commit nothing to a Flood and it takes everything.
		(var state, _) = run.StartBattle(DoomScenario.Flood, countdown: 2, [Idler()]);
		state = PlayOutBattle(state);

		var after = run.AfterBattle(state);

		Assert.That(after.Deck, Is.Empty);
		Assert.That(after.IsDead, Is.False, "still alive, but with nothing to play");
		Assert.That(after.IsOver, Is.True);
		Assert.That(after.OverReason, Does.Contain("deck"));
	}

	[Test]
	public void TheActEndsAfterItsLastFloor()
	{
		var run = new Run { Floor = Run.ActLength }.WithCards([Unit("Survivor", 1, 1, cost: 0)]);
		var survivorId = run.Deck[0].RunCardId;

		(var state, _) = run.StartBattle(DoomScenario.Flood, countdown: 2, [Idler()]);
		state = PlayOutBattle(state, survivorId);

		var after = run.AfterBattle(state);

		Assert.That(after.Floor, Is.EqualTo(Run.ActLength + 1));
		Assert.That(after.IsActComplete, Is.True);
		Assert.That(after.IsOver, Is.True);
		Assert.That(after.IsDead, Is.False, "finishing an act is not dying");
	}
}
