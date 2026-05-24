using ImmutableGameObjects;
using MtgCore;
using MtgSimulator;

namespace MtgSimulator.Tests;

/// <summary>
/// Regression tests for MultiTurnBeamSearchAiStrategy bugs.
///
/// Tests are written against the BUGGY code first — failing tests document the
/// broken behaviour and must PASS after the fix is applied.
/// </summary>
[TestFixture]
public class MultiTurnBeamSearchBugTests
{
	private GameState _state;
	private MtgGameIds _ids;
	private MultiTurnBeamSearchAiStrategy _ai;

	[SetUp]
	public void Setup()
	{
		(_state, _ids) = MtgGameFactory.Create();
		_ai = new MultiTurnBeamSearchAiStrategy(_ids, rng: new Random(42));

		// Prevent empty-library losses during rollout draws (2 lookahead turns × 2 draws per turn)
		for (var i = 0; i < 10; i++)
		{
			var p1Filler = new Card
			{
				Name = "P1Filler",
				ManaCost = 99,
				OwnerId = _ids.Player1Id,
				ControllerId = _ids.Player1Id,
			};
			(_state, _) = _state.AddObject(p1Filler, parentId: _ids.Player1LibraryId);
			var p2Filler = new Card
			{
				Name = "P2Filler",
				ManaCost = 99,
				OwnerId = _ids.Player2Id,
				ControllerId = _ids.Player2Id,
			};
			(_state, _) = _state.AddObject(p2Filler, parentId: _ids.Player2LibraryId);
		}
	}

	// ===== BUG: HORIZON EFFECT — TARMOGOYF NOT DEPLOYED AGAINST DOOM BLADE =====

	/// <summary>
	/// Horizon effect: AI holds Tarmogoyf in hand rather than playing it because the
	/// greedy opponent simulation (with perfect information about the opponent's hand)
	/// kills Tarmogoyf within the lookahead window. Passing the turn defers the play
	/// to a greedy rollout turn where Tarmogoyf survives at the search horizon, making
	/// the pass look artificially better.
	///
	/// Root cause: two-turn lookahead asymmetry — "play now" shows Tarmogoyf dying in
	/// turn 1 of rollout, while "pass" shows Tarmogoyf alive on board at the end of
	/// turn 2 of rollout (the Doom Blade kill happens outside the window).
	///
	/// Expected to FAIL on buggy code (AI picks EndTurn instead of casting Tarmogoyf).
	/// Expected to PASS after fix.
	/// </summary>
	[Test]
	public void HorizonEffect_TarmogoyfsWithDoomBladeOpponent_PlaysTarmogoyf()
	{
		// Player 1 (AI): exactly enough mana for Tarmogoyf (2)
		var p1 = _state.GetPlayer(_ids.Player1Id);
		_state = _state.UpdateObject(_ids.Player1Id, p1 with { MaxMana = 2, CurrentMana = 2 });

		// Player 2 (opponent): enough mana to cast Doom Blade (2) on their simulated turn
		var p2 = _state.GetPlayer(_ids.Player2Id);
		_state = _state.UpdateObject(_ids.Player2Id, p2 with { MaxMana = 2, CurrentMana = 2 });

		// Populate Player 1's graveyard so Tarmogoyf is clearly worth deploying (10/11)
		for (var i = 0; i < 10; i++)
		{
			var grave = new Card
			{
				Name = "GraveFiller",
				ManaCost = 1,
				OwnerId = _ids.Player1Id,
				ControllerId = _ids.Player1Id,
			};
			(_state, _) = _state.AddObject(grave, parentId: _ids.Player1GraveyardId);
		}

		// AI's hand: only Tarmogoyf — the only real choice is cast it or pass
		var tarmogoyf = CardLibrary.Tarmogoyf() with
		{
			OwnerId = _ids.Player1Id,
			ControllerId = _ids.Player1Id,
		};
		(_state, var addedTarmogoyf) = _state.AddObject(tarmogoyf, parentId: _ids.Player1HandId);

		// Opponent's hand: Doom Blade — triggers the oracle/horizon problem
		var doomBlade = CardLibrary.DoomBlade() with
		{
			OwnerId = _ids.Player2Id,
			ControllerId = _ids.Player2Id,
		};
		(_state, _) = _state.AddObject(doomBlade, parentId: _ids.Player2HandId);

		var action = _ai.SelectAction(_state, _ids, _ids.Player1Id);

		Assert.That(
			action,
			Is.InstanceOf<CastCreatureAction>(),
			"AI should deploy Tarmogoyf rather than pass because the opponent knowing about Doom Blade "
				+ "in the lookahead should not prevent the AI from playing threats"
		);
		Assert.That(((CastCreatureAction)action).CardId, Is.EqualTo(addedTarmogoyf.Id));
	}

	// ===== BUG: ONE-ATTACK SIMULATION — AI ATTACKS FACE INSTEAD OF KILLING BLOCKER =====

	/// <summary>
	/// One-attack simulation bug: BoardOnly opponent mode runs only ONE attack action per
	/// simulated opponent turn. When Player 1 has two attackers (Tarmogoyf 7/8 + Siege Rhino
	/// 4/5, combined power = 11), the simulation only uses the stronger attacker (7 damage),
	/// making Player 2 at 11 life appear to survive at 4. In reality, Player 1 would attack
	/// with BOTH creatures, dealing lethal 11 damage. The AI therefore attacks Player 1's face
	/// (which is "safe" in the one-attack model) instead of removing a threatening creature.
	///
	/// Root cause: SimulateOpponentTurn(BoardOnly) executes a single board action instead of
	/// iterating all legal attack/ability actions until exhausted.
	///
	/// Expected to FAIL on buggy code (AI picks face attack, TargetId == Player1Id).
	/// Expected to PASS after fix (AI attacks a Player 1 creature to reduce incoming damage).
	/// </summary>
	[Test]
	public void AttackTargeting_TarmogoyfVersusLethalBoard_AttacksCreatureNotFace()
	{
		// Player 2 (AI) is at 11 life — Player 1's board (7+4=11 power) is exactly lethal
		var p2 = _state.GetPlayer(_ids.Player2Id);
		_state = _state.UpdateObject(_ids.Player2Id, p2 with { Life = 11 });

		// Player 1 at high life so Player 2 attacking face doesn't threaten a win
		var p1 = _state.GetPlayer(_ids.Player1Id);
		_state = _state.UpdateObject(_ids.Player1Id, p1 with { Life = 21 });

		// Player 2's Tarmogoyf: 9/10 (9 graveyard cards), no summoning sickness — can attack now
		for (var i = 0; i < 9; i++)
		{
			var grave = new Card
			{
				Name = "P2GraveFiller",
				ManaCost = 1,
				OwnerId = _ids.Player2Id,
				ControllerId = _ids.Player2Id,
			};
			(_state, _) = _state.AddObject(grave, parentId: _ids.Player2GraveyardId);
		}

		var p2Goyf = CardLibrary.Tarmogoyf() with
		{
			OwnerId = _ids.Player2Id,
			ControllerId = _ids.Player2Id,
		};
		var p2GoyfCC = p2Goyf.GetComponent<CreatureComponent>()!;
		p2Goyf = p2Goyf with
		{
			Components = p2Goyf.Components.Replace(
				p2GoyfCC,
				p2GoyfCC with
				{
					HasSummoningSickness = false,
				}
			),
		};
		(_state, _) = _state.AddObject(p2Goyf, parentId: _ids.Player2BattlefieldId);

		// Player 1's board: 7/8 Tarmogoyf + 4/5 Siege Rhino (combined power = 11 = lethal).
		// HasSummoningSickness = false so both can attack in the rollout simulation — without
		// this, the bug doesn't manifest because P1 can't attack at all.
		for (var i = 0; i < 7; i++)
		{
			var grave = new Card
			{
				Name = "P1GraveFiller",
				ManaCost = 1,
				OwnerId = _ids.Player1Id,
				ControllerId = _ids.Player1Id,
			};
			(_state, _) = _state.AddObject(grave, parentId: _ids.Player1GraveyardId);
		}

		var p1Goyf = CardLibrary.Tarmogoyf() with
		{
			OwnerId = _ids.Player1Id,
			ControllerId = _ids.Player1Id,
		};
		var p1GoyfCC = p1Goyf.GetComponent<CreatureComponent>()!;
		p1Goyf = p1Goyf with
		{
			Components = p1Goyf.Components.Replace(
				p1GoyfCC,
				p1GoyfCC with
				{
					HasSummoningSickness = false,
				}
			),
		};
		(_state, _) = _state.AddObject(p1Goyf, parentId: _ids.Player1BattlefieldId);

		var siegeRhino = CardLibrary.GetByName("Siege Rhino") with
		{
			OwnerId = _ids.Player1Id,
			ControllerId = _ids.Player1Id,
		};
		var rhinoCC = siegeRhino.GetComponent<CreatureComponent>()!;
		siegeRhino = siegeRhino with
		{
			Components = siegeRhino.Components.Replace(
				rhinoCC,
				rhinoCC with
				{
					HasSummoningSickness = false,
				}
			),
		};
		(_state, _) = _state.AddObject(siegeRhino, parentId: _ids.Player1BattlefieldId);

		var action = _ai.SelectAction(_state, _ids, _ids.Player2Id);

		Assert.That(
			action,
			Is.InstanceOf<AttackAction>(),
			"AI should attack with its Tarmogoyf rather than pass"
		);
		Assert.That(
			((AttackAction)action).TargetId,
			Is.Not.EqualTo(_ids.Player1Id),
			"AI should attack a Player 1 creature to reduce incoming damage — "
				+ "attacking face leaves P1's full board (7+4=11 power) intact for lethal next turn"
		);
	}

	// ===== REGRESSION GUARD: VISIBLE ON-BOARD THREATS STILL DETER BAD PLAYS =====

	/// <summary>
	/// Regression guard: AI should NOT deploy a 0/1 Tarmogoyf when the opponent has a
	/// 2/2 already on the battlefield that will kill it in combat for free.
	///
	/// This validates that BoardOnly mode still evaluates visible on-board threats
	/// correctly — the opponent's 2/2 attacks Tarmogoyf in the simulation (dealing 2
	/// damage to a 1-toughness creature), making deployment clearly worse than holding.
	///
	/// Must PASS both before and after any fix — if this breaks, BoardOnly is no longer
	/// evaluating visible opponent permanents.
	/// </summary>
	[Test]
	public void BoardThreat_ZeroPowerTarmogoyf_NotDeployedIntoOpponent2_2()
	{
		// Player 1 (AI): enough mana for Tarmogoyf, empty graveyard so it's a 0/1
		var p1 = _state.GetPlayer(_ids.Player1Id);
		_state = _state.UpdateObject(_ids.Player1Id, p1 with { MaxMana = 2, CurrentMana = 2 });

		var tarmogoyf = CardLibrary.Tarmogoyf() with
		{
			OwnerId = _ids.Player1Id,
			ControllerId = _ids.Player1Id,
		};
		(_state, _) = _state.AddObject(tarmogoyf, parentId: _ids.Player1HandId);

		// Opponent has a 2/2 on the battlefield, ready to attack
		var bears = CardLibrary.GrizzlyBears() with
		{
			OwnerId = _ids.Player2Id,
			ControllerId = _ids.Player2Id,
		};
		var cc = bears.GetComponent<CreatureComponent>()!;
		bears = bears with
		{
			Components = bears.Components.Replace(cc, cc with { HasSummoningSickness = false }),
		};
		(_state, _) = _state.AddObject(bears, parentId: _ids.Player2BattlefieldId);

		var action = _ai.SelectAction(_state, _ids, _ids.Player1Id);

		Assert.That(
			action,
			Is.InstanceOf<EndTurnAction>(),
			"A 0/1 Tarmogoyf should not be deployed into a 2/2 — it dies in combat for free"
		);
	}
}
