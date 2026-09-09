using NUnit.Framework;

namespace MtgSimulator.Tests;

/// <summary>
/// Where a test run reads and writes <c>sim_results/</c>.
///
/// **`sim_results/` is resolved against the WORKING DIRECTORY, and under `dotnet test` that starts
/// as the test binary's folder rather than the repository.** Six fixtures already correct for this
/// by walking up to the solution root; this is the shared version so a seventh does not need its
/// own copy.
///
/// Anchor on the SOLUTION FILE, never on the first <c>sim_results/</c> found walking up — stray
/// ones exist wherever a process has been run from, and `MtgSimulator.Tests/bin/Debug/net10.0/` is
/// the first hit. A table loaded from there is empty, every card reads 0.00pp, and the failure
/// looks like missing DATA rather than a wrong PATH.
///
/// **The working directory is process-wide, so this is order-dependent by nature.** Any fixture
/// that calls it moves every later fixture in the same run. That is exactly how a freshly generated
/// card-value table came to be invisible: `CardValueSweep` wrote it relative to the bin folder, a
/// chdir-ing fixture then ran, and three unrelated tests failed as though the data were missing —
/// while passing in isolation, because alone nothing had moved the directory. Anything that WRITES
/// to sim_results must call this, or where its output lands depends on what ran before it.
/// </summary>
public static class TestPaths
{
	public static void ChdirToSolutionRoot()
	{
		var dir = new DirectoryInfo(AppContext.BaseDirectory);
		while (dir is not null && dir.GetFiles("*.sln").Length == 0)
			dir = dir.Parent;

		Assert.That(dir, Is.Not.Null, "could not find the solution root");
		Directory.SetCurrentDirectory(dir!.FullName);
	}
}
