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
		// Board built by hand, so both libraries are empty. Without this the decking rule
		// decides these games: EndTurn decks the opponent and wins outright.
		_state = _state.WithoutDeckingLoss();
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

	// ===== CARDS IN HAND =====

	[Test]
	public void Evaluate_PlayerHasCardInHand_ScoresPositive()
	{
		var (state, _) = AddCardToHand(_state, _ids.Player1Id);

		var score = StateEvaluator.Evaluate(state, _ids, _ids.Player1Id);

		Assert.That(score, Is.GreaterThan(0f));
	}

	[Test]
	public void Evaluate_OpponentHasCardInHand_ScoresNegative()
	{
		var (state, _) = AddCardToHand(_state, _ids.Player2Id);

		var score = StateEvaluator.Evaluate(state, _ids, _ids.Player1Id);

		Assert.That(score, Is.LessThan(0f));
	}

	// ===== BURN SPELL OPPORTUNITY COST =====

	[Test]
	public void BurnSpell_HoldingCard_ScoresHigherThan_FaceDamageAtFullLife()
	{
		// Holding: card in hand, opponent at 20 life
		var (stateHolding, _) = AddCardToHand(_state, _ids.Player1Id);
		var holdScore = StateEvaluator.Evaluate(stateHolding, _ids, _ids.Player1Id);

		// Used on face: no card in hand, opponent at 17 life
		var opponent = _state.GetPlayer(_ids.Player2Id);
		var stateBolted = _state.UpdateObject(_ids.Player2Id, opponent with { Life = 17 });
		var boltScore = StateEvaluator.Evaluate(stateBolted, _ids, _ids.Player1Id);

		// Keeping the spell in hand should be worth more than dealing 3 face damage at 20 life
		Assert.That(holdScore, Is.GreaterThan(boltScore));
	}

	[Test]
	public void BurnSpell_KillingOpponentCreature_ScoresHigherThan_HoldingCard()
	{
		// Holding: card in hand, facing a 3/3
		var (state, _) = AddCreatureToBattlefield(_state, "Bear", 3, 3, _ids.Player2Id);
		(state, _) = AddCardToHand(state, _ids.Player1Id);
		var holdScore = StateEvaluator.Evaluate(state, _ids, _ids.Player1Id);

		// Used on creature: card and creature both gone (equal baseline state)
		var boltScore = StateEvaluator.Evaluate(_state, _ids, _ids.Player1Id);

		// Killing the creature should improve the score more than holding the spell
		Assert.That(boltScore, Is.GreaterThan(holdScore));
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

	private (GameState, Card) AddCardToHand(GameState state, int ownerId)
	{
		var handId = ownerId == _ids.Player1Id ? _ids.Player1HandId : _ids.Player2HandId;
		var card = new Card
		{
			Name = "TestCard",
			OwnerId = ownerId,
			ControllerId = ownerId,
		};
		var (newState, added) = state.AddObject(card, parentId: handId);
		return (newState, added);
	}

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
			Components = ImmutableArray.Create<GameComponent>(
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
			Components = ImmutableArray.Create<GameComponent>(new PermanentComponent()),
		};
		var (newState, added) = state.AddObject(permanent, parentId: battlefieldId);
		return (newState, added);
	}

	// ===== RACING =====

	/// <summary>
	/// QA scenario. The AI is at 6 with a 3/1 haste; the opponent is at 20 with a 2/2. It attacked
	/// the FACE for 3 and died two turns later to the 2/2 plus a burn spell.
	///
	/// Trading the 3/1 into the 2/2 kills both (3 ≥ 2 toughness, 2 ≥ 1 toughness) and removes the
	/// clock that is actually killing it. Three damage to a player at 20 changes nothing.
	///
	/// Life was scored as a flat weight on the DIFFERENCE, so a point of life was worth the same
	/// at 6 as at 20 and there was no notion of being nearly dead. Trading looked worse purely
	/// because it gave up a power-2 board edge.
	/// </summary>
	[Test]
	public void Evaluate_WhenLow_PrefersTradingAwayTheClockOverFaceDamage()
	{
		var lowLife = _state.GetPlayer(_ids.Player1Id) with { Life = 6 };
		var healthy = _state.GetPlayer(_ids.Player2Id) with { Life = 20 };
		var board = _state
			.UpdateObject(_ids.Player1Id, lowLife)
			.UpdateObject(_ids.Player2Id, healthy);

		// After going face: both creatures alive, opponent at 17.
		var (wentFace, _) = AddCreatureToBattlefield(board, "Raider", 3, 1, _ids.Player1Id);
		(wentFace, _) = AddCreatureToBattlefield(wentFace, "Bear", 2, 2, _ids.Player2Id);
		wentFace = wentFace.UpdateObject(
			_ids.Player2Id,
			wentFace.GetPlayer(_ids.Player2Id) with
			{
				Life = 17,
			}
		);

		// After trading: both creatures dead, opponent still at 20.
		var traded = board;

		var faceScore = StateEvaluator.Evaluate(wentFace, _ids, _ids.Player1Id);
		var tradeScore = StateEvaluator.Evaluate(traded, _ids, _ids.Player1Id);

		Assert.That(
			tradeScore,
			Is.GreaterThan(faceScore),
			"at 6 life, removing the 2/2 that is racing you beats 3 damage to a player at 20"
		);
	}

	/// <summary>
	/// The guard on the fix: the same trade must NOT be preferred when the AI is healthy. A player
	/// at a comfortable total should take the aggressive line, or the fix has simply replaced
	/// mindless aggression with mindless trading.
	/// </summary>
	[Test]
	public void Evaluate_WhenHealthy_StillPrefersPressure()
	{
		var board = _state
			.UpdateObject(_ids.Player1Id, _state.GetPlayer(_ids.Player1Id) with { Life = 20 })
			.UpdateObject(_ids.Player2Id, _state.GetPlayer(_ids.Player2Id) with { Life = 20 });

		var (wentFace, _) = AddCreatureToBattlefield(board, "Raider", 3, 1, _ids.Player1Id);
		(wentFace, _) = AddCreatureToBattlefield(wentFace, "Bear", 2, 2, _ids.Player2Id);
		wentFace = wentFace.UpdateObject(
			_ids.Player2Id,
			wentFace.GetPlayer(_ids.Player2Id) with
			{
				Life = 17,
			}
		);

		var faceScore = StateEvaluator.Evaluate(wentFace, _ids, _ids.Player1Id);
		var tradeScore = StateEvaluator.Evaluate(board, _ids, _ids.Player1Id);

		Assert.That(
			faceScore,
			Is.GreaterThan(tradeScore),
			"at a healthy total the aggressive line is still the better one"
		);
	}
}
