using System.Diagnostics;

namespace MagicCore.Ai;

/// <summary>
/// The progress line EVERY batch run prints (Shayne, 2026-11-09, a project-wide rule): games done of total, minutes
/// elapsed, and the running RESULT, so a long match can be read before it ends. To stderr, at most one line every
/// <see cref="EverySeconds"/> and always the last; thread-safe. A new batch run gets one of these or it does not ship.
/// </summary>
public sealed class RunProgress(string what, int total)
{
	public const double EverySeconds = 20;

	readonly Stopwatch clock = Stopwatch.StartNew();
	readonly object gate = new();
	int done;
	double lastPrinted = -EverySeconds;

	/// <summary>One more finished; <paramref name="result"/> says how the run stands so far (called under the lock).</summary>
	public void Tick(Func<string>? result = null)
	{
		lock (gate)
		{
			done++;
			var now = clock.Elapsed.TotalSeconds;
			if (done < total && now - lastPrinted < EverySeconds)
				return;
			lastPrinted = now;
			Console.Error.WriteLine(
				$"{what}: {done}/{total}, {now / 60:0.0} min{(result?.Invoke() is { Length: > 0 } r ? ", " + r : "")}"
			);
		}
	}
}
