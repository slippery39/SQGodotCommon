using System.Collections.Immutable;
using ImmutableGameObjects;
using KinCore;

namespace KinCore.Tests;

/// <summary>
/// Thorns and Strikes — clusters 2 and 3 of the card design pass.
///
/// **Every test here asserts a number that moved on the board**, never that a field was set. A
/// keyword that is a field rather than an effect is exactly the shape that ships inert and throws
/// nothing, which this repo has now been bitten by more than once.
///
/// Cards are defined INLINE, never loaded from a library — a balance pass on real cards must not
/// break a test about a mechanic.
/// </summary>
public class ThornsAndStrikesTests
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
		int thorns = 0,
		int strikes = 1,
		int lane = 0
	)
	{
		var card = (KinCard)
			new KinCard
			{
				Name = name,
				Cost = 1,
				RunCardId = 0,
			}.WithComponent(
				new UnitComponent
				{
					Power = power,
					Toughness = toughness,
					Thorns = thorns,
					Strikes = strikes,
					Lane = lane,
				}
			);

		var (s, added) = state.AddObject(card, state.ZoneId(ZoneType.Hand));
		return (s, added.Id);
	}

	private static (GameState, int) AddEnemy(
		GameState state,
		string name,
		int health,
		int attack,
		int thorns = 0,
		int strikes = 1,
		int lane = 0
	)
	{
		var (s, e) = state.AddObject(
			new Enemy
			{
				Name = name,
				Health = health,
				MaxHealth = health,
				Intent = attack > 0 ? IntentKind.Attack : IntentKind.Wait,
				IntentAmount = attack,
				Thorns = thorns,
				Strikes = strikes,
				Lane = lane,
			},
			state.ZoneId(ZoneType.Enemies)
		);
		return (s, e.Id);
	}

	private static int EnemyHealth(GameState state, int lane = 0) =>
		state.EnemyInLane(lane)!.Health;

	/// <summary>
	/// **Damage on a unit cannot be read after the turn, because the unit is GONE** — combat v3
	/// withdraws it inside `EndTurnAction`. Four tests here were first written to read it back and
	/// all four failed on a null lane, which is the same scar the handoff records about
	/// `OnTurnStart`.
	///
	/// So what a body took is proved by what it could not soak: damage past its toughness spills,
	/// and the event carries both halves.
	/// </summary>
	private static PlayerDamagedEvent? Spill(ImmutableList<GameEvent> events) =>
		events.OfType<PlayerDamagedEvent>().SingleOrDefault();

	// ===== Thorns =====

	[Test]
	public void EnemyThornsHitTheUnitThatAttackedIt()
	{
		var state = KinBattleFactory.Create(life: 60);
		(state, _) = AddEnemy(state, "Razorback", health: 40, attack: 0, thorns: 6);
		(state, var id) = AddUnit(state, "Brave", power: 5, toughness: 4);

		(state, _) = state.BeginBattle();
		(state, _) = Do(state, new PlayCardAction { CardId = id, Lane = 0 });
		(state, var events) = Do(state, new EndTurnAction());

		// The enemy is WAITING, so every point here is the answer to being hit: 6 against a
		// 4-toughness body soaks 4 and spills 2.
		var dmg = Spill(events);

		Assert.Multiple(() =>
		{
			Assert.That(dmg, Is.Not.Null, "thorns are real damage and behave like it");
			Assert.That(dmg!.Absorbed, Is.EqualTo(4));
			Assert.That(dmg.Amount, Is.EqualTo(2), "6 of spikes, 4 soaked");
		});
	}

	/// <summary>
	/// **The design doc's own example, and the reason thorns is not redundant with combat.**
	///
	/// A 6/12 wall soaks a telegraphed 8 and you take nothing. Against Thorns 5 the same wall takes
	/// 13, soaks 12, and 1 goes through — so a big toughness body has stopped being a safe answer.
	/// </summary>
	[Test]
	public void ThornsLandOnTopOfTheAttackAndTheExcessSpillsToYourFace()
	{
		var state = KinBattleFactory.Create(life: 60);
		(state, _) = AddEnemy(state, "Razorback", health: 40, attack: 8, thorns: 5);
		(state, var id) = AddUnit(state, "Wall", power: 6, toughness: 12);

		(state, _) = state.BeginBattle();
		(state, _) = Do(state, new PlayCardAction { CardId = id, Lane = 0 });
		(state, var events) = Do(state, new EndTurnAction());

		var dmg = events.OfType<PlayerDamagedEvent>().Single();

		Assert.Multiple(() =>
		{
			Assert.That(dmg.Absorbed, Is.EqualTo(12), "the whole wall, spent");
			Assert.That(dmg.Amount, Is.EqualTo(1), "8 + 5 = 13, twelve of it soaked");
			Assert.That(state.GetPlayer().Life, Is.EqualTo(59));
		});
	}

	[Test]
	public void YourThornsHitTheEnemyThatAttackedYou()
	{
		var state = KinBattleFactory.Create(life: 60);
		(state, _) = AddEnemy(state, "Biter", health: 40, attack: 3);
		(state, var id) = AddUnit(state, "Bramblehide", power: 2, toughness: 12, thorns: 6);

		(state, _) = state.BeginBattle();
		(state, _) = Do(state, new PlayCardAction { CardId = id, Lane = 0 });
		(state, _) = Do(state, new EndTurnAction());

		// 2 power + 6 thorns. A 2/12 that eats 8 a turn is the whole case for the keyword.
		Assert.That(EnemyHealth(state), Is.EqualTo(32));
	}

	[Test]
	public void ThornsDoNotFireWhenNothingActuallyAttacked()
	{
		var state = KinBattleFactory.Create(life: 60);
		(state, _) = AddEnemy(state, "Spiky", health: 40, attack: 0, thorns: 9);
		(state, var id) = AddUnit(state, "Pacifist", power: 0, toughness: 1, thorns: 9);

		(state, _) = state.BeginBattle();
		(state, _) = Do(state, new PlayCardAction { CardId = id, Lane = 0 });
		(state, var events) = Do(state, new EndTurnAction());

		// Neither side swung, so neither set of spikes has anything to answer. The unit is a 0/1:
		// nine thorns would have killed it outright and nothing did.
		Assert.Multiple(() =>
		{
			Assert.That(events.OfType<UnitDiedEvent>(), Is.Empty, "a 0-power unit never attacked");
			Assert.That(Spill(events), Is.Null, "and nothing spilled");
			Assert.That(EnemyHealth(state), Is.EqualTo(40), "a waiting enemy never attacked");
		});
	}

	// ===== Strikes =====

	[Test]
	public void StrikingTwiceDealsItsPowerTwice()
	{
		var state = KinBattleFactory.Create(life: 60);
		(state, _) = AddEnemy(state, "Target", health: 40, attack: 0);
		(state, var id) = AddUnit(state, "Twin Blades", power: 8, toughness: 10, strikes: 2);

		(state, _) = state.BeginBattle();
		(state, _) = Do(state, new PlayCardAction { CardId = id, Lane = 0 });
		(state, _) = Do(state, new EndTurnAction());

		Assert.That(EnemyHealth(state), Is.EqualTo(24));
	}

	[Test]
	public void AnEnemyThatStrikesTwiceLandsItsIntentTwice()
	{
		var state = KinBattleFactory.Create(life: 60);
		(state, _) = AddEnemy(state, "Flail Knight", health: 40, attack: 7, strikes: 2);
		(state, var id) = AddUnit(state, "Wall", power: 1, toughness: 10);

		(state, _) = state.BeginBattle();
		(state, _) = Do(state, new PlayCardAction { CardId = id, Lane = 0 });
		(state, var events) = Do(state, new EndTurnAction());

		// 7 twice against a 10-toughness wall: 10 soaked, 4 through. A single strike would have
		// been soaked whole and spilled nothing at all.
		var dmg = Spill(events);

		Assert.Multiple(() =>
		{
			Assert.That(dmg, Is.Not.Null, "one strike would not have got past this wall");
			Assert.That(dmg!.Absorbed, Is.EqualTo(10));
			Assert.That(dmg.Amount, Is.EqualTo(4));
		});
	}

	/// <summary>
	/// **The anti-synergy the enemy side exists to punish, and it is the point of both keywords.**
	///
	/// Strikes is a HOOK, not a number: everything that answers a hit answers each one. So walking
	/// a double-striker into spikes takes the spikes twice, and that is the first time placing a
	/// unit is a question about WHICH unit rather than which lane.
	/// </summary>
	[Test]
	public void ADoubleStrikerWalksIntoThornsTwice()
	{
		var state = KinBattleFactory.Create(life: 60);
		(state, _) = AddEnemy(state, "Razorback", health: 90, attack: 0, thorns: 6);
		(state, var id) = AddUnit(state, "Twin Blades", power: 8, toughness: 10, strikes: 2);

		(state, _) = state.BeginBattle();
		(state, _) = Do(state, new PlayCardAction { CardId = id, Lane = 0 });
		(state, var events) = Do(state, new EndTurnAction());

		// Thorns 6 answering TWO hits is 12 against a 10-toughness body: 10 soaked, 2 through.
		// A single-striker takes 6, soaks all of it, and spills nothing — so this number is the
		// keyword interacting, not either keyword on its own.
		var dmg = Spill(events);

		Assert.Multiple(() =>
		{
			Assert.That(dmg, Is.Not.Null, "one strike would have been soaked whole");
			Assert.That(dmg!.Amount, Is.EqualTo(2), "6 twice, 10 soaked");
		});
	}

	[Test]
	public void AnUnopposedStrikerHitsTheOpponentOncePerStrike()
	{
		var state = KinBattleFactory.Create(life: 60, opponentHealth: 100);
		(state, var id) = AddUnit(state, "Twin Blades", power: 8, toughness: 10, strikes: 2);

		(state, _) = state.BeginBattle();
		(state, _) = Do(state, new PlayCardAction { CardId = id, Lane = 0 });
		(state, _) = Do(state, new EndTurnAction());

		// An open lane is still the exchange. Missing this branch is how Strikes would have looked
		// like it worked in every test above and done nothing in the fight that matters.
		Assert.That(state.GetOpponent().Health, Is.EqualTo(84));
	}

	// ===== The regression guard =====

	[Test]
	public void AnOrdinaryExchangeIsUntouchedByEitherKeyword()
	{
		var state = KinBattleFactory.Create(life: 20);
		(state, _) = AddEnemy(state, "Leviathan", health: 20, attack: 5);
		(state, var id) = AddUnit(state, "Chump", power: 3, toughness: 1);

		(state, _) = state.BeginBattle();
		(state, _) = Do(state, new PlayCardAction { CardId = id, Lane = 0 });
		(state, _) = Do(state, new EndTurnAction());

		Assert.Multiple(() =>
		{
			Assert.That(EnemyHealth(state), Is.EqualTo(17), "power once");
			Assert.That(state.GetPlayer().Life, Is.EqualTo(16), "1 soaked of 5");
		});
	}
}
