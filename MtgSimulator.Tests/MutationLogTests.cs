using System.Collections.Immutable;

namespace MtgSimulator.Tests;

/// <summary>
/// **What the search TRIED, which no report used to say.**
///
/// A final decklist shows what survived, and "this card is not in the deck" has two causes with
/// opposite fixes — never proposed, or proposed and then cut. The log is what tells them apart.
/// </summary>
[TestFixture]
public class MutationLogTests
{
	private static Decklist Deck(int lands, params (string Card, int Copies)[] spells) =>
		new(
			"d",
			spells.ToImmutableSortedDictionary(s => s.Card, s => s.Copies, StringComparer.Ordinal),
			lands
		);

	private static MutationRow Row(string added, double delta, string outcome, int gen = 1) =>
		new(gen, 0, "d", added, "", 0.50, 0.50 + delta, 10, outcome);

	/// <summary>
	/// **A recount is the most common mutation and would otherwise log as an empty diff.** `Mutate`
	/// changes 3x to 4x far more often than it swaps a card in, so rendering only card-set changes
	/// would make the operator the search leans on hardest invisible.
	/// </summary>
	[Test]
	public void ARecountIsReportedAsAChange_NotAsAnEmptyDiff()
	{
		var (added, removed) = MutationLog.Diff(Deck(20, ("Bolt", 3)), Deck(20, ("Bolt", 4)));

		Assert.Multiple(() =>
		{
			Assert.That(added, Is.EqualTo("1x Bolt"));
			Assert.That(removed, Is.Empty);
		});
	}

	[Test]
	public void ASwapNamesBothSides_AndLandsAreCountedToo()
	{
		var (added, removed) = MutationLog.Diff(
			Deck(20, ("Bolt", 4), ("Bear", 2)),
			Deck(18, ("Bolt", 4), ("Ritual", 2))
		);

		Assert.Multiple(() =>
		{
			Assert.That(added, Is.EqualTo("2x Ritual"));
			Assert.That(removed, Is.EqualTo("2x Bear + 2x Land"));
		});
	}

	/// <summary>
	/// **The selection artifact this aggregation exists to avoid.** Acceptance is conditioned on
	/// beating the parent, so averaging only accepted rows reports every card as positive whatever
	/// the truth is — the same shape as reading a card's value out of the decks that played it.
	/// A card proposed three times and kept once is a card the search likes and the field does not.
	/// </summary>
	[Test]
	public void MeanDeltaIsOverProposals_NotOverAcceptedOnes()
	{
		var log = new MutationLog();
		log.Add(Row("1x Conduit", +0.10, MutationLog.Accepted, gen: 2));
		log.Add(Row("1x Conduit", -0.04, MutationLog.Rejected, gen: 5));
		log.Add(Row("1x Conduit", -0.06, MutationLog.Rejected, gen: 9));

		var card = log.ByCard().Single(c => c.Card == "Conduit");

		Assert.Multiple(() =>
		{
			Assert.That(card.Proposed, Is.EqualTo(3));
			Assert.That(card.Accepted, Is.EqualTo(1));
			Assert.That(card.MeanDelta, Is.EqualTo(0.0).Within(1e-9));
			Assert.That(card.FirstGen, Is.EqualTo(2));
			Assert.That(card.LastGen, Is.EqualTo(9));
		});
	}

	/// <summary>
	/// The synthetic "Land" entry is a rendering of the `Lands` count, not a card, and must not
	/// appear in a per-card table as the most-proposed "card" in every run.
	/// </summary>
	[Test]
	public void TheLandCountIsNotReportedAsACard()
	{
		var log = new MutationLog();
		log.Add(Row("1x Bolt + 2x Land", +0.01, MutationLog.Accepted));

		Assert.That(log.ByCard().Select(c => c.Card), Is.EquivalentTo(new[] { "Bolt" }));
	}

	/// Card names contain commas, which is the whole reason the CSV escapes anything.
	[Test]
	public void ACardNameWithACommaSurvivesTheCsv()
	{
		var log = new MutationLog();
		log.Add(Row("1x Krenko, Mob Boss", +0.02, MutationLog.Accepted));

		var line = log.ToCsv().Split('\n')[1];

		Assert.That(line, Does.Contain("\"1x Krenko, Mob Boss\""));
	}
}
