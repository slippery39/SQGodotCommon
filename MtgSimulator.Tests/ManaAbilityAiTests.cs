using System.Collections.Immutable;
using ImmutableGameObjects;
using MtgCore;
using MtgSimulator;

namespace MtgSimulator.Tests;

/// <summary>
/// Scenario tests verifying that BeamSearchAiStrategy correctly values mana-ability plays.
///
/// Each test constructs a minimal game state where the only profitable line is:
///   play/activate the mana source → spend the extra mana on a spell that wouldn't otherwise fit.
/// The tests step through SelectAction calls one at a time, asserting the correct action type
/// and target card at each step.
///
/// Uses MtgGameFactory.Create() (real mana) with manual mana setup — we want the mana
/// constraint to be real so the forced play is meaningful.
/// </summary>
[TestFixture]
public class ManaAbilityAiTests
{
	private GameState _state;
	private MtgGameIds _ids;
	private BeamSearchAiStrategy _ai;

	[SetUp]
	public void Setup()
	{
		(_state, _ids) = MtgGameFactory.Create();
		_ai = new BeamSearchAiStrategy(_ids, rng: new Random(42));

		var p1 = _state.GetPlayer(_ids.Player1Id);
		_state = _state.UpdateObject(_ids.Player1Id, p1 with { CurrentMana = 2, MaxMana = 2 });
	}

	// ===== MOX =====

	/// <summary>
	/// Scenario: 2 mana, Mox (free) + 3-drop creature in hand.
	/// Mox is the only productive first play (3-drop costs 3; nothing to attack with).
	/// Expected line: cast Mox → activate Mox (+1 mana = 3) → cast 3-drop.
	/// </summary>
	[Test]
	public void Mox_WithThreeDropInHand_PlaysMoxActivatesAndCastsCreature()
	{
		var mox = CardLibrary.Mox() with
		{
			OwnerId = _ids.Player1Id,
			ControllerId = _ids.Player1Id,
		};
		(_state, var addedMox) = _state.AddObject(mox, parentId: _ids.Player1HandId);
		(_state, var addedCreature) = _state.AddObject(
			MakeThreeDrop(),
			parentId: _ids.Player1HandId
		);

		var step1 = _ai.SelectAction(_state, _ids, _ids.Player1Id);
		Assert.That(step1, Is.InstanceOf<CastPermanentAction>(), "Step 1: expected to cast Mox");
		Assert.That(((CastPermanentAction)step1).CardId, Is.EqualTo(addedMox.Id));
		_state = Execute(_state, step1);

		var step2 = _ai.SelectAction(_state, _ids, _ids.Player1Id);
		Assert.That(
			step2,
			Is.InstanceOf<ActivateAbilityAction>(),
			"Step 2: expected to activate Mox"
		);
		Assert.That(((ActivateAbilityAction)step2).CardId, Is.EqualTo(addedMox.Id));
		_state = Execute(_state, step2);

		var step3 = _ai.SelectAction(_state, _ids, _ids.Player1Id);
		Assert.That(
			step3,
			Is.InstanceOf<CastCreatureAction>(),
			"Step 3: expected to cast 3-drop creature"
		);
		Assert.That(((CastCreatureAction)step3).CardId, Is.EqualTo(addedCreature.Id));
	}

	// ===== SOL RING =====

	/// <summary>
	/// Scenario: 2 mana, Sol Ring (cost 1, adds 2) + 3-drop creature in hand.
	/// Expected line: cast Sol Ring (→ 1 mana left) → activate Sol Ring (→ 3 mana) → cast 3-drop.
	/// </summary>
	[Test]
	public void SolRing_WithThreeDropInHand_PlaysSolRingActivatesAndCastsCreature()
	{
		var solRing = CardLibrary.SolRing() with
		{
			OwnerId = _ids.Player1Id,
			ControllerId = _ids.Player1Id,
		};
		(_state, var addedSolRing) = _state.AddObject(solRing, parentId: _ids.Player1HandId);
		(_state, var addedCreature) = _state.AddObject(
			MakeThreeDrop(),
			parentId: _ids.Player1HandId
		);

		var step1 = _ai.SelectAction(_state, _ids, _ids.Player1Id);
		Assert.That(
			step1,
			Is.InstanceOf<CastPermanentAction>(),
			"Step 1: expected to cast Sol Ring"
		);
		Assert.That(((CastPermanentAction)step1).CardId, Is.EqualTo(addedSolRing.Id));
		_state = Execute(_state, step1);

		var step2 = _ai.SelectAction(_state, _ids, _ids.Player1Id);
		Assert.That(
			step2,
			Is.InstanceOf<ActivateAbilityAction>(),
			"Step 2: expected to activate Sol Ring"
		);
		Assert.That(((ActivateAbilityAction)step2).CardId, Is.EqualTo(addedSolRing.Id));
		_state = Execute(_state, step2);

		var step3 = _ai.SelectAction(_state, _ids, _ids.Player1Id);
		Assert.That(
			step3,
			Is.InstanceOf<CastCreatureAction>(),
			"Step 3: expected to cast 3-drop creature"
		);
		Assert.That(((CastCreatureAction)step3).CardId, Is.EqualTo(addedCreature.Id));
	}

	// ===== LLANOWAR ELVES =====

	/// <summary>
	/// Scenario: 2 mana, Llanowar Elves already on the battlefield (not sick, already attacked),
	/// 3-drop creature in hand.
	///
	/// HasAttacked = true removes the attack option so the only productive moves are
	/// activate (tap for mana) and then cast. This mirrors the common mid-game state
	/// where the Elves have already swung and are available to tap.
	/// </summary>
	[Test]
	public void LlanowarElves_OnBattlefield_ActivatesAndCastsCreature()
	{
		var elvesCard = CardLibrary.LlanowarElves() with
		{
			OwnerId = _ids.Player1Id,
			ControllerId = _ids.Player1Id,
		};
		var cc = elvesCard.GetComponent<CreatureComponent>()!;
		elvesCard = elvesCard with
		{
			Components = elvesCard.Components.Replace(
				cc,
				cc with
				{
					HasSummoningSickness = false,
					HasAttacked = true,
				}
			),
		};
		(_state, var addedElves) = _state.AddObject(elvesCard, parentId: _ids.Player1BattlefieldId);
		(_state, var addedCreature) = _state.AddObject(
			MakeThreeDrop(),
			parentId: _ids.Player1HandId
		);

		var step1 = _ai.SelectAction(_state, _ids, _ids.Player1Id);
		Assert.That(
			step1,
			Is.InstanceOf<ActivateAbilityAction>(),
			"Step 1: expected to activate Llanowar Elves"
		);
		Assert.That(((ActivateAbilityAction)step1).CardId, Is.EqualTo(addedElves.Id));
		_state = Execute(_state, step1);

		var step2 = _ai.SelectAction(_state, _ids, _ids.Player1Id);
		Assert.That(
			step2,
			Is.InstanceOf<CastCreatureAction>(),
			"Step 2: expected to cast 3-drop creature"
		);
		Assert.That(((CastCreatureAction)step2).CardId, Is.EqualTo(addedCreature.Id));
	}

	// ===== HELPERS =====

	private Card MakeThreeDrop() =>
		new()
		{
			Name = "Test Creature",
			ManaCost = 3,
			OwnerId = _ids.Player1Id,
			ControllerId = _ids.Player1Id,
			Components = ImmutableArray.Create<GameComponent>(
				new PermanentComponent(),
				new CreatureComponent { Power = 3, Toughness = 3 }
			),
		};

	private static GameState Execute(GameState state, GameAction action)
	{
		var (stateWithAction, success) = state.TryAddAction(action);
		Assert.That(success, Is.True, $"TryAddAction failed for {action.GetType().Name}");
		var (finalState, _) = stateWithAction.ProcessAllActions();
		return finalState;
	}
}
