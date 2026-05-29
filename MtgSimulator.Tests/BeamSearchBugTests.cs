using System.Collections.Immutable;
using ImmutableGameObjects;
using MtgCore;
using MtgSimulator;

namespace MtgSimulator.Tests;

/// <summary>
/// Regression tests for three BeamSearchAiStrategy bugs where the AI makes
/// strategically pointless moves.
///
/// Tests are written against the BUGGY code first — 1a, 2, and 3 must FAIL
/// before fixes are applied, then PASS after. Test 1b is a regression guard
/// that must PASS both before and after.
/// </summary>
[TestFixture]
public class BeamSearchBugTests
{
	private GameState _state;
	private MtgGameIds _ids;
	private BeamSearchAiStrategy _ai;

	[SetUp]
	public void Setup()
	{
		(_state, _ids) = MtgGameFactory.Create();
		_ai = new BeamSearchAiStrategy(_ids, rng: new Random(42));
	}

	// ===== BUG 1: FAST MANA WITHOUT PAYOFF =====

	/// <summary>
	/// Bug 1a: AI plays Seething Song when there is no payoff card in hand.
	///
	/// Setup: Player has 3 mana (exactly enough to cast Seething Song) and
	/// Seething Song is the only card in hand. The opponent has a card in their
	/// library so they draw on the next turn, making EndTurn score the same as
	/// playing the spell (-1.1 hand penalty each way). The AI must prefer EndTurn.
	///
	/// Expected to FAIL on buggy code (random or exploit causes SS to be chosen).
	/// Expected to PASS after Fix 1A (active-player boundary) and Fix 3 (EndTurn tiebreaker).
	/// </summary>
	[Test]
	public void FastMana_WithNoPayoffInHand_EndsTurnInsteadOfPlayingFastMana()
	{
		var p1 = _state.GetPlayer(_ids.Player1Id);
		_state = _state.UpdateObject(_ids.Player1Id, p1 with { MaxMana = 3, CurrentMana = 3 });

		// Opponent has a card in library so they draw after EndTurn — creates score tie
		var libraryFiller = CardLibrary.GrizzlyBears() with
		{
			OwnerId = _ids.Player2Id,
			ControllerId = _ids.Player2Id,
		};
		(_state, _) = _state.AddObject(libraryFiller, parentId: _ids.Player2LibraryId);

		// Player hand contains only fast mana — no payoff card
		var seethingSong = CardLibrary.SeethingSong() with
		{
			OwnerId = _ids.Player1Id,
			ControllerId = _ids.Player1Id,
		};
		(_state, _) = _state.AddObject(seethingSong, parentId: _ids.Player1HandId);

		var action = _ai.SelectAction(_state, _ids, _ids.Player1Id);

		Assert.That(
			action,
			Is.InstanceOf<EndTurnAction>(),
			"With no payoff in hand, the AI should EndTurn rather than spend a card on fast mana"
		);
	}

	/// <summary>
	/// Bug 1b (regression guard): AI must still play fast mana when it enables a payoff.
	///
	/// Setup: Player has 1 mana — not enough for the 2-cost creature in hand. Lotus Bloom
	/// is free and adds 3 mana, unlocking the creature. The correct line is:
	/// cast Lotus Bloom → cast the 2-cost creature.
	///
	/// Must PASS both before and after fixes — the fix should not break legitimate fast mana.
	/// </summary>
	[Test]
	public void FastMana_WithPayoffInHand_PlaysFastManaThenPayoff()
	{
		var p1 = _state.GetPlayer(_ids.Player1Id);
		_state = _state.UpdateObject(_ids.Player1Id, p1 with { MaxMana = 1, CurrentMana = 1 });

		var lotusBoom = CardLibrary.LotusBoom() with
		{
			OwnerId = _ids.Player1Id,
			ControllerId = _ids.Player1Id,
		};
		var payoffCreature = new Card
		{
			Name = "Payoff Creature",
			ManaCost = 2,
			OwnerId = _ids.Player1Id,
			ControllerId = _ids.Player1Id,
			Components = ImmutableArray.Create<GameComponent>(
				new PermanentComponent(),
				new CreatureComponent { Power = 2, Toughness = 2 }
			),
		};

		(_state, var addedLotus) = _state.AddObject(lotusBoom, parentId: _ids.Player1HandId);
		(_state, var addedPayoff) = _state.AddObject(payoffCreature, parentId: _ids.Player1HandId);

		var step1 = _ai.SelectAction(_state, _ids, _ids.Player1Id);
		Assert.That(
			step1,
			Is.InstanceOf<CastSpellAction>(),
			"Step 1: Lotus Bloom enables the 2-cost payoff — AI should cast it"
		);
		Assert.That(((CastSpellAction)step1).CardId, Is.EqualTo(addedLotus.Id));
		_state = Execute(_state, step1);

		var step2 = _ai.SelectAction(_state, _ids, _ids.Player1Id);
		Assert.That(
			step2,
			Is.InstanceOf<CastCreatureAction>(),
			"Step 2: now that we have mana, AI should cast the payoff creature"
		);
		Assert.That(((CastCreatureAction)step2).CardId, Is.EqualTo(addedPayoff.Id));
	}

	// ===== BUG 2: GIANT GROWTH WITHOUT COMBAT BENEFIT =====

	/// <summary>
	/// Bug 2: AI casts Giant Growth on a creature after it has already attacked —
	/// the +3/+3 is wasted because the creature can't attack again this turn and
	/// the buff evaporates at end of turn.
	///
	/// Setup: Player has Grizzly Bears on the battlefield (HasAttacked=true) and
	/// Giant Growth in hand. No opponent creatures exist. Pumping the bear gains nothing.
	///
	/// Expected to FAIL on buggy code (GetEffectivePower includes UntilEndOfTurn modifiers,
	/// so GG looks like +6 concrete score). Expected to PASS after Fix 2
	/// (StateEvaluator uses GetEffectivePermanentPower, ignoring temporary buffs).
	/// </summary>
	[Test]
	public void GiantGrowth_WithNoOpponentCreaturesToKill_IsNotCast()
	{
		(_state, _ids) = MtgGameFactory.CreateForTesting();
		_ai = new BeamSearchAiStrategy(_ids, rng: new Random(42));

		// Grizzly Bears already attacked this turn — cannot attack again, so GG has no combat use
		var bears = CardLibrary.GrizzlyBears() with
		{
			OwnerId = _ids.Player1Id,
			ControllerId = _ids.Player1Id,
		};
		var bearsCc = bears.GetComponent<CreatureComponent>()!;
		bears = bears with
		{
			Components = bears.Components.Replace(
				bearsCc,
				bearsCc with
				{
					HasSummoningSickness = false,
					HasAttacked = true,
				}
			),
		};
		(_state, _) = _state.AddObject(bears, parentId: _ids.Player1BattlefieldId);

		// Giant Growth is the only card in hand — no opponent creatures to kill
		var giantGrowth = CardLibrary.GiantGrowth() with
		{
			OwnerId = _ids.Player1Id,
			ControllerId = _ids.Player1Id,
		};
		(_state, _) = _state.AddObject(giantGrowth, parentId: _ids.Player1HandId);

		var action = _ai.SelectAction(_state, _ids, _ids.Player1Id);

		Assert.That(
			action,
			Is.Not.InstanceOf<CastSpellAction>(),
			"Giant Growth has no value without an opponent creature to kill — AI should not cast it"
		);
	}

	// ===== BUG 3: NEUTRAL ATTACK INTO TAUNT CREATURE =====

	/// <summary>
	/// Bug 3: AI attacks into a Taunt creature when the attack gains nothing —
	/// both creatures survive and no damage reaches the opponent player.
	///
	/// Setup: Player has a 1/3 creature (no sickness). Opponent has Wall of Roots
	/// (2/5 Taunt). Attack outcome: attacker takes 2 damage and survives (1 toughness
	/// left), wall takes 1 damage and survives — truly neutral. AI should EndTurn.
	///
	/// Expected to FAIL on buggy code (neutral attack ties with EndTurn; random or
	/// active-player exploit breaks the tie toward attack).
	/// Expected to PASS after Fix 1A (active-player boundary) and Fix 3 (EndTurn tiebreaker).
	/// </summary>
	[Test]
	public void Attack_IntoTauntCreatureWithNeutralOutcome_EndsTurnInstead()
	{
		(_state, _ids) = MtgGameFactory.CreateForTesting();
		_ai = new BeamSearchAiStrategy(_ids, rng: new Random(42));

		// 1/3 attacker — survives a hit from the 2/5 wall (takes 2 damage, toughness 3 > 2)
		var attacker = new Card
		{
			Name = "Test Attacker",
			ManaCost = 1,
			OwnerId = _ids.Player1Id,
			ControllerId = _ids.Player1Id,
			Components = ImmutableArray.Create<GameComponent>(
				new PermanentComponent(),
				new CreatureComponent
				{
					Power = 1,
					Toughness = 3,
					HasSummoningSickness = false,
				}
			),
		};
		(_state, _) = _state.AddObject(attacker, parentId: _ids.Player1BattlefieldId);

		// Wall of Roots: 2/5 with Taunt — forces any attack to target it
		var wall = CardLibrary.WallOfThorns() with
		{
			OwnerId = _ids.Player2Id,
			ControllerId = _ids.Player2Id,
		};
		var wallCc = wall.GetComponent<CreatureComponent>()!;
		wall = wall with
		{
			Components = wall.Components.Replace(
				wallCc,
				wallCc with
				{
					HasSummoningSickness = false,
				}
			),
		};
		(_state, _) = _state.AddObject(wall, parentId: _ids.Player2BattlefieldId);

		var action = _ai.SelectAction(_state, _ids, _ids.Player1Id);

		Assert.That(
			action,
			Is.InstanceOf<EndTurnAction>(),
			"Attacking into Wall of Roots gains nothing (both creatures survive) — AI should EndTurn"
		);
	}

	// ===== HELPERS =====

	private static GameState Execute(GameState state, GameAction action)
	{
		var (stateWithAction, success) = state.TryAddAction(action);
		Assert.That(success, Is.True, $"TryAddAction failed for {action.GetType().Name}");
		var (finalState, _) = stateWithAction.ProcessAllActions();
		return finalState;
	}
}
