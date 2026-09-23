using System.Collections.Immutable;
using ImmutableGameObjects;
using KinCore;

namespace KinCore.Tests;

/// <summary>
/// **The companion as the thing you protect (2026-09-22):** its health is your life, its Guard
/// soaks the attack in its own lane and refreshes every turn, and it moves once a turn for free.
///
/// Every test reads a number that moved — life, a lane — never that a field was set. Cards are
/// defined inline, never loaded from a library.
/// </summary>
public class GuardAndMoveTests
{
	private const int Centre = KinBattle.LaneCount / 2;

	private static (GameState State, ImmutableList<GameEvent> Events) Do(
		GameState state,
		GameAction action
	) => state.AddAction(action).ProcessAllActions();

	private static Companion Wall(int toughness) =>
		new()
		{
			Name = "Wall",
			BasePower = 1,
			BaseToughness = toughness,
		};

	private static Run RunWith(Companion companion, params RunCard[] deck) =>
		new Run
		{
			Life = 60,
			MaxLife = 60,
			Companion = companion,
		}.WithCards(deck);

	private static Enemy Enemy(int attack, int lane = Centre, bool flies = false) =>
		new()
		{
			Name = "Foe",
			Health = 500,
			MaxHealth = 500,
			Intent = attack > 0 ? IntentKind.Attack : IntentKind.Wait,
			IntentAmount = attack,
			Lane = lane,
			Flies = flies,
		};

	private static RunCard Guarding(int guard) =>
		new()
		{
			Name = "Brace",
			Cost = 0,
			Effects =
			[
				new KinEffect
				{
					Trigger = EffectTrigger.OnPlay,
					Target = KinTarget.Companion,
					Template = new BuffAction { Guard = guard },
					Text = $"your companion gains {guard} Guard",
				},
			],
		};

	// ===== Guard =====

	/// <summary>
	/// **The Opponent here never reinforces, and it has to not.** Left to its default it summons a
	/// Revenant into an empty lane on turn 2, which hits you through that open lane on turns 3 and
	/// 4 — 8 life that has nothing to do with Guard, and the first version of this test read it as
	/// Guard failing.
	/// </summary>
	private static readonly OpponentDefinition Idle =
		new()
		{
			Name = "Idle",
			Health = 500,
			SummonInterval = 999,
		};

	/// <summary>
	/// **It refreshes.** Under the old rule a 3-toughness companion taking 2 a turn built up damage and
	/// fell on turn 2, leaving the lane open. Now its Guard stands back up every turn and nothing gets
	/// through, however long the fight runs.
	/// </summary>
	[Test]
	public void GuardRefreshesEveryTurnSoASmallAttackNeverGetsThrough()
	{
		var run = RunWith(Wall(3));
		var (state, _) = run.StartBattle([Enemy(2)], opponent: Idle);

		for (var turn = 0; turn < 4; turn++)
			(state, _) = Do(state, new EndTurnAction());

		Assert.That(state.GetPlayer().Life, Is.EqualTo(run.Life));
	}

	[Test]
	public void AnOpenLaneGoesAroundTheGuardStraightToYou()
	{
		var run = RunWith(Wall(30));
		var (state, _) = run.StartBattle([Enemy(5, lane: 0)], opponentHealth: 500);

		(state, _) = Do(state, new EndTurnAction());

		Assert.That(
			state.GetPlayer().Life,
			Is.EqualTo(run.Life - 5),
			"30 Guard, in the wrong lane"
		);
	}

	[Test]
	public void AFlierInTheCompanionsLaneGoesOverTheGuard()
	{
		var run = RunWith(Wall(30));
		var (state, _) = run.StartBattle([Enemy(5, flies: true)], opponentHealth: 500);

		(state, _) = Do(state, new EndTurnAction());

		Assert.That(state.GetPlayer().Life, Is.EqualTo(run.Life - 5));
	}

	/// <summary>
	/// **A guard card is block:** it stacks on the refreshed Guard for the turn it is played and is
	/// gone the next. Turn 1 is braced and takes nothing; turn 2 is not and takes the difference.
	/// </summary>
	[Test]
	public void AGuardCardStacksForOneTurnOnly()
	{
		const int attack = 10;
		var companion = Wall(3);
		var run = RunWith(companion, Guarding(8));
		var (state, _) = run.StartBattle([Enemy(attack)], opponentHealth: 500);

		var brace = state.CardsIn(ZoneType.Hand).Single(c => c.Name == "Brace");
		(state, _) = Do(state, new PlayCardAction { CardId = brace.Id });
		(state, _) = Do(state, new EndTurnAction());

		var afterBraced = state.GetPlayer().Life;
		(state, _) = Do(state, new EndTurnAction());

		Assert.Multiple(() =>
		{
			Assert.That(afterBraced, Is.EqualTo(run.Life), "3 + 8 Guard covers the 10");
			Assert.That(
				state.GetPlayer().Life,
				Is.EqualTo(run.Life - (attack - companion.BaseToughness)),
				"the brace expired; only the refreshed 3 stood in front of the 10"
			);
		});
	}

	/// <summary>
	/// **Damage aimed at the companion is damage to you, after its Guard** — "2 to every unit you
	/// hold" from a trait or a boss wore the companion's body down before. It has no body now.
	/// </summary>
	[Test]
	public void AnEffectThatHitsTheCompanionSpendsGuardThenLife()
	{
		var run = RunWith(Wall(3));
		var (state, _) = run.StartBattle([], opponentHealth: 500);
		var companion = state.Companion()!;

		(state, _) = Do(state, new DealDamageAction { Amount = 5, TargetIds = [companion.Id] });

		Assert.Multiple(() =>
		{
			Assert.That(state.GetPlayer().Life, Is.EqualTo(run.Life - 2), "3 of 5 on the Guard");
			Assert.That(state.Companion()!.Unit().Guard, Is.Zero);
			Assert.That(state.Companion()!.Unit().IsDead, Is.False);
		});
	}

	// ===== The move =====

	[Test]
	public void MovingIntoTheLaneOfTheBigAttackIsWhatSavesYourLife()
	{
		var run = RunWith(Wall(30));
		var (state, _) = run.StartBattle([Enemy(15, lane: 0)], opponentHealth: 500);

		(state, _) = Do(state, new MoveCompanionAction { Lane = 0 });
		(state, _) = Do(state, new EndTurnAction());

		// The same 15 went straight to you in AnOpenLaneGoesAroundTheGuardStraightToYou.
		Assert.That(state.GetPlayer().Life, Is.EqualTo(run.Life));
	}

	[Test]
	public void TheCompanionMovesOnceATurnAndAgainNextTurn()
	{
		var run = RunWith(Wall(10));
		var (state, _) = run.StartBattle([], opponentHealth: 500);

		(state, _) = Do(state, new MoveCompanionAction { Lane = 0 });

		Assert.Multiple(() =>
		{
			Assert.That(state.Companion()!.Unit().Lane, Is.Zero, "it moved");
			Assert.That(
				new MoveCompanionAction { Lane = 1 }
					.ValidateAdd(state)
					.IsValid,
				Is.False,
				"not twice in one turn"
			);
		});

		(state, _) = Do(state, new EndTurnAction());

		Assert.That(
			new MoveCompanionAction { Lane = 1 }
				.ValidateAdd(state)
				.IsValid,
			Is.True
		);
	}

	[Test]
	public void TheCompanionCannotMoveIntoALaneYouHold()
	{
		var unit = new RunCard
		{
			Name = "Holder",
			Cost = 0,
			IsUnit = true,
			Power = 1,
			Toughness = 1,
		};
		var run = RunWith(Wall(10), unit);
		var (state, _) = run.StartBattle([], opponentHealth: 500);

		var holder = state.CardsIn(ZoneType.Hand).Single(c => c.Name == "Holder");
		(state, _) = Do(state, new PlayCardAction { CardId = holder.Id, Lane = 0 });

		Assert.That(
			new MoveCompanionAction { Lane = 0 }
				.ValidateAdd(state)
				.IsValid,
			Is.False
		);
	}

	/// <summary>
	/// **The bot has to USE the move**, or every sim measures a companion that never leaves the
	/// centre. A 15 in lane 0 against 30 Guard: stepping in front of it costs nothing, staying costs 15.
	/// </summary>
	[Test]
	public void TheBotStepsInFrontOfTheBigAttack()
	{
		var run = RunWith(Wall(30));
		var (state, _) = run.StartBattle([Enemy(15, lane: 0)], opponentHealth: 500);

		var played = KinBot.PlayTurn(state);

		Assert.That(played.Companion()!.Unit().Lane, Is.Zero);
	}
}
