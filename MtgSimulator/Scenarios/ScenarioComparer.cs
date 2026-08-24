using System.Diagnostics;
using System.Text;
using ImmutableGameObjects;
using MtgCore;

namespace MtgSimulator.Scenarios;

/// <summary>
/// What each strategy does when handed the same position.
///
/// <see cref="Score"/> is the strategy's own ranking of its chosen action — a rollout score for the
/// multi-turn search, an immediate score for the plain beam — so it is NOT comparable between
/// rows. <see cref="Breakdown"/> is, because every strategy's breakdown comes from the same
/// <c>StateEvaluator</c> applied to the position that strategy's choice leads to.
/// </summary>
public sealed record StrategyResult(
	string StrategyName,
	string ChosenAction,
	float Score,
	long ElapsedMs,
	EvaluationBreakdown? Breakdown,
	string? Error
);

/// <summary>
/// Runs several AI strategies against one scenario and reports what each did.
///
/// This is the half of the tooling the in-game overlay cannot do. The overlay shows one strategy
/// deciding live; this shows several deciding the SAME position, which is the only way to tell
/// whether a change helped or whether the board simply differed.
/// </summary>
public static class ScenarioComparer
{
	/// <summary>
	/// Every strategy gets its own RNG at the same seed, so a difference between rows is the
	/// strategy and never the dice. Sequential on purpose — the timings are part of the output and
	/// running these in parallel would have them contend for cores and report noise.
	/// </summary>
	public static IReadOnlyList<StrategyResult> Compare(
		Scenario scenario,
		IReadOnlyList<(string Name, Func<MtgGameIds, Random, IAiStrategy> Build)> strategies,
		MtgGameIds ids,
		int rngSeed = 99
	)
	{
		var results = new List<StrategyResult>(strategies.Count);

		foreach (var (name, build) in strategies)
		{
			var strategy = build(ids, new Random(rngSeed));
			var stopwatch = Stopwatch.StartNew();
			try
			{
				var action = strategy.SelectAction(scenario.State, ids, scenario.PlayerToMove);
				stopwatch.Stop();

				var score = (strategy as ICapturingAiStrategy)?.LastDecision?.Score ?? 0f;
				var breakdown = ExplainAfter(scenario, ids, action);

				results.Add(
					new StrategyResult(
						name,
						ActionDescriber.Describe(action, scenario.State),
						score,
						stopwatch.ElapsedMilliseconds,
						breakdown,
						null
					)
				);
			}
			catch (Exception ex)
			{
				// A strategy that throws on a saved position is itself a finding worth seeing next
				// to the others, so it becomes a row rather than taking the whole comparison down.
				stopwatch.Stop();
				results.Add(
					new StrategyResult(
						name,
						"-",
						0f,
						stopwatch.ElapsedMilliseconds,
						null,
						ex.Message
					)
				);
			}
		}

		return results;
	}

	private static EvaluationBreakdown? ExplainAfter(
		Scenario scenario,
		MtgGameIds ids,
		GameAction action
	)
	{
		try
		{
			var (after, _) = scenario.State.AddAction(action).ProcessAllActions();
			return StateEvaluator.Explain(after, ids, scenario.PlayerToMove);
		}
		catch
		{
			return null;
		}
	}

	/// <summary>
	/// The comparison as a fixed-width table. Terms run down the rows and strategies across the
	/// columns, because the question being asked is "where do these two disagree", and a column
	/// diff reads at a glance where two separate blocks do not.
	/// </summary>
	public static string Format(Scenario scenario, IReadOnlyList<StrategyResult> results)
	{
		var sb = new StringBuilder();
		var before = StateEvaluator.Explain(
			scenario.State,
			MtgGameIdsFor(scenario.State),
			scenario.PlayerToMove
		);

		sb.AppendLine($"Scenario: {scenario.Name}");
		if (!string.IsNullOrWhiteSpace(scenario.Note))
			sb.AppendLine($"  {scenario.Note}");
		sb.AppendLine(
			$"  player to move: {scenario.PlayerToMove}   position scores {before.Total:F2}"
		);
		sb.AppendLine();

		const int labelWidth = 20;
		const int colWidth = 22;

		void Row(string label, IEnumerable<string> cells)
		{
			sb.Append(label.PadRight(labelWidth));
			foreach (var cell in cells)
				sb.Append(Truncate(cell, colWidth - 1).PadLeft(colWidth));
			sb.AppendLine();
		}

		Row("", results.Select(r => r.StrategyName));
		sb.AppendLine(new string('-', labelWidth + colWidth * results.Count));
		Row("chose", results.Select(r => r.Error == null ? r.ChosenAction : "ERROR"));
		Row("ranked score", results.Select(r => r.Error == null ? $"{r.Score:F2}" : "-"));
		Row("time", results.Select(r => $"{r.ElapsedMs} ms"));
		sb.AppendLine();

		// Terms of the position each choice LEADS TO, against the position it started from.
		var termLabels = before.IsTerminal ? [] : before.Terms.Select(t => t.Label).ToList();
		for (var i = 0; i < termLabels.Count; i++)
		{
			var index = i;
			Row(
				"  " + termLabels[i],
				results.Select(r =>
				{
					var terms = r.Breakdown?.Terms.ToList();
					if (terms == null || terms.Count != termLabels.Count)
						return "-";
					return Signed(terms[index].Value - before.Terms.ElementAt(index).Value);
				})
			);
		}

		Row(
			"  total",
			results.Select(r =>
				r.Breakdown == null ? "-" : Signed(r.Breakdown.Value.Total - before.Total)
			)
		);

		var errors = results.Where(r => r.Error != null).ToList();
		if (errors.Count > 0)
		{
			sb.AppendLine();
			foreach (var r in errors)
				sb.AppendLine($"  {r.StrategyName}: {r.Error}");
		}

		return sb.ToString();
	}

	private static string Signed(float v) => v >= 0 ? $"+{v:F2}" : v.ToString("F2");

	private static string Truncate(string s, int max) => s.Length <= max ? s : s[..(max - 1)] + "…";

	/// <summary>
	/// Rebuilds the id struct from the state's own well-known registry, so a scenario loaded from
	/// disk does not need the ids that produced it saved alongside.
	/// </summary>
	public static MtgGameIds MtgGameIdsFor(GameState state) =>
		new(
			GameId: state.GetWellKnownId(MtgObjectKeys.Game),
			StackId: state.GetWellKnownId(MtgObjectKeys.Stack),
			Player1Id: state.GetWellKnownId(MtgObjectKeys.Player1),
			Player1HandId: state.GetWellKnownId(MtgObjectKeys.Player1Hand),
			Player1LibraryId: state.GetWellKnownId(MtgObjectKeys.Player1Library),
			Player1GraveyardId: state.GetWellKnownId(MtgObjectKeys.Player1Graveyard),
			Player1BattlefieldId: state.GetWellKnownId(MtgObjectKeys.Player1Battlefield),
			Player1ExileId: state.GetWellKnownId(MtgObjectKeys.Player1Exile),
			Player2Id: state.GetWellKnownId(MtgObjectKeys.Player2),
			Player2HandId: state.GetWellKnownId(MtgObjectKeys.Player2Hand),
			Player2LibraryId: state.GetWellKnownId(MtgObjectKeys.Player2Library),
			Player2GraveyardId: state.GetWellKnownId(MtgObjectKeys.Player2Graveyard),
			Player2BattlefieldId: state.GetWellKnownId(MtgObjectKeys.Player2Battlefield),
			Player2ExileId: state.GetWellKnownId(MtgObjectKeys.Player2Exile)
		);
}
