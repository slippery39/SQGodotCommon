using System.Collections.Immutable;
using ImmutableGameObjects;
using MtgCore;
using MtgCore.Cards.Builders;
using MtgSimulator;

namespace MtgSimulator.Tests;

/// <summary>
/// Does the AI know WHICH card to discard?
///
/// `DiscardChoiceStabilityTests` asserts it does not crash on a discard choice. Nothing asserts it
/// picks the right card, and there is a structural reason to doubt it: `CardsInHandWeight` scores
/// every hand card at a flat 1.4, so a bomb and a blank produce an identical hand count.
///
/// **The rollout is the reason this needs measuring rather than arguing.** `ScoreAfterCompletingTurn`
/// finishes the turn and rolls two more, and `PlayGreedyTurn` casts one spell per simulated turn —
/// so for a card it can actually reach, the rollout OBSERVES the difference and no static term is
/// needed. That is exactly why four previous evaluator changes measured neutral. The gap, if there
/// is one, is confined to cards the rollout cannot reach:
///
/// | Case | Bomb castable within the lookahead? | Rollout can see it? |
/// |---|---|---|
/// | A | yes — both cards affordable now | expected yes |
/// | B | no — bomb costs far more than available mana | expected NO |
///
/// Case B is the Griselbrand-on-turn-1 shape, and it is the one a card-value term would fix.
/// </summary>
[TestFixture]
public class DiscardQualityTests
{
	private const int LookaheadTurns = 2;

	/// <summary>A card worth keeping: a huge body, at <paramref name="manaCost"/>.</summary>
	private static Card Bomb(int ownerId, int manaCost) =>
		CardFactory.Creature("Bomb", manaCost: manaCost, power: 8, toughness: 8).Build() with
		{
			OwnerId = ownerId,
			ControllerId = ownerId,
		};

	/// <summary>A card worth pitching, at the SAME cost so castability cannot confound the pair.</summary>
	private static Card Chaff(int ownerId, int manaCost) =>
		CardFactory.Creature("Chaff", manaCost: manaCost, power: 1, toughness: 1).Build() with
		{
			OwnerId = ownerId,
			ControllerId = ownerId,
		};

	private static Card Pitch(int ownerId) =>
		CardFactory.Spell("Pitch", manaCost: 0).WithDiscard().Build() with
		{
			OwnerId = ownerId,
			ControllerId = ownerId,
		};

	[Test]
	public void BothCardsAffordable_TheRolloutSeesTheDifference() =>
		Assert.That(
			Report("A: both affordable", mana: 10, bodyCost: 2).Spread,
			Is.GreaterThan(0.01f),
			"with both cards castable inside the lookahead the rollout plays them and should "
				+ "distinguish them; if this is flat, the gap is wider than the hand term"
		);

	/// <summary>
	/// The case a card-value term exists to fix. **This test asserts the DEFECT**, so it fails the
	/// day the gap closes — which is the point at which the hand-quality work has a measurable
	/// target and this assertion should be inverted.
	/// </summary>
	[Test]
	public void BombBeyondTheLookahead_TheRolloutIsBlindToIt()
	{
		var report = Report("B: bomb unreachable", mana: 2, bodyCost: 8);

		Assert.That(
			report.Spread,
			Is.LessThan(0.01f),
			"if this now differs, the rollout has learned to see an uncastable card and the "
				+ "hand-quality term may be unnecessary — re-measure before building it"
		);
		Assert.That(
			report.DiscardedBomb,
			Is.True,
			"blind to the difference, the AI should be falling through to its tiebreak and "
				+ "pitching whichever card sorts first — here, the bomb"
		);
	}

	/// <summary>
	/// The acceptance test for card-value scoring: the same unreachable-bomb position the rollout
	/// is blind to, with a value table supplied to ResolveChoice. It must flip the choice.
	///
	/// The table is INLINE rather than loaded from `sim_results/` — a test that depends on a
	/// generated file passes or fails on whether someone has run a sweep.
	/// </summary>
	[Test]
	public void WithCardValues_TheUnreachableBombIsKept()
	{
		var report = Report("B + card values", mana: 2, bodyCost: 8, Table());

		Assert.That(report.DiscardedBomb, Is.False, "it should pitch the Chaff and keep the Bomb");
	}

	/// <summary>
	/// Reachability: an eight-drop must NOT be hoarded over a playable two-drop on turn one, even
	/// though the eight-drop is worth far more once cast. The reach discount is what decides it.
	/// </summary>
	[Test]
	public void AnUncastableBombLosesToAPlayableCard_OnTurnOne()
	{
		var (state, ids) = MtgGameFactory.Create();
		state = state.WithoutDeckingLoss();

		foreach (var pid in new[] { ids.Player1Id, ids.Player2Id })
		{
			var p = state.GetPlayer(pid);
			state = state.UpdateObject(pid, p with { MaxMana = 2, CurrentMana = 2 });
		}

		// Bomb costs 8 and is worth 50; Playable costs 2 and is worth only 12. At two mana the
		// bomb is six turns away, so 0.75^6 leaves it worth ~8.9 against the playable card's 12.
		(state, _) = state.AddObject(Bomb(ids.Player1Id, 8), ids.Player1HandId);
		(state, _) = state.AddObject(
			CardFactory.Creature("Playable", manaCost: 2, power: 2, toughness: 2).Build() with
			{
				OwnerId = ids.Player1Id,
				ControllerId = ids.Player1Id,
			},
			ids.Player1HandId
		);

		var (withSpell, spell) = state.AddObject(Pitch(ids.Player1Id), ids.Player1HandId);
		var (atChoice, _) = withSpell
			.AddAction(new CastSpellAction { CardId = spell.Id, CastingPlayerId = ids.Player1Id })
			.ProcessAllActions();

		var table = new CardValueTable(
			new Dictionary<string, float> { ["Bomb"] = 50f, ["Playable"] = 12f },
			weight: 1.0f
		);
		var ai = new MultiTurnBeamSearchAiStrategy(
			ids,
			lookaheadTurns: LookaheadTurns,
			cardValues: table
		);

		var chosen = ai.ResolveChoice(atChoice, atChoice.GetPendingChoice()!, ids.Player1Id);
		var name = NameOf(atChoice, chosen);
		TestContext.Out.WriteLine($"turn-one discard with an 8-drop in hand: pitched {name}");

		Assert.That(name, Is.EqualTo("Bomb"), "the unreachable 8-drop should be the one pitched");
	}

	/// <summary>
	/// The guard against what the evaluator version got wrong: card values must never be consulted
	/// about anything but a choice. A land drop must be worth its full mana with the table supplied.
	/// </summary>
	[Test]
	public void CardValues_DoNotTouchTheLandDropDecision()
	{
		var (state, ids) = MtgGameFactory.Create();
		state = state.WithoutDeckingLoss();

		var player = state.GetPlayer(ids.Player1Id);
		state = state.UpdateObject(ids.Player1Id, player with { MaxMana = 7, CurrentMana = 7 });
		(state, _) = state.AddObject(Bomb(ids.Player1Id, 8), ids.Player1HandId);
		(state, var land) = state.AddObject(
			new Card
			{
				Name = "Plains",
				OwnerId = ids.Player1Id,
				ControllerId = ids.Player1Id,
				Subtypes = ImmutableHashSet.Create("Land"),
			},
			ids.Player1HandId
		);

		var before = StateEvaluator.Evaluate(state, ids, ids.Player1Id);
		var (after, _) = state
			.AddAction(new PlayLandAction { CardId = land.Id, CastingPlayerId = ids.Player1Id })
			.ProcessAllActions();

		Assert.That(
			StateEvaluator.Evaluate(after, ids, ids.Player1Id) - before,
			Is.GreaterThanOrEqualTo(2.0f),
			"the evaluator must know nothing about card values — an earlier version put this term "
				+ "in the evaluator and made a 7->8 land drop score -10.50, costing 24.6% win rate"
		);
	}

	private static CardValueTable Table() =>
		new(new Dictionary<string, float> { ["Bomb"] = 50f, ["Chaff"] = 1f }, weight: 1.0f);

	private readonly record struct Outcome(float Spread, bool DiscardedBomb);

	/// <summary>
	/// Casts a discard spell and scores EVERY option with the same rollout `ResolveChoice` uses,
	/// then reports the spread. A spread of zero means every option is the same position to the
	/// search, and the choice falls through to a tiebreak.
	/// </summary>
	private static Outcome Report(
		string label,
		int mana,
		int bodyCost,
		CardValueTable? cardValues = null
	)
	{
		var (state, ids) = MtgGameFactory.Create();
		state = state.WithoutDeckingLoss();

		foreach (var pid in new[] { ids.Player1Id, ids.Player2Id })
		{
			var player = state.GetPlayer(pid);
			state = state.UpdateObject(pid, player with { MaxMana = mana, CurrentMana = mana });
		}

		// Hand order puts the bomb FIRST, so "pitched the bomb" is what a zero-information
		// tiebreak produces — the failure is legible rather than a coin flip.
		(state, _) = state.AddObject(Bomb(ids.Player1Id, bodyCost), ids.Player1HandId);
		(state, _) = state.AddObject(Chaff(ids.Player1Id, bodyCost), ids.Player1HandId);

		var (withSpell, spell) = state.AddObject(Pitch(ids.Player1Id), ids.Player1HandId);
		var (atChoice, _) = withSpell
			.AddAction(new CastSpellAction { CardId = spell.Id, CastingPlayerId = ids.Player1Id })
			.ProcessAllActions();

		Assert.That(atChoice.IsWaitingForChoice, Is.True, "discard should pause the pipeline");
		var choice = atChoice.GetPendingChoice()!;

		var ai = new MultiTurnBeamSearchAiStrategy(
			ids,
			lookaheadTurns: LookaheadTurns,
			cardValues: cardValues
		);
		var scores = new List<(string Name, float Score)>();

		foreach (var option in choice.Options)
		{
			// Also verifies ChoiceOption.Id is a CARD id — a card-value lookup depends on it, and
			// an id that resolves to something else would prune nothing while erroring nowhere.
			var name =
				atChoice.HasObject(option.Id) && atChoice.GetObject(option.Id) is Card card
					? card.Name
					: $"<not a card: {option.Id}>";

			var (resolved, _) = atChoice.ResolveChoice(ImmutableList.Create(option.Id));
			scores.Add((name, ai.ScoreAfterCompletingTurn(resolved, ids.Player1Id)));
		}

		var chosen = ai.ResolveChoice(atChoice, choice, ids.Player1Id);
		var chosenName = scores.Count == 0 ? "" : NameOf(atChoice, chosen);

		TestContext.Out.WriteLine($"=== {label} (mana {mana}, bodies cost {bodyCost}) ===");
		foreach (var (name, score) in scores)
			TestContext.Out.WriteLine($"   discard {name, -6} -> {score, 9:F3}");
		TestContext.Out.WriteLine($"   AI discarded: {chosenName}");

		var spread = scores.Count == 0 ? 0f : scores.Max(s => s.Score) - scores.Min(s => s.Score);
		TestContext.Out.WriteLine($"   spread: {spread:F4}\n");

		return new Outcome(spread, chosenName == "Bomb");
	}

	private static string NameOf(GameState state, ImmutableList<int> selection) =>
		selection.Count > 0
		&& state.HasObject(selection[0])
		&& state.GetObject(selection[0]) is Card card
			? card.Name
			: "(nothing)";
}
