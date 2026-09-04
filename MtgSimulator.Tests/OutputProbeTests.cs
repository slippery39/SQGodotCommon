using System.Collections.Immutable;
using MtgCore;

namespace MtgSimulator.Tests;

/// <summary>
/// **The same acceptance gate as `ContextValueTests`, against a different substrate.**
///
/// The evaluator-based version cannot pass it, and that was measured rather than assumed:
/// `StateEvaluator` excludes temporary mana and until-end-of-turn power, so the two elves this
/// ranks on score identically to doing nothing, and a longer rollout collapsed every card to +0.0
/// because the fixture resolved itself.
///
/// `OutputProbe` swaps the substrate for accumulated OUTPUT against an opponent that cannot die.
/// Same known answer: on DES the hand-built elf deck holding Wirewood Conduit and Timberwatch Elder
/// beat the builder's own elf deck 65-35, from the same pool at the same land count.
/// </summary>
[TestFixture]
public class OutputProbeTests
{
	private const string Conduit = "Wirewood Conduit";
	private const string Timberwatch = "Timberwatch Elder";
	private const string Sage = "Reclamation Sage";
	private const string Archer = "Poison-Tip Archer";

	private const int Copies = 4;

	/// <summary>
	/// **Land count is read from `Decklist.MinLands`, never hardcoded.** `MTG_MIN_LANDS` overrides
	/// it, so a fixture pinned at 17 passes under the evolution runs' 12 and fails a plain
	/// `dotnet test` at the 20 default — an environment-dependent test, which is worse than none.
	/// This project has lost measurements to that variable more than once.
	/// </summary>
	private static int Lands => Math.Max(Decklist.MinLands, 17);

	/// <summary>
	/// **Wide enough that the shell is still resizable once a candidate is removed from it.**
	///
	/// `Compare` takes the candidate out and regrows the rest to leave exactly room for it, so the
	/// remaining cards must be able to hold `DeckSize - lands - copies` between them at 4 copies
	/// each. Ten names was not: at `MTG_MIN_LANDS=12` the shell is 17 lands, so a candidate already
	/// in it leaves 9 cards asked to supply 39 spells against a 36 cap — and the grow loop had no
	/// progress check, so the process HUNG rather than failing. Twelve names caps at 44 and clears
	/// the widest legal shell (`MinLands` 12 → 44 spells needed).
	///
	/// The floor is the binding case, not the default: the same shell needs only 36 from 9 cards at
	/// the 20 default and terminated fine, which is exactly why a plain `dotnet test` never saw it.
	/// </summary>
	private static readonly string[] ShellCards =
	[
		"Llanowar Elves",
		"Elvish Mystic",
		"Wirewood Herald",
		"Wirewood Symbiont",
		"Dwynen's Elite",
		"Elvish Visionary",
		"Elvish Archdruid",
		"Nissa, Vastwood Seer",
		"Sylvan Ranger",
		"Radha, Heart of Keld",
		"Dwynen, Gilt-Leaf Daen",
		"Fauna Shaman",
	];

	/// The shell sized so that shell + `Copies` of one candidate is exactly a legal 60.
	private static Dictionary<string, int> Shell()
	{
		var need = Decklist.DeckSize - Lands - Copies;
		var shell = new Dictionary<string, int>(StringComparer.Ordinal);
		for (var i = 0; shell.Values.Sum() < need; i++)
		{
			var name = ShellCards[i % ShellCards.Length];
			if (shell.GetValueOrDefault(name) >= Decklist.MaxCopies)
				continue;
			shell[name] = shell.GetValueOrDefault(name) + 1;
		}
		return shell;
	}

	private static IReadOnlyList<CardOutput> Measure(int turns = OutputProbe.DefaultTurns)
	{
		var pool = SetRegistry.Designed.Cards.ToDictionary(
			c => c.Name,
			c => c,
			StringComparer.OrdinalIgnoreCase
		);
		return OutputProbe.Compare(
			Shell(),
			Lands,
			[Conduit, Timberwatch, Sage, Archer],
			pool,
			turns: turns
		);
	}

	/// <summary>
	/// **The opponent must survive, or the whole construction is pointless.** If life stops at zero
	/// the measurement saturates and every reasonable deck reads the same, which is precisely how
	/// the evaluator-based version collapsed. Asserted before the ranking, because a passing rank
	/// order on a saturated measure would be luck.
	/// </summary>
	[Test]
	public void TheOpponentTakesDamagePastZeroAndTheGameKeepsGoing()
	{
		var pool = SetRegistry.Designed.Cards.ToDictionary(
			c => c.Name,
			c => c,
			StringComparer.OrdinalIgnoreCase
		);
		var spells = Shell();
		spells[Conduit] = Copies;

		var deck = new Decklist(
			"elves",
			spells.ToImmutableSortedDictionary(StringComparer.Ordinal),
			Lands
		);
		var output = OutputProbe.Play(deck, pool, seed: 1, turns: 12);

		TestContext.Out.WriteLine(
			$"  damage {output.Damage}, permanents {output.Permanents}, "
				+ $"drawn {output.CardsDrawn}, turns {output.Turns}"
		);

		Assert.Multiple(() =>
		{
			Assert.That(
				output.Turns,
				Is.GreaterThan(8),
				"the game ended early — something killed it"
			);
			Assert.That(
				output.Damage,
				Is.GreaterThan(20),
				"damage stopped at the starting life total, so the opponent is still dying"
			);
		});
	}

	/// <summary>
	/// **A candidate that is ALREADY in the shell must still be measurable.**
	///
	/// This is the case that made the feature inert for a whole 10-deck run. A core's candidate list
	/// is its own card pool, and the deck built from that core is made of the same cards — so the
	/// overlap is the common case, not the edge case. Adding `copies` on top of what the shell held
	/// produced 8 copies of a 4-of, every candidate threw identically, and every engine slot silently
	/// fell back to isolation value. The run completed and reported a plausible number.
	///
	/// A uniform failure is the worst shape a bug can take: nothing looks wrong, and the measurement
	/// simply is not there.
	/// </summary>
	[Test]
	public void ACandidateAlreadyInTheShellIsStillMeasured()
	{
		var pool = SetRegistry.Designed.Cards.ToDictionary(
			c => c.Name,
			c => c,
			StringComparer.OrdinalIgnoreCase
		);

		// Every candidate here is part of the shell, which is what a real core slot looks like.
		var measured = OutputProbe.Compare(
			Shell(),
			Lands,
			["Llanowar Elves", "Elvish Archdruid", "Nissa, Vastwood Seer"],
			pool,
			seeds: 1,
			turns: 6
		);

		Assert.That(measured, Has.Count.EqualTo(3));
		Assert.That(
			measured.Select(m => m.Raw),
			Is.Not.All.EqualTo(0.0),
			"every candidate measured zero — the shell overlap is silently failing again"
		);
	}

	/// <summary>
	/// The gate. Asserted as an ORDERING rather than a threshold — the scale is arbitrary and a
	/// threshold would need retuning whenever the shell or the turn count moves.
	/// </summary>
	[Test]
	public void InAnElfShell_TheEngineElvesOutrankTheGenericBodies()
	{
		var byName = Measure().ToDictionary(r => r.Name, r => r, StringComparer.Ordinal);

		foreach (var r in byName.Values.OrderByDescending(r => r.OverAverage))
			TestContext.Out.WriteLine(
				$"  {r.Name, -24}{r.Raw, 9:F1} raw {r.OverAverage, 9:F1} over avg"
			);

		Assert.Multiple(() =>
		{
			Assert.That(
				byName[Conduit].OverAverage,
				Is.GreaterThan(byName[Sage].OverAverage),
				$"{Conduit} still ranks below {Sage} in a deck full of Elves"
			);
			Assert.That(
				byName[Timberwatch].OverAverage,
				Is.GreaterThan(byName[Archer].OverAverage),
				$"{Timberwatch} still ranks below {Archer} in a deck full of Elves"
			);
		});
	}

	/// <summary>
	/// How the ranking moves with the horizon. Recorded rather than asserted: where the turn count
	/// sits decides whether ramp or immediate damage wins, and that is a judgement, not a fact.
	/// </summary>
	[Test]
	[Explicit("Diagnostic — ranking against turn count.")]
	public void HowDoesTheRankingMoveWithTheHorizon()
	{
		foreach (var turns in new[] { 5, 8, 12, 16 })
			TestContext.Out.WriteLine(
				$"  turn {turns, 2}: "
					+ string.Join(
						"  ",
						Measure(turns)
							.OrderByDescending(r => r.OverAverage)
							.Select(r => $"{r.Name.Split(' ')[^1]} {r.OverAverage:+0.0;-0.0}")
					)
			);
	}
}
