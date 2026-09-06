using System.Collections.Immutable;
using NUnit.Framework;

namespace MtgSimulator.Tests;

/// <summary>
/// **An archetype is named for the card that cheats, not the one that sorts first.**
///
/// Cards asking the same demands produce the byte-identical core and dedupe into one entry, so one
/// of them has to name it. On ALL, a 24-card reanimation group was named alphabetically — the
/// report said "Angel of Second Rites" and `Reanimate` never appeared at all. Real decks are named
/// for the card that puts the big thing into play.
/// </summary>
[TestFixture]
public class EngineNamingTests
{
	private static DeckCore Core(string name) =>
		new(name, [new CoreSlot("Payoff", [name], 4, IsIdentity: true)]);

	[Test]
	public void TheHighestGapCardNamesTheArchetype()
	{
		// Deliberately the alphabetically LAST card, so sorting cannot produce this answer by
		// accident — the old rule would have returned "Angel of Second Rites".
		var group = new[] { Core("Angel of Second Rites"), Core("Gravedigger"), Core("Reanimate") };
		var gap = new Dictionary<string, int>(StringComparer.Ordinal) { ["Reanimate"] = 7 };

		Assert.That(EngineDiscovery.Representative(group, gap).Name, Is.EqualTo("Reanimate"));
	}

	/// <summary>
	/// **The tiebreak still does all the work for archetypes with no cheat in them.** Every card in
	/// a tribal group has gap 0, so a lord group must be ordered exactly as it was before — this is
	/// the guard that the change did not quietly reorder every archetype in the report.
	/// </summary>
	[Test]
	public void WithNoCheatInTheGroup_TheOrderIsUnchanged()
	{
		var group = new[] { Core("Krenko, Mob Boss"), Core("Arms Dealer"), Core("Goblin Chieftain") };

		Assert.That(
			EngineDiscovery.Representative(group, new Dictionary<string, int>()).Name,
			Is.EqualTo("Arms Dealer"),
			"with every gap 0 this must fall back to alphabetical, as it always did"
		);
	}

	/// <summary>
	/// Two cheats in one group — the BIGGER gap wins, and alphabetical order breaks a true tie so
	/// the report is reproducible. `Raise the Sunken` and `Reanimate` both read gap 7 on ALL.
	/// </summary>
	[Test]
	public void TheBiggerCheatWins_AndATrueTieIsBrokenAlphabetically()
	{
		var group = new[] { Core("Reanimate"), Core("Raise the Sunken"), Core("Endless Obedience") };

		var bigger = new Dictionary<string, int>(StringComparer.Ordinal)
		{
			["Reanimate"] = 7,
			["Raise the Sunken"] = 5,
			["Endless Obedience"] = 2,
		};
		Assert.That(EngineDiscovery.Representative(group, bigger).Name, Is.EqualTo("Reanimate"));

		var tied = new Dictionary<string, int>(StringComparer.Ordinal)
		{
			["Reanimate"] = 7,
			["Raise the Sunken"] = 7,
			["Endless Obedience"] = 2,
		};
		Assert.That(
			EngineDiscovery.Representative(group, tied).Name,
			Is.EqualTo("Raise the Sunken"),
			"a tie must resolve the same way on every run, or the report is not reproducible"
		);
	}
}
