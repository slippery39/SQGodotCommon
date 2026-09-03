using ImmutableGameObjects;
using MtgCore;
using MtgCore.Cards.Builders;

namespace MtgSimulator.Tests;

/// <summary>
/// **Why is Wirewood Conduit's tap ability never used?** Measured: zero activations across four
/// real games. This narrows the cause instead of guessing at it.
///
/// The candidate explanations need opposite fixes, so they have to be separated:
///
/// 1. The card is never CAST in those games at all, and the activation count means nothing.
/// 2. The action is never OFFERED — a legality problem in the generator.
/// 3. It is offered and never CHOSEN — the evaluator excludes temporary mana, so activating scores
///    the same as doing nothing.
/// 4. It attacks instead, and attacking somehow spends the tap.
///
/// Note 4 is ruled out by the engine's own rules rather than by measurement: `MtgCore/CLAUDE.md`
/// records that `IsExhausted` is deliberately SEPARATE from `HasAttacked` and attacking does not set
/// it, so a creature may attack and still tap for an ability afterwards. The reverse is not true —
/// tapping first blocks the attack — which makes ORDER the thing worth checking, not exclusivity.
/// </summary>
[TestFixture]
public class ConduitBehaviourTests
{
	private const string Conduit = "Wirewood Conduit";

	private static Card Pool(string name) =>
		SetRegistry.Designed.Cards.Single(c =>
			string.Equals(c.Name, name, StringComparison.OrdinalIgnoreCase)
		);

	/// <summary>
	/// A board with the Conduit ready — in play, not summoning sick, not exhausted — and three other
	/// Elves so its "mana equal to the number of Elves" is worth four.
	/// </summary>
	private static (GameState State, MtgGameIds Ids, int ConduitId) Board(int mana)
	{
		var (state, ids) = MtgGameFactory.Create();
		state = state.WithoutDeckingLoss();

		var p1 = state.GetPlayer(ids.Player1Id);
		state = state.UpdateObject(ids.Player1Id, p1 with { CurrentMana = mana, MaxMana = mana });

		var conduitId = 0;
		foreach (
			var (name, isConduit) in new[]
			{
				(Conduit, true),
				("Llanowar Elves", false),
				("Elvish Mystic", false),
				("Wirewood Herald", false),
			}
		)
		{
			var card = Pool(name) with { OwnerId = ids.Player1Id, ControllerId = ids.Player1Id };
			var body = card.GetComponent<CreatureComponent>()!;
			card = (Card)card.WithComponentReplaced(body with { HasSummoningSickness = false });
			(state, var added) = state.AddObject(card, parentId: ids.Player1BattlefieldId);
			if (isConduit)
				conduitId = added.Id;
		}

		return (state, ids, conduitId);
	}

	/// <summary>
	/// Rules out cause 2. If the generator does not offer it, nothing downstream can choose it and
	/// the evaluator is not implicated at all.
	/// </summary>
	[Test]
	public void TheActivationIsOfferedWhenTheConduitIsReady()
	{
		var (state, ids, conduitId) = Board(mana: 5);

		var offered = MtgActionGenerator
			.GetLegalActions(state, ids, ids.Player1Id)
			.OfType<ActivateAbilityAction>()
			.Where(a => a.CardId == conduitId)
			.ToList();

		Assert.That(offered, Is.Not.Empty, "the generator never offers the Conduit's tap ability");
	}

	/// <summary>
	/// **Cause 3, and the one that matters.** The activation produces temporary mana, which
	/// `StateEvaluator` counts at exactly zero — it reads `MaxMana` only, deliberately, so that fast
	/// mana is not priced as permanent. So activating and doing nothing score identically, and the
	/// search has no reason to prefer the activation.
	///
	/// Asserted as a comparison against passing, not against a magic number: the claim is that the
	/// evaluator cannot SEE the ability, and equality is what that looks like.
	/// </summary>
	[Test]
	public void ActivatingScoresNoBetterThanDoingNothing()
	{
		var (state, ids, conduitId) = Board(mana: 5);

		var activate = MtgActionGenerator
			.GetLegalActions(state, ids, ids.Player1Id)
			.OfType<ActivateAbilityAction>()
			.First(a => a.CardId == conduitId);

		var before = StateEvaluator.Evaluate(state, ids, ids.Player1Id);
		var (after, _) = state.AddActions([activate]).ProcessAllActions();
		var scored = StateEvaluator.Evaluate(after, ids, ids.Player1Id);

		var mana =
			after.GetPlayer(ids.Player1Id).CurrentMana - state.GetPlayer(ids.Player1Id).CurrentMana;

		TestContext.Out.WriteLine(
			$"  mana gained {mana}, evaluator {before:F2} -> {scored:F2} (delta {scored - before:F2})"
		);

		Assert.Multiple(() =>
		{
			Assert.That(mana, Is.GreaterThan(0), "the ability produced no mana at all");
			Assert.That(
				scored - before,
				Is.LessThanOrEqualTo(0.01f),
				"the evaluator DOES see the mana — then the never-tapped behaviour needs another explanation"
			);
		});
	}

	/// <summary>
	/// The order question. Tapping first blocks the attack, so if the AI attacks first it keeps both
	/// options — and if it taps first it has traded an attack for mana it cannot score.
	/// </summary>
	[Test]
	public void AttackingDoesNotSpendTheTap()
	{
		var (state, ids, conduitId) = Board(mana: 5);

		var attack = MtgActionGenerator
			.GetLegalActions(state, ids, ids.Player1Id)
			.OfType<AttackAction>()
			.FirstOrDefault(a => a.AttackerId == conduitId);

		Assert.That(attack, Is.Not.Null, "the Conduit cannot attack, so ordering is not the issue");

		var (afterAttack, _) = state.AddActions([attack!]).ProcessAllActions();
		var stillOffered = MtgActionGenerator
			.GetLegalActions(afterAttack, ids, ids.Player1Id)
			.OfType<ActivateAbilityAction>()
			.Any(a => a.CardId == conduitId);

		Assert.That(
			stillOffered,
			Is.True,
			"attacking consumed the tap — then the two uses genuinely compete and the AI is choosing"
		);
	}

	/// <summary>
	/// **The line that decides whether this card is playable at all: activate, then cast something
	/// the mana just paid for.**
	///
	/// The zero score above only proves the activation is not worth taking FOR ITSELF. It says
	/// nothing about whether the search can find it as a SETUP move — and that is exactly what
	/// `FastManaPotentialEvaluator` exists for, scoring by available mana in the potential bucket so
	/// a setup line survives pruning even though its immediate score is flat. `ManaAbilityAiTests`
	/// already shows the pattern working for a Mox.
	///
	/// One mana available and a five-drop in hand: the only line that casts it is Conduit for four.
	/// If the AI cannot find this, the card is unplayable by this pilot regardless of any valuation
	/// change, and the deckbuilder must not be taught to want it.
	/// </summary>
	[Test]
	public void TheActivateThenCastLineIsFound()
	{
		var (state, ids, _) = Board(mana: 1);

		var fatty = CardFactory
			.Creature("Test Fatty", manaCost: 5, power: 5, toughness: 5)
			.Build() with
		{
			OwnerId = ids.Player1Id,
			ControllerId = ids.Player1Id,
		};
		(state, var inHand) = state.AddObject(fatty, parentId: ids.Player1HandId);

		var ai = new MultiTurnBeamSearchAiStrategy(ids, 2, rng: new Random(11));
		var cast = false;

		for (var step = 0; step < 8 && !cast; step++)
		{
			var chosen = ai.SelectAction(state, ids, ids.Player1Id);
			if (chosen is null or EndTurnAction)
				break;
			(state, _) = state.AddActions([chosen]).ProcessAllActions();
			cast = state.GetCardsInZone(ids.Player1BattlefieldId).Any(c => c.Id == inHand.Id);
		}

		TestContext.Out.WriteLine(
			$"  five-drop resolved: {cast}, mana left {state.GetPlayer(ids.Player1Id).CurrentMana}"
		);

		Assert.That(
			cast,
			Is.True,
			"the pilot never found activate-then-cast, so the Conduit is unusable by it"
		);
	}

	/// <summary>
	/// **A sink does not have to be EXPENSIVE — it can be a wide hand.**
	///
	/// The "no mana sink" reading of this deck was drawn from its curve topping out at three, but
	/// Wirewood Herald draws a card whenever an Elf enters, and the builder plays four of it. A full
	/// hand of two-drops is a sink: without the Conduit you deploy one per turn, with it you deploy
	/// three. That is exactly the elf deck's plan, so if the pilot cannot find THIS line the card is
	/// unusable in the deck it was designed for — a much stronger claim than "no expensive cards".
	///
	/// Two mana, three other Elves (Conduit taps for four), four two-drops in hand.
	/// </summary>
	[Test]
	public void TheActivateThenDeployWideLineIsFound()
	{
		var (state, ids, _) = Board(mana: 2);

		for (var i = 0; i < 4; i++)
		{
			var elf = Pool("Elvish Visionary") with
			{
				OwnerId = ids.Player1Id,
				ControllerId = ids.Player1Id,
			};
			(state, _) = state.AddObject(elf, parentId: ids.Player1HandId);
		}

		var before = state.GetCardsInZone(ids.Player1BattlefieldId).Count();
		var ai = new MultiTurnBeamSearchAiStrategy(ids, 2, rng: new Random(13));

		for (var step = 0; step < 10; step++)
		{
			var chosen = ai.SelectAction(state, ids, ids.Player1Id);
			if (chosen is null or EndTurnAction)
				break;
			(state, _) = state.AddActions([chosen]).ProcessAllActions();
		}

		var deployed = state.GetCardsInZone(ids.Player1BattlefieldId).Count() - before;
		TestContext.Out.WriteLine(
			$"  deployed {deployed} this turn, mana left {state.GetPlayer(ids.Player1Id).CurrentMana}"
		);

		// Two base mana pays for exactly one two-drop. More than one means the Conduit's mana was
		// made AND spent — which is the whole line.
		Assert.That(
			deployed,
			Is.GreaterThan(1),
			"the pilot deployed only what its base mana paid for — the Conduit's mana went unused"
		);
	}

	/// <summary>
	/// What the pilot actually does with a full board and mana to spare, reported rather than
	/// asserted: the interesting output is WHICH action wins, and pinning that would make this test
	/// a restatement of the current AI rather than a question about it.
	/// </summary>
	[Test]
	[Explicit("Diagnostic — what the pilot picks when the activation is available.")]
	public void WhatDoesThePilotChooseInstead()
	{
		var (state, ids, conduitId) = Board(mana: 5);
		var ai = new MultiTurnBeamSearchAiStrategy(ids, 2, rng: new Random(7));

		for (var step = 0; step < 6; step++)
		{
			var chosen = ai.SelectAction(state, ids, ids.Player1Id);
			if (chosen is null || chosen is EndTurnAction)
			{
				TestContext.Out.WriteLine($"  step {step}: EndTurn");
				break;
			}

			var tag = chosen switch
			{
				ActivateAbilityAction a when a.CardId == conduitId => "ACTIVATE Conduit",
				ActivateAbilityAction a => $"activate {a.CardId}",
				AttackAction a when a.AttackerId == conduitId => "ATTACK with Conduit",
				AttackAction => "attack",
				_ => chosen.GetType().Name,
			};
			TestContext.Out.WriteLine($"  step {step}: {tag}");

			(state, _) = state.AddActions([chosen]).ProcessAllActions();
		}
	}
}
