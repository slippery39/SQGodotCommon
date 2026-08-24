using ImmutableGameObjects;
using MtgCore;
using MtgSimulator;

namespace MtgSimulator.Tests;

/// <summary>
/// StateEvaluator.Explain is the implementation and Evaluate is a one-line wrapper over its
/// Total, specifically so a display path cannot drift from the scoring path. An inspector that
/// shows terms which do not sum to the score the search used is worse than no inspector — it
/// would send you hunting a discrepancy that exists only in the renderer.
///
/// These tests are that guarantee. If someone later adds a term to Evaluate without adding it to
/// the breakdown (or vice versa), the equality tests below fail rather than the AI panel quietly
/// lying.
/// </summary>
[TestFixture]
public class EvaluationBreakdownTests
{
	private GameState _state;
	private MtgGameIds _ids;

	[SetUp]
	public void Setup()
	{
		(_state, _ids) = MtgGameFactory.Create();
		// Same reason as StateEvaluatorTests: a hand-built board has empty libraries, and without
		// this the decking rule decides the game before any term matters.
		_state = _state.WithoutDeckingLoss();
	}

	[Test]
	public void Explain_TotalEqualsEvaluate_OnABalancedBoard()
	{
		var breakdown = StateEvaluator.Explain(_state, _ids, _ids.Player1Id);
		var score = StateEvaluator.Evaluate(_state, _ids, _ids.Player1Id);

		Assert.That(breakdown.Total, Is.EqualTo(score));
	}

	[Test]
	public void Explain_TotalEqualsEvaluate_WhenEveryTermIsNonZero()
	{
		// Move every term off zero at once, so a dropped term cannot hide behind a zero.
		var state = _state.UpdateObject(
			_ids.Player1Id,
			_state.GetPlayer(_ids.Player1Id) with
			{
				Life = 14,
				MaxMana = 5,
			}
		);
		state = state.UpdateObject(
			_ids.Player2Id,
			state.GetPlayer(_ids.Player2Id) with
			{
				Life = 9,
				MaxMana = 3,
			}
		);

		var breakdown = StateEvaluator.Explain(state, _ids, _ids.Player1Id);
		var score = StateEvaluator.Evaluate(state, _ids, _ids.Player1Id);

		Assert.Multiple(() =>
		{
			Assert.That(breakdown.Total, Is.EqualTo(score));
			Assert.That(breakdown.Life, Is.Not.EqualTo(0f), "life should differ on this board");
			Assert.That(breakdown.Mana, Is.Not.EqualTo(0f), "mana should differ on this board");
			Assert.That(breakdown.IsTerminal, Is.False);
		});
	}

	[Test]
	public void Explain_TermsSumToTotal()
	{
		var state = _state.UpdateObject(
			_ids.Player1Id,
			_state.GetPlayer(_ids.Player1Id) with
			{
				Life = 17,
				MaxMana = 4,
			}
		);

		var breakdown = StateEvaluator.Explain(state, _ids, _ids.Player1Id);

		// The renderer displays Terms and prints Total underneath. If those disagree the panel is
		// self-contradicting on its face.
		var summed = breakdown.Terms.Sum(t => t.Value);

		Assert.That(summed, Is.EqualTo(breakdown.Total).Within(0.0001f));
	}

	[Test]
	public void Explain_ATerminalState_ReportsOneRowNotEightZeroes()
	{
		var state = _state.UpdateObject(
			_ids.Player2Id,
			_state.GetPlayer(_ids.Player2Id) with
			{
				HasLost = true,
			}
		);

		var breakdown = StateEvaluator.Explain(state, _ids, _ids.Player1Id);

		Assert.Multiple(() =>
		{
			Assert.That(breakdown.IsTerminal, Is.True);
			Assert.That(breakdown.Total, Is.EqualTo(StateEvaluator.WinScore));
			Assert.That(
				breakdown.Total,
				Is.EqualTo(StateEvaluator.Evaluate(state, _ids, _ids.Player1Id))
			);
			Assert.That(breakdown.Terms.Count(), Is.EqualTo(1));
			Assert.That(breakdown.Terms.Single().Label, Is.EqualTo("win"));
		});
	}

	[Test]
	public void Explain_ALostState_ReportsLoss()
	{
		var state = _state.UpdateObject(
			_ids.Player1Id,
			_state.GetPlayer(_ids.Player1Id) with
			{
				HasLost = true,
			}
		);

		var breakdown = StateEvaluator.Explain(state, _ids, _ids.Player1Id);

		Assert.Multiple(() =>
		{
			Assert.That(breakdown.IsTerminal, Is.True);
			Assert.That(breakdown.Total, Is.EqualTo(StateEvaluator.LossScore));
			Assert.That(breakdown.Terms.Single().Label, Is.EqualTo("loss"));
		});
	}
}
