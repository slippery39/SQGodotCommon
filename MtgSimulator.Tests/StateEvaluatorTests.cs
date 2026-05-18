using System.Collections.Immutable;
using ImmutableGameObjects;
using MtgCore;
using MtgSimulator;

namespace MtgSimulator.Tests;

/// <summary>
/// Unit tests for StateEvaluator.
///
/// Uses MtgGameFactory.Create() (not CreateForTesting) so both players start
/// with 0 mana, giving a clean zero baseline — equal states score exactly 0
/// and each factor's contribution is unambiguous.
/// </summary>
[TestFixture]
public class StateEvaluatorTests
{
	private GameState _state;
	private MtgGameIds _ids;

	[SetUp]
	public void Setup()
	{
		(_state, _ids) = MtgGameFactory.Create();
	}

	// ===== BASELINE =====

	[Test]
	public void Evaluate_EqualState_ScoresZero()
	{
		var score = StateEvaluator.Evaluate(_state, _ids, _ids.Player1Id);

		Assert.That(score, Is.EqualTo(0f));
	}

	// ===== LIFE =====

	[Test]
	public void Evaluate_PlayerHasMoreLife_ScoresPositive()
	{
		var player = _state.GetPlayer(_ids.Player1Id);
		var state = _state.UpdateObject(_ids.Player1Id, player with { Life = 25 });

		var score = StateEvaluator.Evaluate(state, _ids, _ids.Player1Id);

		Assert.That(score, Is.GreaterThan(0f));
	}

	[Test]
	public void Evaluate_OpponentHasMoreLife_ScoresNegative()
	{
		var opponent = _state.GetPlayer(_ids.Player2Id);
		var state = _state.UpdateObject(_ids.Player2Id, opponent with { Life = 25 });

		var score = StateEvaluator.Evaluate(state, _ids, _ids.Player1Id);

		Assert.That(score, Is.LessThan(0f));
	}

	// ===== CREATURE COUNT =====

	[Test]
	public void Evaluate_PlayerHasMoreCreatures_ScoresPositive()
	{
		var (state, _) = AddCreatureToBattlefield(_state, "Bear", 2, 2, _ids.Player1Id);

		var score = StateEvaluator.Evaluate(state, _ids, _ids.Player1Id);

		Assert.That(score, Is.GreaterThan(0f));
	}

	[Test]
	public void Evaluate_OpponentHasMoreCreatures_ScoresNegative()
	{
		var (state, _) = AddCreatureToBattlefield(_state, "Bear", 2, 2, _ids.Player2Id);

		var score = StateEvaluator.Evaluate(state, _ids, _ids.Player1Id);

		Assert.That(score, Is.LessThan(0f));
	}

	// ===== TERMINAL STATES =====

	[Test]
	public void Evaluate_PlayerHasLost_ReturnsLossScore()
	{
		var player = _state.GetPlayer(_ids.Player1Id);
		var state = _state.UpdateObject(_ids.Player1Id, player with { HasLost = true });

		var score = StateEvaluator.Evaluate(state, _ids, _ids.Player1Id);

		Assert.That(score, Is.EqualTo(StateEvaluator.LossScore));
	}

	[Test]
	public void Evaluate_OpponentHasLost_ReturnsWinScore()
	{
		var opponent = _state.GetPlayer(_ids.Player2Id);
		var state = _state.UpdateObject(_ids.Player2Id, opponent with { HasLost = true });

		var score = StateEvaluator.Evaluate(state, _ids, _ids.Player1Id);

		Assert.That(score, Is.EqualTo(StateEvaluator.WinScore));
	}

	// ===== SYMMETRY =====

	[Test]
	public void Evaluate_SameStateFromOpponentPerspective_ScoresOpposite()
	{
		var player = _state.GetPlayer(_ids.Player1Id);
		var state = _state.UpdateObject(_ids.Player1Id, player with { Life = 25 });

		var p1Score = StateEvaluator.Evaluate(state, _ids, _ids.Player1Id);
		var p2Score = StateEvaluator.Evaluate(state, _ids, _ids.Player2Id);

		Assert.That(p1Score, Is.GreaterThan(0f));
		Assert.That(p2Score, Is.LessThan(0f));
	}

	// ===== NON-CREATURE PERMANENTS =====

	[Test]
	public void Evaluate_PlayerHasNonCreaturePermanent_ScoresPositive()
	{
		var (state, _) = AddNonCreaturePermanentToBattlefield(_state, _ids.Player1Id);

		var score = StateEvaluator.Evaluate(state, _ids, _ids.Player1Id);

		Assert.That(score, Is.GreaterThan(0f));
	}

	[Test]
	public void Evaluate_OpponentHasNonCreaturePermanent_ScoresNegative()
	{
		var (state, _) = AddNonCreaturePermanentToBattlefield(_state, _ids.Player2Id);

		var score = StateEvaluator.Evaluate(state, _ids, _ids.Player1Id);

		Assert.That(score, Is.LessThan(0f));
	}

	[Test]
	public void Evaluate_EqualNonCreaturePermanentsBothSides_ScoresZero()
	{
		var (state, _) = AddNonCreaturePermanentToBattlefield(_state, _ids.Player1Id);
		(state, _) = AddNonCreaturePermanentToBattlefield(state, _ids.Player2Id);

		var score = StateEvaluator.Evaluate(state, _ids, _ids.Player1Id);

		Assert.That(score, Is.EqualTo(0f));
	}

	// ===== HELPERS =====

	private (GameState, Card) AddCreatureToBattlefield(
		GameState state,
		string name,
		int power,
		int toughness,
		int ownerId
	)
	{
		var battlefieldId = state.GetPlayerZoneId(ownerId, ZoneType.Battlefield);
		var creature = new Card
		{
			Name = name,
			OwnerId = ownerId,
			ControllerId = ownerId,
			Components = ImmutableList.Create<GameComponent>(
				new CreatureComponent { Power = power, Toughness = toughness }
			),
		};
		var (newState, added) = state.AddObject(creature, parentId: battlefieldId);
		return (newState, added);
	}

	private (GameState, Card) AddNonCreaturePermanentToBattlefield(GameState state, int ownerId)
	{
		var battlefieldId = state.GetPlayerZoneId(ownerId, ZoneType.Battlefield);
		var permanent = new Card
		{
			Name = "TestEnchantment",
			OwnerId = ownerId,
			ControllerId = ownerId,
			Components = ImmutableList.Create<GameComponent>(new PermanentComponent()),
		};
		var (newState, added) = state.AddObject(permanent, parentId: battlefieldId);
		return (newState, added);
	}
}
