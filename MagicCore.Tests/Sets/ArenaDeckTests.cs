using MagicCore.Sets.Fra;

namespace MagicCore.Tests.Sets;

public class ArenaDeckTests
{
	[Test]
	public void AnArenaExport_ReadsTheMainDeck_NotTheSideboard()
	{
		var result = ArenaDeck.Parse(
			"""
			Deck
			4 Last Gasp (FRA) 96
			17 Swamp (FRA) 283
			1 Plains (FDN) 272

			Sideboard
			2 Prophesied End (FRA) 12
			"""
		);

		Assert.That(result.Ok, string.Join("; ", result.Problems));
		Assert.That(result.Deck, Has.Count.EqualTo(22));
		Assert.That(result.Deck.Count(c => c.Data.Name == "Last Gasp"), Is.EqualTo(4));
		Assert.That(result.Deck.Any(c => c.Data.Name == "Prophesied End"), Is.False);
	}

	[Test]
	public void APrepareCard_ByItsFrontFace_AndBareLinesWithoutASetCode()
	{
		var result = ArenaDeck.Parse("2 Blossom-Blessed Angel\n3 forest");

		Assert.That(result.Ok, string.Join("; ", result.Problems));
		Assert.That(
			result.Deck.Count(c => c.Data.Name == "Blossom-Blessed Angel // Seed Suture"),
			Is.EqualTo(2)
		);
		Assert.That(result.Deck.Count(c => c.Data.Name == "Forest"), Is.EqualTo(3));
	}

	[Test]
	public void UnknownAndUnplayableCards_AreNamed_AndNoDeckIsReturned()
	{
		// Whichever card is not scripted yet — read, not restated, so scripting it breaks nothing.
		var unplayable = FraCards.AllNames.FirstOrDefault(n => !FraCards.IsImplemented(n));
		var result = ArenaDeck.Parse(
			$"4 Lightning Bolt (STA) 42\n10 Swamp (FRA) 283{(unplayable is null ? "" : $"\n1 {unplayable}")}"
		);

		Assert.That(result.Ok, Is.False);
		Assert.That(result.Deck, Is.Empty);
		Assert.That(
			result.Problems,
			Has.Some.StartsWith("Lightning Bolt: not a Reality Fracture card")
		);
		if (unplayable is not null)
			Assert.That(result.Problems, Has.Some.StartsWith($"{unplayable}: not playable yet"));
	}

	[Test]
	public void ACompanionSectionFirst_TheDeckIsStillRead()
	{
		var result = ArenaDeck.Parse("Companion\n1 Last Gasp (FRA) 96\n\nDeck\n17 Swamp (FRA) 283");

		Assert.That(result.Deck.Select(c => c.Data.Name).Distinct(), Is.EqualTo(new[] { "Swamp" }));
	}

	[Test]
	public void AnAbsurdCount_IsAProblemNotACrash()
	{
		Assert.That(ArenaDeck.Parse("99999999999 Swamp").Ok, Is.False);
	}

	[Test]
	public void NothingPasted_SaysWhatToPaste()
	{
		Assert.That(ArenaDeck.Parse("  \n").Problems, Has.Count.EqualTo(1));
	}
}
