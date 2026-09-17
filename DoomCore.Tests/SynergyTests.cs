using System.Collections.Immutable;
using DoomCore;
using ImmutableGameObjects;

namespace DoomCore.Tests;

/// <summary>
/// The primitives that make synergies sayable: scaling amounts, stat changes, and adjacency.
///
/// **Every effect amount in the game was a literal before these**, so no card could grow, reward a
/// board, or care where it stood. That is most of why the pool had no synergies — not that nobody
/// wrote them, but that they could not be written.
///
/// **Every test asserts the CONSEQUENCE.** A primitive that is declared and never fires throws no
/// error and looks exactly like one that worked.
///
/// Cards are defined INLINE, never loaded from content.
/// </summary>
public class SynergyTests
{
	private static (GameState State, ImmutableList<GameEvent> Events) Do(
		GameState state,
		GameAction action
	) => state.AddAction(action).ProcessAllActions();

	private static (GameState, int) AddUnit(
		GameState state,
		string name,
		int power,
		int toughness,
		ZoneType zone = ZoneType.Hand,
		params DoomEffect[] effects
	)
	{
		var card = (DoomCard)
			new DoomCard
			{
				Name = name,
				Cost = 0,
				RunCardId = 0,
				Effects = [.. effects],
			}.WithComponent(new UnitComponent { Power = power, Toughness = toughness });

		var (s, added) = state.AddObject(card, state.ZoneId(zone));
		return (s, added.Id);
	}

	private static (GameState, int) AddEnemy(GameState state, int health, int attack, int lane)
	{
		var (s, e) = state.AddObject(
			new Enemy
			{
				Name = "Enemy",
				Health = health,
				MaxHealth = health,
				Intent = attack > 0 ? IntentKind.Attack : IntentKind.Wait,
				IntentAmount = attack,
				Lane = lane,
			},
			state.ZoneId(ZoneType.Enemies)
		);
		return (s, e.Id);
	}

	private static DoomEffect On(EffectTrigger trigger, DoomTarget target, GameAction template) =>
		new()
		{
			Trigger = trigger,
			Target = target,
			Template = template,
		};

	// ===== Scaling amounts =====

	/// <summary>
	/// An amount multiplied by something the board can answer. **The whole point of `CountOf`.**
	/// </summary>
	[Test]
	public void DamageScalesWithTheBoard()
	{
		var state = DoomBattleFactory.Create(DoomScenario.Flood, countdown: 9, opponentHealth: 500);
		(state, _) = AddUnit(state, "A", 1, 9, ZoneType.Field);
		(state, _) = AddUnit(state, "B", 1, 9, ZoneType.Field);
		(state, var enemyId) = AddEnemy(state, health: 100, attack: 0, lane: 4);

		(state, _) = state.BeginBattle();

		// Two units on the field, so "3 per unit you hold" is 6 — and the rite itself is not a unit.
		(state, var riteId) = AddUnit(state, "unused", 0, 1);
		state = state.RemoveObject(riteId);

		var (s2, rite) = state.AddObject(
			new DoomCard
			{
				Name = "Volley",
				Cost = 0,
				RunCardId = 0,
				Effects =
				[
					On(
						EffectTrigger.OnPlay,
						DoomTarget.AllEnemies,
						new DealDamageAction { Amount = 3, PerEach = CountOf.YourUnits }
					),
				],
			},
			state.ZoneId(ZoneType.Hand)
		);
		state = s2;

		(state, _) = Do(state, new PlayCardAction { CardId = rite.Id });

		Assert.That(
			((Enemy)state.GetObject(enemyId)).Health,
			Is.EqualTo(94),
			"3 x 2 units = 6, not 3 — the amount scaled"
		);
	}

	/// <summary>
	/// `CountOf.None` must multiply by ONE, not zero. Getting this backwards would silently turn
	/// every card authored before scaling existed into a no-op.
	/// </summary>
	[Test]
	public void AnUnscaledAmountIsUnchanged()
	{
		var state = DoomBattleFactory.Create(DoomScenario.Flood, countdown: 9, opponentHealth: 500);
		(state, var enemyId) = AddEnemy(state, health: 100, attack: 0, lane: 0);
		(state, _) = state.BeginBattle();

		var (s2, rite) = state.AddObject(
			new DoomCard
			{
				Name = "Shot",
				Cost = 0,
				RunCardId = 0,
				Effects =
				[
					On(
						EffectTrigger.OnPlay,
						DoomTarget.AllEnemies,
						new DealDamageAction { Amount = 7 }
					),
				],
			},
			state.ZoneId(ZoneType.Hand)
		);
		state = s2;

		(state, _) = Do(state, new PlayCardAction { CardId = rite.Id });

		Assert.That(((Enemy)state.GetObject(enemyId)).Health, Is.EqualTo(93));
	}

	// ===== Stat changes =====

	[Test]
	public void ABuffChangesAUnitsStats()
	{
		var state = DoomBattleFactory.Create(DoomScenario.Flood, countdown: 9, opponentHealth: 500);
		(state, var targetId) = AddUnit(state, "Target", 2, 4, ZoneType.Field);
		(state, _) = state.BeginBattle();

		(state, var events) = Do(
			state,
			new BuffAction
			{
				TargetIds = [targetId],
				Power = 5,
				Toughness = 3,
			}
		);

		var unit = ((DoomCard)state.GetObject(targetId)).Unit();
		Assert.That(unit.Power, Is.EqualTo(7));
		Assert.That(unit.Toughness, Is.EqualTo(7));

		var buffed = events.OfType<UnitBuffedEvent>().Single();
		Assert.That(buffed.Power, Is.EqualTo(5), "the event carries the delta, not the total");
		Assert.That(buffed.Toughness, Is.EqualTo(3));
	}

	/// <summary>
	/// A debuff is a negative buff, and toughness floors at 1. Taking a unit to 0 toughness would
	/// kill it the instant it was touched, and killing is `DealDamageAction`'s job.
	/// </summary>
	[Test]
	public void ADebuffCannotKillByItself()
	{
		var state = DoomBattleFactory.Create(DoomScenario.Flood, countdown: 9, opponentHealth: 500);
		(state, var targetId) = AddUnit(state, "Target", 4, 2, ZoneType.Field);
		(state, _) = state.BeginBattle();

		(state, _) = Do(
			state,
			new BuffAction
			{
				TargetIds = [targetId],
				Power = -9,
				Toughness = -9,
			}
		);

		var unit = ((DoomCard)state.GetObject(targetId)).Unit();
		Assert.That(unit.Power, Is.Zero, "floored, not negative");
		Assert.That(unit.Toughness, Is.EqualTo(1));
		Assert.That(unit.IsDead, Is.False);
	}

	// ===== Adjacency — the spatial axis =====

	/// <summary>
	/// **Lane choice was very nearly arbitrary until this existed**: any open lane was as good as
	/// any other. An effect that reads next door is the first reason to prefer one.
	/// </summary>
	[Test]
	public void AnEffectReachesTheLanesEitherSide()
	{
		var state = DoomBattleFactory.Create(DoomScenario.Flood, countdown: 9, opponentHealth: 500);
		(state, var left) = AddUnit(state, "Left", 1, 9, ZoneType.Field);
		(state, var right) = AddUnit(state, "Right", 1, 9, ZoneType.Field);
		(state, var far) = AddUnit(state, "Far", 1, 9, ZoneType.Field);

		state = Place(state, left, 0);
		state = Place(state, right, 2);
		state = Place(state, far, 4);

		var (s2, banner) = state.AddObject(
			(DoomCard)
				new DoomCard
				{
					Name = "Banner",
					Cost = 0,
					RunCardId = 0,
					Effects =
					[
						On(
							EffectTrigger.OnPlay,
							DoomTarget.YourUnitsInAdjacentLanes,
							new BuffAction { Power = 3 }
						),
					],
				}.WithComponent(new UnitComponent { Power = 1, Toughness = 1 }),
			state.ZoneId(ZoneType.Hand)
		);
		state = s2;

		(state, _) = state.BeginBattle();
		(state, _) = Do(state, new PlayCardAction { CardId = banner.Id, Lane = 1 });

		Assert.That(((DoomCard)state.GetObject(left)).Unit().Power, Is.EqualTo(4), "lane 0");
		Assert.That(((DoomCard)state.GetObject(right)).Unit().Power, Is.EqualTo(4), "lane 2");
		Assert.That(
			((DoomCard)state.GetObject(far)).Unit().Power,
			Is.EqualTo(1),
			"lane 4 is not adjacent to lane 1 — adjacency must not mean 'everything'"
		);
	}

	/// <summary>
	/// A lane at the edge has ONE neighbour. That is content, not a limitation: it is what makes
	/// the middle lanes worth more than the outside ones to anything reading adjacency.
	/// </summary>
	[Test]
	public void TheEdgeLaneHasOneNeighbour()
	{
		var state = DoomBattleFactory.Create(DoomScenario.Flood, countdown: 9, opponentHealth: 500);
		(state, var e1) = AddEnemy(state, health: 50, attack: 0, lane: 1);
		(state, var e3) = AddEnemy(state, health: 50, attack: 0, lane: 3);

		var (s2, blast) = state.AddObject(
			(DoomCard)
				new DoomCard
				{
					Name = "Blast",
					Cost = 0,
					RunCardId = 0,
					Effects =
					[
						On(
							EffectTrigger.OnPlay,
							DoomTarget.EnemiesInAdjacentLanes,
							new DealDamageAction { Amount = 10 }
						),
					],
				}.WithComponent(new UnitComponent { Power = 0, Toughness = 9 }),
			state.ZoneId(ZoneType.Hand)
		);
		state = s2;

		(state, _) = state.BeginBattle();
		(state, _) = Do(state, new PlayCardAction { CardId = blast.Id, Lane = 0 });

		Assert.That(((Enemy)state.GetObject(e1)).Health, Is.EqualTo(40), "lane 1 is next door");
		Assert.That(
			((Enemy)state.GetObject(e3)).Health,
			Is.EqualTo(50),
			"and there is no lane -1 to wrap around to"
		);
	}

	// ===== The companion's ability =====

	/// <summary>
	/// **Ash's ability, end to end.** It reads what the enemy KILLED last turn, which only means
	/// anything because combat v3 made withdrawing and dying different events — the four units that
	/// walked off the board at end of turn must not feed it.
	/// </summary>
	[Test]
	public void AshGrowsOnWhatTheEnemyKilled()
	{
		var run = new Run
		{
			Life = 200,
			MaxLife = 200,
			Companion = StarterContent.StarterCompanion,
		}.WithCards(
			[
				new RunCard
				{
					Name = "Fodder",
					Cost = 0,
					IsUnit = true,
					Power = 0,
					Toughness = 1,
				},
				new RunCard
				{
					Name = "Survivor",
					Cost = 0,
					IsUnit = true,
					Power = 0,
					Toughness = 40,
				},
			]
		);

		// One enemy that kills anything small in lane 0, and nothing near the companion.
		var enemy = new Enemy
		{
			Name = "Crusher",
			Health = 500,
			MaxHealth = 500,
			Intent = IntentKind.Attack,
			IntentAmount = 30,
			Lane = 0,
		};

		var (state, _) = run.StartBattle(
			DoomScenario.Flood,
			countdown: 9,
			[enemy],
			opponentHealth: 500
		);

		var ashId = state.Units().Single(u => u.HasComponent<CompanionComponent>()).Id;
		var basePower = ((DoomCard)state.GetObject(ashId)).Unit().Power;

		// Turn 1: feed the crusher, and put the survivor somewhere safe so it WITHDRAWS.
		var fodder = state.CardsIn(ZoneType.Hand).First(c => c.Name == "Fodder");
		(state, _) = Do(state, new PlayCardAction { CardId = fodder.Id, Lane = 0 });
		var survivor = state.CardsIn(ZoneType.Hand).First(c => c.Name == "Survivor");
		(state, _) = Do(state, new PlayCardAction { CardId = survivor.Id, Lane = 4 });

		(state, _) = Do(state, new EndTurnAction());

		Assert.That(
			state.GetBattle().DiedLastTurnRunCardIds,
			Has.Count.EqualTo(1),
			"one died; the other withdrew, and withdrawing is not dying"
		);

		var grown = ((DoomCard)state.GetObject(ashId)).Unit().Power;
		Assert.That(
			grown,
			Is.EqualTo(basePower + 2),
			"Ash read the death at the start of this turn and grew by 2"
		);
	}

	/// <summary>It STACKS within a battle — a grinding fight makes Ash a different creature.</summary>
	[Test]
	public void AshKeepsWhatItGained()
	{
		var run = new Run
		{
			Life = 400,
			MaxLife = 400,
			Companion = StarterContent.StarterCompanion,
		}.WithCards(
			[
				new RunCard
				{
					Name = "Fodder",
					Cost = 0,
					IsUnit = true,
					Power = 0,
					Toughness = 1,
				},
			]
		);

		var enemy = new Enemy
		{
			Name = "Crusher",
			Health = 500,
			MaxHealth = 500,
			Intent = IntentKind.Attack,
			IntentAmount = 30,
			Lane = 0,
		};

		var (state, _) = run.StartBattle(
			DoomScenario.Flood,
			countdown: 99,
			[enemy],
			opponentHealth: 500
		);

		var ashId = state.Units().Single(u => u.HasComponent<CompanionComponent>()).Id;
		var basePower = ((DoomCard)state.GetObject(ashId)).Unit().Power;

		for (var turn = 0; turn < 3; turn++)
		{
			var fodder = state.CardsIn(ZoneType.Hand).First(c => c.Name == "Fodder");
			(state, _) = Do(state, new PlayCardAction { CardId = fodder.Id, Lane = 0 });
			(state, _) = Do(state, new EndTurnAction());
		}

		Assert.That(
			((DoomCard)state.GetObject(ashId)).Unit().Power,
			Is.EqualTo(basePower + 6),
			"three deaths across three turns, +2 each, kept"
		);
	}

	/// <summary>
	/// **A unit that merely withdrew must never feed the attrition axis**, or a companion that pays
	/// for your losses would be paid every turn by your own board doing what it always does.
	/// </summary>
	[Test]
	public void WithdrawingDoesNotFeedAsh()
	{
		var run = new Run
		{
			Life = 200,
			MaxLife = 200,
			Companion = StarterContent.StarterCompanion,
		}.WithCards(
			[
				new RunCard
				{
					Name = "Survivor",
					Cost = 0,
					IsUnit = true,
					Power = 0,
					Toughness = 40,
				},
			]
		);

		var (state, _) = run.StartBattle(
			DoomScenario.Flood,
			countdown: 99,
			[],
			opponentHealth: 500
		);

		var ashId = state.Units().Single(u => u.HasComponent<CompanionComponent>()).Id;
		var basePower = ((DoomCard)state.GetObject(ashId)).Unit().Power;

		var survivor = state.CardsIn(ZoneType.Hand).First(c => c.Name == "Survivor");
		(state, _) = Do(state, new PlayCardAction { CardId = survivor.Id, Lane = 4 });
		(state, _) = Do(state, new EndTurnAction());

		Assert.That(state.UnitInLane(4), Is.Null, "it withdrew");
		Assert.That(
			((DoomCard)state.GetObject(ashId)).Unit().Power,
			Is.EqualTo(basePower),
			"and Ash was paid nothing for it"
		);
	}

	private static GameState Place(GameState state, int cardId, int lane)
	{
		var card = (DoomCard)state.GetObject(cardId);
		state = state.UpdateObject(
			cardId,
			card.WithComponentReplaced(card.Unit() with { Lane = lane })
		);
		return state.MoveObject(cardId, state.ZoneId(ZoneType.Field));
	}
}
