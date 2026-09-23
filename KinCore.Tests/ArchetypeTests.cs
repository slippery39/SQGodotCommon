using KinCore;

namespace KinCore.Tests;

/// <summary>
/// The companion-as-archetype slice, checked against the REAL content.
///
/// The mechanics are tested on inline cards elsewhere, so a balance pass never breaks them. These
/// test the things that can only go wrong in authored content — a keyword that renders blank, a
/// starter that is not what the companion declares, a card that is in the pool and never offered.
/// **Every expectation reads the authored value** rather than restating it.
/// </summary>
public class ArchetypeTests
{
	private static IEnumerable<RunCard> EveryCard() =>
		StarterContent
			.RewardPool(ThemeLibrary.All[0].Theme)
			.Concat(StarterContent.Roster.SelectMany(c => c.Starter));

	/// <summary>
	/// **Thorns, Strikes and Breakthrough are FIELDS, and a field has no text unless something
	/// states it** — the trap Devour fell into first. A Briar Sentinel whose face said nothing
	/// would be a 2/12 as far as the player could tell.
	/// </summary>
	[Test]
	public void EveryKeywordACardCarriesIsStatedInItsRulesText()
	{
		foreach (var card in EveryCard())
		{
			var text = string.Join(" | ", KinRulesText.Lines(card));

			Assert.Multiple(() =>
			{
				if (card.Thorns > 0)
					Assert.That(text, Does.Contain($"Thorns {card.Thorns}"), card.Name);
				if (card.Strikes > 1)
					Assert.That(text, Does.Contain("Strikes"), card.Name);
				if (card.Breakthrough)
					Assert.That(text, Does.Contain("Breakthrough"), card.Name);
			});
		}
	}

	/// <summary>
	/// **An enemy's behaviour has to be visible, or countering it is luck.** Enemies had no text
	/// surface at all before this slice.
	/// </summary>
	[Test]
	public void EveryKeywordAnEnemyCarriesIsStatedInItsRulesText()
	{
		foreach (var enemy in EnemyLibrary.All)
		{
			var text = string.Join(" | ", KinRulesText.Lines(enemy));

			Assert.Multiple(() =>
			{
				if (enemy.Thorns > 0)
					Assert.That(text, Does.Contain($"Thorns {enemy.Thorns}"), enemy.Name);
				if (enemy.Strikes > 1)
					Assert.That(text, Does.Contain("Strikes"), enemy.Name);
				if (enemy.Flies)
					Assert.That(text, Does.Contain("Flier"), enemy.Name);
			});
		}
	}

	[Test]
	public void AnArchetypeCompanionStartsWithItsOwnCardsAndTheSameDeckSize()
	{
		var baseline = StarterContent.NewRun(seed: 1, StarterContent.StarterCompanion).Deck.Count;

		foreach (var companion in StarterContent.Roster)
		{
			var deck = StarterContent.NewRun(seed: 1, companion).Deck.Select(c => c.Name).ToList();

			// Every companion starts on the same number of cards, so a starter is a CHOICE of
			// cards rather than a bigger or smaller deck.
			Assert.That(deck, Has.Count.EqualTo(baseline), companion.Name);

			foreach (var card in companion.Starter)
				Assert.That(deck, Does.Contain(card.Name), $"{companion.Name} lost {card.Name}");
		}
	}

	[Test]
	public void BothSliceCompanionsDeclareAnArchetype()
	{
		// The slice is Bulwark and Face. The other three get starters when their archetypes land;
		// until then they deliberately start on the generic three.
		Assert.Multiple(() =>
		{
			Assert.That(StarterContent.Bramble.Starter, Is.Not.Empty, "Bramble is Bulwark");
			Assert.That(StarterContent.Pike.Starter, Is.Not.Empty, "Pike is Face");
		});
	}

	/// <summary>
	/// **In the pool is not the same as offered** — the offer is a weighted draw, and a card whose
	/// weight or tagging went wrong would sit in the pool and never reach a reward screen. Across
	/// a few whole runs of floors, every archetype card must turn up at least once.
	/// </summary>
	[Test]
	public void EveryArchetypeCardIsActuallyOffered()
	{
		var theme = ThemeLibrary.All[0].Theme;
		var offered = Enumerable
			.Range(1, 30)
			.SelectMany(seed =>
				Enumerable
					.Range(1, Run.RunLength)
					.SelectMany(floor => StarterContent.RewardsFor(theme, seed, floor))
			)
			.Select(c => c.Name)
			.ToHashSet();

		foreach (var card in StarterContent.BulwarkCards.Concat(StarterContent.FaceCards))
			Assert.That(offered, Does.Contain(card.Name), $"{card.Name} is never offered");
	}
}
