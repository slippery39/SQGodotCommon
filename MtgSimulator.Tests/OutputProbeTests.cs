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

	/// 39 spells + 17 lands; each candidate adds 4 to make exactly 60.
	private static readonly Dictionary<string, int> Shell =
		new(StringComparer.Ordinal)
		{
			["Llanowar Elves"] = 4,
			["Elvish Mystic"] = 4,
			["Wirewood Herald"] = 4,
			["Wirewood Symbiont"] = 4,
			["Dwynen's Elite"] = 4,
			["Elvish Visionary"] = 4,
			["Elvish Archdruid"] = 4,
			["Nissa, Vastwood Seer"] = 4,
			["Sylvan Ranger"] = 4,
			["Radha, Heart of Keld"] = 3,
		};

	private static IReadOnlyList<CardOutput> Measure(int turns = OutputProbe.DefaultTurns)
	{
		var pool = SetRegistry.Designed.Cards.ToDictionary(
			c => c.Name,
			c => c,
			StringComparer.OrdinalIgnoreCase
		);
		return OutputProbe.Compare(
			Shell,
			lands: 17,
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
		var spells = Shell.ToDictionary(kv => kv.Key, kv => kv.Value, StringComparer.Ordinal);
		spells[Conduit] = 4;

		var deck = new Decklist(
			"elves",
			spells.ToImmutableSortedDictionary(StringComparer.Ordinal),
			17
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
