using ImmutableGameObjects;
using MtgCore;
using MtgCore.Cards.Builders;
using MtgSimulator;
using static MtgCore.Cards.Builders.TargetBuilder;

namespace MtgSimulator.Tests;

/// <summary>
/// Does the search actually take a win that is sitting on the board?
///
/// "Lethal move detection" is the single most-repeated improvement across five years of the
/// Strategy Card Game AI Competition — every entrant named it as one of the two biggest wins. But
/// this engine has no blocking, so attacking costs nothing and every individual attack scores well
/// on its own; the beam may already assemble lethal without help, one attack per SelectAction
/// call.
///
/// **These tests exist to find out before anything is built.** A fix for a gap that is not there
/// is worse than no fix: it adds a pre-search pass every move, forever, to solve a problem the
/// search was already solving.
/// </summary>
[TestFixture]
public class LethalDetectionTests
{
	private GameState _state;
	private MtgGameIds _ids;

	[SetUp]
	public void Setup()
	{
		(_state, _ids) = MtgGameFactory.Create();
		// Hand-built board, so libraries are empty. Without this the decking rule decides the
		// game and EndTurn wins outright, which would mask everything these tests ask about.
		_state = _state.WithoutDeckingLoss();
	}

	/// <summary>Puts a creature that can attack this turn onto the given battlefield.</summary>
	private void AddAttacker(int ownerId, int battlefieldId, int power, string name)
	{
		var card = new Card
		{
			Name = name,
			ManaCost = 1,
			OwnerId = ownerId,
			ControllerId = ownerId,
			Components =
			[
				new CreatureComponent
				{
					Power = power,
					Toughness = power,
					HasSummoningSickness = false,
				},
			],
		};
		(_state, _) = _state.AddObject(card, parentId: battlefieldId);
	}

	private void SetLife(int playerId, int life)
	{
		var p = _state.GetPlayer(playerId);
		_state = _state.UpdateObject(playerId, p with { Life = life });
	}

	/// <summary>
	/// Plays out the AI's turn the way GameRunner does — SelectAction, apply, repeat — and reports
	/// whether the opponent died. This is the honest question: not "does one call return an
	/// attack" but "does the turn end with the opponent dead", since SelectAction re-runs after
	/// every action and can assemble lethal across several calls.
	/// </summary>
	private bool PlayTurnAndCheckOpponentDies(int playerId, int opponentId, int maxActions = 40)
	{
		var strategy = new MultiTurnBeamSearchAiStrategy(_ids, rng: new Random(7));
		var state = _state;

		for (var i = 0; i < maxActions; i++)
		{
			if (state.GetPlayer(opponentId).HasLost)
				return true;

			if (state.IsWaitingForChoice)
			{
				var choice = state.GetPendingChoice();
				if (choice == null)
					break;
				(state, _) = state.ResolveChoice(strategy.ResolveChoice(state, choice, playerId));
				continue;
			}

			var legal = MtgActionGenerator.GetLegalActions(state, _ids, playerId);
			if (legal.Count == 0)
				break;

			var action = strategy.SelectAction(state, _ids, playerId);
			if (action is EndTurnAction)
				break;

			(state, _) = state.AddAction(action).ProcessAllActions();
		}

		return state.GetPlayer(opponentId).HasLost;
	}

	[Test]
	public void OneAttackerWithExactLethal_Wins()
	{
		AddAttacker(_ids.Player1Id, _ids.Player1BattlefieldId, power: 6, name: "Ogre");
		SetLife(_ids.Player2Id, 6);

		Assert.That(
			PlayTurnAndCheckOpponentDies(_ids.Player1Id, _ids.Player2Id),
			Is.True,
			"a single attacker with exactly lethal must be sent"
		);
	}

	[Test]
	public void ThreeAttackersWithExactLethal_Wins()
	{
		for (var i = 0; i < 3; i++)
			AddAttacker(_ids.Player1Id, _ids.Player1BattlefieldId, power: 3, name: $"Bear{i}");
		SetLife(_ids.Player2Id, 9);

		Assert.That(
			PlayTurnAndCheckOpponentDies(_ids.Player1Id, _ids.Player2Id),
			Is.True,
			"lethal spread across three attackers must be assembled"
		);
	}

	/// <summary>
	/// The case the branching caps make interesting. DefaultMaxBranching is 16 at the root and
	/// DefaultExpandBranching is 5 below it, so a wide board cannot explore every attack ordering
	/// — the premise being that SelectAction re-runs and re-offers what it pruned.
	/// </summary>
	[Test]
	public void TwentyAttackersWithExactLethal_Wins()
	{
		for (var i = 0; i < 20; i++)
			AddAttacker(_ids.Player1Id, _ids.Player1BattlefieldId, power: 1, name: $"Goblin{i}");
		SetLife(_ids.Player2Id, 20);

		Assert.That(
			PlayTurnAndCheckOpponentDies(_ids.Player1Id, _ids.Player2Id),
			Is.True,
			"a wide board at exactly lethal must still close, despite the branching caps"
		);
	}

	/// <summary>
	/// Lethal while the opponent has a bigger board. The race term should already favour this, but
	/// it is the position where an evaluator that liked its own board could talk itself out of the
	/// win — attacking with everything looks like giving up board presence.
	/// </summary>
	[Test]
	public void LethalIntoABiggerOpposingBoard_StillWins()
	{
		for (var i = 0; i < 4; i++)
			AddAttacker(_ids.Player1Id, _ids.Player1BattlefieldId, power: 2, name: $"Mine{i}");
		for (var i = 0; i < 6; i++)
			AddAttacker(_ids.Player2Id, _ids.Player2BattlefieldId, power: 5, name: $"Theirs{i}");
		SetLife(_ids.Player2Id, 8);

		Assert.That(
			PlayTurnAndCheckOpponentDies(_ids.Player1Id, _ids.Player2Id),
			Is.True,
			"the win is the win, even when the opponent's board is far scarier"
		);
	}

	/// <summary>
	/// Not lethal — one point short. Pinned so the tests above cannot pass by way of an AI that
	/// simply always attacks with everything, which would make them prove nothing.
	/// </summary>
	[Test]
	public void OneShortOfLethal_DoesNotWin()
	{
		for (var i = 0; i < 3; i++)
			AddAttacker(_ids.Player1Id, _ids.Player1BattlefieldId, power: 3, name: $"Bear{i}");
		SetLife(_ids.Player2Id, 10);

		Assert.That(
			PlayTurnAndCheckOpponentDies(_ids.Player1Id, _ids.Player2Id),
			Is.False,
			"nine power cannot kill through ten life; if this passes the fixture proves nothing"
		);
	}

	private void AddToHand(int ownerId, int handId, Card template)
	{
		var card = template with { OwnerId = ownerId, ControllerId = ownerId };
		(_state, _) = _state.AddObject(card, parentId: handId);
	}

	private void SetMana(int playerId, int mana)
	{
		var p = _state.GetPlayer(playerId);
		_state = _state.UpdateObject(playerId, p with { MaxMana = mana, CurrentMana = mana });
	}

	// ===== LETHAL THAT NEEDS A NON-ATTACK ACTION =====
	//
	// Everything above is reachable by attacking, which costs nothing in an engine with no
	// blocking, so each step is individually attractive and the beam walks into the win. These two
	// are different: the enabling action is NOT individually attractive, so the search has to see
	// the combination or it never plays it.

	/// <summary>
	/// Three damage on board, three in hand, opponent at six. Neither half is lethal alone, and
	/// spending a card to deal three to a player is a poor trade in isolation — the evaluator
	/// prices it at 3 x LifeWeight = 0.6 against 1.4 for the card leaving hand.
	/// </summary>
	[Test]
	public void BurnPlusAttack_IsFoundWhenNeitherHalfIsLethal()
	{
		AddAttacker(_ids.Player1Id, _ids.Player1BattlefieldId, power: 3, name: "Ogre");
		SetMana(_ids.Player1Id, 1);
		AddToHand(
			_ids.Player1Id,
			_ids.Player1HandId,
			CardFactory
				.Instant("Bolt", manaCost: 1)
				.WithDamage(3)
				.WithTarget(Single().PlayersOrCreatures())
				.Build()
		);
		SetLife(_ids.Player2Id, 6);

		Assert.That(
			PlayTurnAndCheckOpponentDies(_ids.Player1Id, _ids.Player2Id),
			Is.True,
			"3 on board + 3 in hand kills through 6; neither half does it alone"
		);
	}

	/// <summary>
	/// The case with a specific mechanical reason to fail. StateEvaluator scores power via
	/// GetEffectivePermanentPower, which deliberately EXCLUDES UntilEndOfTurn buffs — so a pump
	/// spell contributes exactly nothing to the score, and the only thing the evaluator sees is
	/// the card leaving hand. The pump is strictly negative right up until it is lethal.
	/// </summary>
	[Test]
	public void PumpPlusAttack_IsFoundWhenTheAttackAloneIsNotLethal()
	{
		AddAttacker(_ids.Player1Id, _ids.Player1BattlefieldId, power: 2, name: "Bear");
		SetMana(_ids.Player1Id, 1);
		AddToHand(
			_ids.Player1Id,
			_ids.Player1HandId,
			CardFactory
				.Instant("Growth", manaCost: 1)
				.WithBoost(3, 3)
				.WithTarget(Single().YourCreatures())
				.Build()
		);
		SetLife(_ids.Player2Id, 5);

		Assert.That(
			PlayTurnAndCheckOpponentDies(_ids.Player1Id, _ids.Player2Id),
			Is.True,
			"a 2/2 pumped to 5/5 is exactly lethal through 5 life"
		);
	}
}
