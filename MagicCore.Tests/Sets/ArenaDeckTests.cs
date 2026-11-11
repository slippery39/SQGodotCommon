using MagicCore.Sets;
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

	// Shayne's TRK sealed lists (2026-11-10): the parser knew FRA alone.
	[Test]
	public void AnotherSetsExport_ReadsWithThatSet_AndNotAsFra()
	{
		const string export = """
			Deck
			2 Targ Wrangler (TRK) 175
			8 Swamp (MIR) 340
			""";
		var trk = ArenaDeck.Parse(export, "TRK");
		Assert.That(trk.Ok, string.Join("; ", trk.Problems));
		Assert.That(trk.Deck, Has.Count.EqualTo(10));
		Assert.That(ArenaDeck.Parse(export).Ok, Is.False, "an FRA read refuses a TRK card");
	}

	// Arena spaces an ellipsis, Scryfall doesn't: Shayne's soup deck (2026-11-10) was refused for it.
	[Test]
	public void AnEllipsis_SpacedOrNot_IsTheSameName()
	{
		var result = ArenaDeck.Parse(
			"""
			Deck
			1 All Good Things . . . (TRK) 226
			""",
			"TRK"
		);
		Assert.That(result.Ok, string.Join("; ", result.Problems));
		Assert.That(result.Deck.Single().Data.Name, Is.EqualTo("All Good Things..."));
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
		// Whichever FRA card is not scripted yet — read, not restated, so scripting it breaks nothing.
		var unplayable = CardRegistry
			.Printings("FRA")
			.Select(p => p.Card.Name)
			.FirstOrDefault(n => !CardRegistry.IsImplemented(n));
		var result = ArenaDeck.Parse(
			$"4 Lightning Bolt (STA) 42\n1 Andoria (TRK) 275\n10 Swamp (FRA) 283{(unplayable is null ? "" : $"\n1 {unplayable}")}"
		);

		Assert.That(result.Ok, Is.False);
		Assert.That(result.Deck, Is.Empty);
		Assert.That(
			result.Problems,
			Has.Some.StartsWith("Lightning Bolt: not a Reality Fracture card")
		);
		// A TRK card plays, but a pasted pool or deck is still FRA's (MC-5.5): refused here, not a crash later.
		Assert.That(result.Problems, Has.Some.StartsWith("Andoria: not a Reality Fracture card"));
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
	public void APool_ReadsDeckAndSideboard_DropsBasics_AndLeavesOutWhatIsNotFra()
	{
		var result = ArenaDeck.ReadPool(
			"""
			Deck
			2 Last Gasp (FRA) 96
			8 Swamp (FRA) 283

			Sideboard
			1 Prophesied End (FRA) 12
			1 Lightning Bolt (STA) 42
			7 Island
			1 Blossom-Blessed Angel /// Seed Suture (FRA) 30
			"""
		);

		Assert.That(
			result.Pool,
			Is.EquivalentTo(
				new[]
				{
					"Last Gasp",
					"Last Gasp",
					"Prophesied End",
					"Blossom-Blessed Angel // Seed Suture",
				}
			)
		);
		Assert.That(result.LeftOut, Is.EqualTo(new[] { "Lightning Bolt" }));
	}

	[Test]
	public void NothingPasted_SaysWhatToPaste()
	{
		Assert.That(ArenaDeck.Parse("  \n").Problems, Has.Count.EqualTo(1));
	}
}
