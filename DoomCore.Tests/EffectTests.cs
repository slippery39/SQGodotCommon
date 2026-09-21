using System.Collections.Immutable;
using DoomCore;
using ImmutableGameObjects;

namespace DoomCore.Tests;

/// <summary>
/// Cards are defined INLINE here, never loaded from content — balance changes to real cards must
/// not break tests about mechanics.
///
/// **Every test here asserts the CONSEQUENCE, not the construction.** An effect that is declared
/// and never fires throws no error and looks exactly like one that worked; this repo has lost four
/// bugs to precisely that, so building the effect is never the thing being checked.
/// </summary>
public class EffectTests
{
	private static (GameState State, ImmutableList<GameEvent> Events) Do(
		GameState state,
		GameAction action
	) => state.AddAction(action).ProcessAllActions();

	private static (GameState, int) AddRite(GameState state, int cost, params DoomEffect[] effects)
	{
		var card = new DoomCard
		{
			Name = "Rite",
			Cost = cost,
			RunCardId = 0,
			Effects = [.. effects],
		};

		var (s, added) = state.AddObject(card, state.ZoneId(ZoneType.Hand));
		return (s, added.Id);
	}

	private static DoomEffect OnPlay(DoomTarget target, GameAction template) =>
		new()
		{
			Trigger = EffectTrigger.OnPlay,
			Target = target,
			Template = template,
		};

	private static (GameState, int) AddEnemy(GameState state, int health, int lane)
	{
		var (s, e) = state.AddObject(
			new Enemy
			{
				Name = "Wretch",
				Health = health,
				MaxHealth = health,
				Intent = IntentKind.Wait,
				Lane = lane,
			},
			state.ZoneId(ZoneType.Enemies)
		);
		return (s, e.Id);
	}

	[Test]
	public void ARiteDealsItsDamageToEveryEnemy()
	{
		var state = DoomBattleFactory.Create(DoomScenario.Flood, countdown: 9, opponentHealth: 500);
		(state, _) = AddEnemy(state, health: 5, lane: 0);
		(state, _) = AddEnemy(state, health: 5, lane: 1);
		(state, var riteId) = AddRite(
			state,
			cost: 1,
			OnPlay(DoomTarget.AllEnemies, new DealDamageAction { Amount = 2 })
		);

		(state, _) = state.BeginBattle();
		(state, _) = Do(state, new PlayCardAction { CardId = riteId });

		Assert.That(
			state.LivingEnemies().Select(e => e.Health),
			Is.EquivalentTo(new[] { 3, 3 }),
			"both enemies should have taken it — one action holds every target and loops"
		);
	}

	[Test]
	public void ARiteThatKillsAnEnemyRaisesItsDeath()
	{
		var state = DoomBattleFactory.Create(DoomScenario.Flood, countdown: 9, opponentHealth: 500);
		(state, _) = AddEnemy(state, health: 2, lane: 0);
		(state, var riteId) = AddRite(
			state,
			cost: 0,
			OnPlay(DoomTarget.AllEnemies, new DealDamageAction { Amount = 4 })
		);

		(state, _) = state.BeginBattle();
		(state, var events) = Do(state, new PlayCardAction { CardId = riteId });

		Assert.That(state.LivingEnemies(), Is.Empty);
		Assert.That(
			events.OfType<EnemyDiedEvent>().Any(),
			Is.True,
			"a death the UI never hears about is a health bar that drops for no visible reason"
		);
	}

	[Test]
	public void ARiteCanHitTheOpponentDirectly()
	{
		var state = DoomBattleFactory.Create(DoomScenario.Flood, countdown: 9, opponentHealth: 30);
		(state, var riteId) = AddRite(
			state,
			cost: 0,
			OnPlay(DoomTarget.Opponent, new DealDamageAction { Amount = 5 })
		);

		(state, _) = state.BeginBattle();
		(state, _) = Do(state, new PlayCardAction { CardId = riteId });

		Assert.That(state.GetOpponent().Health, Is.EqualTo(25));
	}

	[Test]
	public void GainingLifeIsCappedAtMaximum()
	{
		var state = DoomBattleFactory.Create(
			DoomScenario.Flood,
			countdown: 9,
			life: 18,
			maxLife: 20,
			opponentHealth: 500
		);
		(state, var riteId) = AddRite(
			state,
			cost: 0,
			OnPlay(DoomTarget.Player, new GainLifeAction { Amount = 10 })
		);

		(state, _) = state.BeginBattle();
		(state, _) = Do(state, new PlayCardAction { CardId = riteId });

		Assert.That(state.GetPlayer().Life, Is.EqualTo(20), "capped, never overshooting");
	}

	/// <summary>
	/// The rule that makes an authoring mistake loud. A card with no body and no effect would cost
	/// energy, leave the hand and change nothing — indistinguishable from a card that worked.
	/// </summary>
	[Test]
	public void ACardThatIsNeitherABodyNorAnEffectIsRefused()
	{
		var state = DoomBattleFactory.Create(DoomScenario.Flood, countdown: 9, opponentHealth: 500);
		(state, var inertId) = AddRite(state, cost: 1);

		(state, _) = state.BeginBattle();

		var validation = new PlayCardAction { CardId = inertId }.ValidateAdd(state);

		Assert.That(validation.IsValid, Is.False);
		Assert.That(validation.Reason, Does.Contain("does nothing"));
	}

	/// <summary>
	/// A lane rule with no lane must hit nobody rather than quietly picking lane 0.
	///
	/// **Rewritten 2026-09-18.** It used to prove a RITE could never use a lane rule, because a
	/// rite has no body and therefore no lane. A rite now carries the lane it was dropped on — that
	/// is the whole targeting model, see `DoomTargeting.Resolve` — so the case this guards is what
	/// is left: a holder with neither a body nor a drop, which is how a doom's battle effect
	/// resolves (`DoomBattleEffects` passes `sourceId: 0`). That still has to hit nothing.
	/// </summary>
	[Test]
	public void ALaneTargetWithNoLaneAtAllHitsNothing()
	{
		var state = DoomBattleFactory.Create(DoomScenario.Flood, countdown: 9, opponentHealth: 500);
		(state, _) = AddEnemy(state, health: 5, lane: 0);

		(state, _) = state.BeginBattle();
		(state, _) = Do(
			state,
			new ResolveEffectsAction
			{
				SourceId = 0,
				Trigger = EffectTrigger.OnPlay,
				Effects =
				[
					OnPlay(DoomTarget.EnemyInSourceLane, new DealDamageAction { Amount = 99 }),
				],
			}
		);

		Assert.That(
			state.LivingEnemies().Single().Health,
			Is.EqualTo(5),
			"a lane rule with no lane must hit nobody, not default to lane 0"
		);
	}
}
