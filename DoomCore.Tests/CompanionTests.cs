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

	private static Enemy Enemy(int attack, int health = 50) =>
		new()
		{
			Name = attack > 0 ? "Wretch" : "Idler",
			Health = health,
			MaxHealth = health,
			Intent = attack > 0 ? IntentKind.Attack : IntentKind.Wait,
			IntentAmount = attack,
		};

	private static GameState PlayOut(GameState state)
	{
		while (!state.GetBattle().IsOver)
			(state, _) = Do(state, new EndTurnAction());
		return state;
	}

	[Test]
	public void TheCompanionIsOnTheFieldFromTheFirstTurnWithoutBeingPlayed()
	{
		var run = RunWith(Dog, Unit("Filler", 1, 1));
		var (state, _) = run.StartBattle(DoomScenario.Zombie, countdown: 3, [Enemy(0)]);

		var companion = state.Units().Single(u => u.HasComponent<CompanionComponent>());

		Assert.That(companion.Name, Does.StartWith("Ash"));
		Assert.That(companion.Unit().Power, Is.EqualTo(1));
		Assert.That(companion.Unit().Toughness, Is.EqualTo(3));
		Assert.That(state.CardsIn(ZoneType.Hand).Any(c => c.Name.StartsWith("Ash")), Is.False);
	}

	[Test]
	public void SurvivingAnApocalypseLeavesAPermanentMark()
	{
		var run = RunWith(Dog, Unit("Filler", 1, 1));

		var (state, _) = run.StartBattle(DoomScenario.Nuclear, countdown: 2, [Enemy(0)]);
		var after = run.AfterBattle(PlayOut(state));

		Assert.That(after.Companion.Marks.Select(m => m.Name), Is.EqualTo(new[] { "Glowing" }));
		Assert.That(after.Companion.Power, Is.EqualTo(3), "base 1 +2 from Glowing");
		Assert.That(after.Companion.Toughness, Is.EqualTo(3), "unchanged");
		Assert.That(after.Companion.FullName, Is.EqualTo("Ash — Glowing"));
	}

	[Test]
	public void MarksAccumulateAcrossAWholeRun()
	{
		var run = RunWith(Dog, Unit("Filler", 1, 1));

		foreach (
			var scenario in new[] { DoomScenario.Nuclear, DoomScenario.Zombie, DoomScenario.Zombie }
		)
		{
			var (state, _) = run.StartBattle(scenario, countdown: 2, [Enemy(0)]);
			run = run.AfterBattle(PlayOut(state));
		}

		Assert.That(run.Companion.Marks.Count, Is.EqualTo(3));
		Assert.That(run.Companion.Power, Is.EqualTo(3), "1 base + 2 Glowing");
		Assert.That(run.Companion.Toughness, Is.EqualTo(7), "3 base + 2 + 2 Gravemarked");
		Assert.That(run.Companion.FullName, Does.Contain("Glowing").And.Contain("Gravemarked"));
	}

	[Test]
	public void TheMarksAreCarriedIntoTheNextBattlesStatline()
	{
		var run = RunWith(Dog, Unit("Filler", 1, 1));

		var (first, _) = run.StartBattle(DoomScenario.Nuclear, countdown: 2, [Enemy(0)]);
		run = run.AfterBattle(PlayOut(first));

		var (second, _) = run.StartBattle(DoomScenario.Zombie, countdown: 2, [Enemy(0)]);
		var companion = second.Units().Single(u => u.HasComponent<CompanionComponent>());

		Assert.That(companion.Unit().Power, Is.EqualTo(3), "it walked out of the last one changed");
	}

	// ===== The doom cannot touch it =====

	[Test]
	public void FloodCannotDrownTheCompanion()
	{
		var run = RunWith(Dog, Unit("Hoarded", 1, 1));

		var (state, _) = run.StartBattle(DoomScenario.Flood, countdown: 2, [Enemy(0)]);
		var after = run.AfterBattle(PlayOut(state));

		Assert.That(after.Deck, Is.Empty, "the deck drowned");
		Assert.That(after.Companion.Name, Is.EqualTo("Ash"), "the companion did not");
		Assert.That(after.Companion.BaseToughness, Is.EqualTo(3));
	}

	[Test]
	public void NuclearCannotIrradiateTheCompanion()
	{
		var run = RunWith(Dog, Unit("Exposed", 2, 2));

		var (state, _) = run.StartBattle(DoomScenario.Nuclear, countdown: 2, [Enemy(0)]);
		var after = run.AfterBattle(PlayOut(state));

		// The companion was on the field the whole time — exactly what Nuclear reads.
		Assert.That(after.Deck.Any(c => c.HasTag(DoomTransforms.IrradiatedTag)), Is.False);
		Assert.That(after.Companion.Marks.Single().Name, Is.EqualTo("Glowing"));
		Assert.That(after.Companion.Power, Is.EqualTo(3), "marked, not irradiated");
	}

	[Test]
	public void ADeadCompanionDoesNotFeedZombieAndReturnsNextBattle()
	{
		var run = RunWith(Dog, Unit("Filler", 1, 1));

		// 9 damage against a 1/3 companion blocking: it dies every time.
		var (state, _) = run.StartBattle(DoomScenario.Zombie, countdown: 2, [Enemy(9)]);

		var companion = state.Units().Single(u => u.HasComponent<CompanionComponent>());
		(state, _) = Do(
			state,
			new AssignAction
			{
				UnitId = companion.Id,
				EnemyId = state.LivingEnemies().First().Id,
				Assignment = Assignment.Block,
			}
		);

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
		(state, _) = Do(
			state,
			new AssignAction
			{
				UnitId = companion.Id,
				EnemyId = state.LivingEnemies().First().Id,
				Assignment = Assignment.Block,
			}
		);
		(state, _) = Do(state, new EndTurnAction());

		Assert.That(state.GetPlayer().Life, Is.EqualTo(58), "3 toughness absorbed 3 of 5");
	}
}
