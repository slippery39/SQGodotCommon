using System.Collections.Immutable;
using ImmutableGameObjects;
using MtgCore;
using MtgSimulator;

namespace MtgSimulator.Tests;

/// <summary>
/// **Why doesn't the Twin combo run? Three candidate causes that look identical from a report.**
///
/// Mode 7 reads `assem 70%, depth 1.0` — the copier gets cast, and its ability is activated at most
/// once. Depth is how we know: a running loop puts token copies onto the battlefield carrying the
/// UNTAPPERS' names, so `EngineProbe` counts each token as another enabler deployed and depth climbs
/// with the iterations. Pinned at 1.0 means the loop turns over at most once.
///
/// A goldfish cannot tell these apart, so this isolates the DECISION instead:
///
///   1. The activation is never OFFERED — no legal Illusionist target, or the cost cannot be paid.
///   2. It is offered and the AI DECLINES it — the evaluator prefers something else.
///   3. It is taken once and the AI does not go again — the loop is available and unexplored.
///
/// The board is built by hand, so both libraries are empty; `WithoutDeckingLoss` is required or
/// ending the turn decks the opponent and wins outright, which would decide every test here.
/// </summary>
[TestFixture]
public class TwinComboPilotTests
{
	private GameState _state;
	private MtgGameIds _ids;

	[SetUp]
	public void Setup()
	{
		(_state, _ids) = MtgGameFactory.Create();
		_state = _state.WithoutDeckingLoss();

		var p1 = _state.GetPlayer(_ids.Player1Id);
		_state = _state.UpdateObject(_ids.Player1Id, p1 with { CurrentMana = 6, MaxMana = 6 });
	}

	private static Card Card(string name) => ComboProving.Cards.Single(c => c.Name == name);

	/// <summary>
	/// **Cause 1.** With a copier and an untapper both in play and mana available, the engine must
	/// at least OFFER the activation. If it does not, nothing downstream matters.
	/// </summary>
	[Test]
	public void TheActivationIsOffered()
	{
		var (s, copier) = Play(_state, Card("Twinflame Artisan"));
		(s, _) = Play(s, Card("Mirevale Deceiver"));

		var legal = MtgActionGenerator.GetLegalActions(s, _ids, _ids.Player1Id);
		var activations = legal
			.OfType<ActivateAbilityAction>()
			.Where(a => a.CardId == copier.Id)
			.ToList();

		TestContext.Out.WriteLine(
			$"{legal.Count} legal actions; {activations.Count} are the copier's ability"
		);

		Assert.That(activations, Is.Not.Empty, "the copier's ability was never offered");
	}

	/// <summary>
	/// **Cause 2.** Offered is not chosen. This asks the REAL production AI what it does from that
	/// position and prints its pick, so a refusal is visible rather than inferred.
	/// </summary>
	[Test]
	public void TheAiChoosesToActivateIt()
	{
		var (s, copier) = Play(_state, Card("Twinflame Artisan"));
		(s, _) = Play(s, Card("Mirevale Deceiver"));

		var ai = new MultiTurnBeamSearchAiStrategy(_ids, rng: new Random(42));
		var chosen = ai.SelectAction(s, _ids, _ids.Player1Id);

		TestContext.Out.WriteLine($"AI chose: {Describe(chosen)}");

		Assert.That(
			chosen,
			Is.InstanceOf<ActivateAbilityAction>(),
			"the AI declined a free, repeatable token-copy ability"
		);
		Assert.That(((ActivateAbilityAction)chosen).CardId, Is.EqualTo(copier.Id));
	}

	/// <summary>
	/// **Cause 3.** The loop, driven by the AI rather than by the test. Six decisions from a board
	/// holding both halves: if the engine and the pilot both work, bodies should keep arriving.
	/// </summary>
	[Test]
	public void TheAiTurnsTheLoopOverRepeatedly()
	{
		var (s, _) = Play(_state, Card("Twinflame Artisan"));
		(s, _) = Play(s, Card("Mirevale Deceiver"));

		var ai = new MultiTurnBeamSearchAiStrategy(_ids, rng: new Random(42));
		var before = Creatures(s);

		for (var i = 0; i < 6; i++)
		{
			var action = ai.SelectAction(s, _ids, _ids.Player1Id);
			TestContext.Out.WriteLine(
				$"  step {i + 1}: {Describe(action)}  (creatures {Creatures(s)})"
			);
			if (action is EndTurnAction)
				break;
			s = Execute(s, action);
		}

		TestContext.Out.WriteLine($"creatures {before} -> {Creatures(s)}");

		Assert.That(
			Creatures(s),
			Is.GreaterThan(before + 1),
			"the AI made at most one copy — the loop is available and it is not being taken"
		);
	}

	/// <summary>
	/// **The control: lethal in ONE attack must be taken.** `FindWinner` short-circuits the beam
	/// when a node reaches a win, so if this fails the lethal check is broken outright and the test
	/// below would be measuring nothing.
	/// </summary>
	[Test]
	public void LethalInOneAttack_IsTaken()
	{
		var s = SetOpponentLife(_state, 1);
		(s, _) = PlayVanilla(s, "Striker", power: 1, count: 1);

		var ai = new MultiTurnBeamSearchAiStrategy(_ids, rng: new Random(42));
		var chosen = ai.SelectAction(s, _ids, _ids.Player1Id);

		TestContext.Out.WriteLine($"AI chose: {Describe(chosen)}");
		Assert.That(
			chosen,
			Is.InstanceOf<AttackAction>(),
			"one swing wins and the AI did not take it"
		);
	}

	/// <summary>
	/// **The real case, and the one the Twin deck actually faces.** Six hasty 1/1s against an
	/// opponent at 6: lethal is on the board, but reaching it costs SIX sequential `AttackAction`s.
	///
	/// The search is depth 3 with an expansion branching of 5, so a six-action line is past the
	/// horizon — `FindWinner` can only see a win it can actually reach inside the beam. The measured
	/// consequence in a real game is a combo deck that assembles on turn 2, activates 198 times,
	/// and ends in `ActionLimitReached` without ever swinging.
	///
	/// If this fails, the gap is the horizon, not the lethal check, and it is a piloting bug that
	/// affects every wide board rather than anything specific to this combo.
	/// </summary>
	[Test]
	public void LethalInSixAttacks_IsAlsoTaken()
	{
		var s = SetOpponentLife(_state, 6);
		(s, _) = PlayVanilla(s, "Striker", power: 1, count: 6);

		var ai = new MultiTurnBeamSearchAiStrategy(_ids, rng: new Random(42));
		var chosen = ai.SelectAction(s, _ids, _ids.Player1Id);

		TestContext.Out.WriteLine($"AI chose: {Describe(chosen)}");
		Assert.That(
			chosen,
			Is.InstanceOf<AttackAction>(),
			"lethal is on the board but needs six swings — the AI did not start attacking"
		);
	}

	/// <summary>
	/// **The case that motivated the lethal pass, and the one the beam could not solve.**
	///
	/// `LethalDetectionTests` concluded the search needs no lethal check, on the premise that
	/// *"attacking costs nothing and every individual attack scores well on its own"* — so the beam
	/// assembles lethal one attack per `SelectAction` call. True, but only while nothing outscores a
	/// swing. A repeatable free ability is +1 creature at evaluator weight 3.0 against a point of
	/// damage at life weight 0.2, so the incremental assembly never STARTS.
	///
	/// `FindWinner` cannot cover it either: it fires only when a node's rollout reached a win, and
	/// the rollout completes our turn with `PlayGreedyTurn`, which plays exactly ONE action. A kill
	/// needing six swings is never simulated.
	///
	/// Measured before the fix (`TwinComboGoldfishDiagnostic`): the deck assembled on turn 2,
	/// activated its copier **198 times**, and ended in `ActionLimitReached` — a draw — while
	/// holding lethal throughout.
	/// </summary>
	[Test]
	public void WithLethalOnBoard_TheAiAttacksInsteadOfCombing()
	{
		var s = SetOpponentLife(_state, 6);
		(s, _) = PlayVanilla(s, "Striker", power: 1, count: 6);
		(s, _) = Play(s, Card("Twinflame Artisan"));
		(s, _) = Play(s, Card("Mirevale Deceiver"));

		var ai = new MultiTurnBeamSearchAiStrategy(_ids, rng: new Random(42));
		var chosen = ai.SelectAction(s, _ids, _ids.Player1Id);

		TestContext.Out.WriteLine($"AI chose: {Describe(chosen)}");
		Assert.That(
			chosen,
			Is.InstanceOf<AttackAction>(),
			"it had lethal and made another token instead"
		);
	}

	// ===== helpers =====

	private GameState SetOpponentLife(GameState s, int life)
	{
		var p2 = s.GetPlayer(_ids.Player2Id);
		return s.UpdateObject(_ids.Player2Id, p2 with { Life = life });
	}

	private (GameState, Card) PlayVanilla(GameState state, string name, int power, int count)
	{
		Card last = null!;
		for (var i = 0; i < count; i++)
		{
			var (next, placed) = state.AddObject(
				new Card
				{
					Name = $"{name} {i}",
					OwnerId = _ids.Player1Id,
					ControllerId = _ids.Player1Id,
					Components = ImmutableArray.Create<GameComponent>(
						new PermanentComponent(),
						new CreatureComponent
						{
							Power = power,
							Toughness = 1,
							HasSummoningSickness = false,
						}
					),
				},
				parentId: _ids.Player1BattlefieldId
			);
			state = next;
			last = (Card)placed;
		}
		return (state, last);
	}

	private static string Describe(GameAction a) =>
		a switch
		{
			ActivateAbilityAction x => $"Activate ability {x.AbilityIndex} on card {x.CardId}",
			CastCreatureAction x => $"Cast creature {x.CardId}",
			AttackAction => "Attack",
			EndTurnAction => "EndTurn",
			_ => a.GetType().Name,
		};

	private int Creatures(GameState s) =>
		s.GetCardsInZone(_ids.Player1BattlefieldId).Count(c => c.HasComponent<CreatureComponent>());

	private GameState Execute(GameState s, GameAction action)
	{
		var (next, _) = s.AddAction(action).ProcessAllActions();
		return next;
	}

	private (GameState, Card) Play(GameState state, Card card)
	{
		var (next, _) = state
			.AddAction(
				new PutIntoBattlefieldAction
				{
					CardTemplate = card with
					{
						OwnerId = _ids.Player1Id,
						ControllerId = _ids.Player1Id,
					},
				}
			)
			.ProcessAllActions();

		var placed = next.GetCardsInZone(_ids.Player1BattlefieldId).Last(c => c.Name == card.Name);
		var creature = placed.GetComponent<CreatureComponent>()!;
		next = next.UpdateObject(
			placed.Id,
			placed.WithComponentReplaced(creature with { HasSummoningSickness = false })
		);
		return (next, (Card)next.GetObject(placed.Id));
	}
}
