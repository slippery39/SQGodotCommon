namespace MagicCore.Ai;

/// <summary>
/// When a match has said enough (story AI-6.3): two sequential probability ratio tests on the
/// mirror-pair scores — A better by <see cref="Margin"/>, and A worse by it — each against no
/// difference, at <see cref="Error"/> per direction (a false "better" at most 5%, a false "worse"
/// at most 5%). A pair (the same decks, seed and coin flip, seats swapped) is the independent trial,
/// scoring 0, ½ or 1 for A; the log-likelihood ratio is the normal approximation chess-engine testing
/// uses for results with draws (Fishtest's GSPRT): n·(p₁ − p₀)·(2·mean − p₀ − p₁) / (2·variance).
/// Each direction stops once it decides — left running, noise could later cross its other bound.
/// </summary>
public static class EarlyStop
{
	/// <summary>The smallest difference worth finding: 5pp (Shayne, 2026-10-19).</summary>
	public const double Margin = 0.05;

	/// <summary>The chance of each wrong verdict, per direction — a difference reported that isn't there, or missed.</summary>
	public const double Error = 0.05;

	/// <summary>
	/// No verdict before this many pairs: on a first batch of mostly split pairs the sample variance is
	/// too low, and an over-confident test would call a real edge "no difference".
	/// </summary>
	public const int MinPairs = 48;

	/// <summary>
	/// A floor under the variance — every pair alike (an AI against itself) would divide by zero. Real
	/// search matches measure ~0.06 (AI-3: ±2.7pp over 78 pairs), so this never binds on them.
	/// </summary>
	const double VarianceFloor = 0.01;

	public enum Verdict
	{
		Undecided,
		Better,
		Worse,
		NoDifference,
	}

	/// <summary>Where the tests stand; a direction ruled out stays ruled out (<see cref="Judge"/>).</summary>
	public sealed record Reading(
		Verdict Verdict,
		double BetterLlr,
		double WorseLlr,
		double Bound,
		bool BetterRuledOut = false,
		bool WorseRuledOut = false
	);

	/// <summary>
	/// Where the tests stand on these pair scores (A's score in each mirror pair), carrying forward what
	/// <paramref name="before"/> — the last judgement of the same run — already ruled out.
	/// </summary>
	public static Reading Judge(IReadOnlyList<double> pairScores, Reading? before = null)
	{
		var bound = Math.Log((1 - Error) / Error);
		var n = pairScores.Count;
		if (n < MinPairs)
			return new(Verdict.Undecided, 0, 0, bound);
		var mean = pairScores.Average();
		var variance = Math.Max(VarianceFloor, Variance(pairScores));
		double Llr(double p1) => n * (p1 - 0.5) * (2 * mean - 0.5 - p1) / (2 * variance);
		var (better, worse) = (Llr(0.5 + Margin), Llr(0.5 - Margin));
		var betterOut = before?.BetterRuledOut == true || better <= -bound;
		var worseOut = before?.WorseRuledOut == true || worse <= -bound;
		var verdict =
			!betterOut && better >= bound ? Verdict.Better
			: !worseOut && worse >= bound ? Verdict.Worse
			: betterOut && worseOut ? Verdict.NoDifference
			: Verdict.Undecided;
		return new(verdict, better, worse, bound, betterOut, worseOut);
	}

	/// <summary>The sample variance of pair scores — the one copy; the match's standard error reads it too.</summary>
	public static double Variance(IReadOnlyList<double> xs)
	{
		if (xs.Count < 2)
			return double.NaN;
		var mean = xs.Average();
		return xs.Sum(x => (x - mean) * (x - mean)) / (xs.Count - 1);
	}
}
