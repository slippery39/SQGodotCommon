using MagicCore.Cards;

namespace MagicCore.Tests.Cards;

public class CardDataSetTests
{
	static readonly IReadOnlyList<CardData> Fra = CardDataSet.Load("FRA");

	[Test]
	public void Fra_LoadsWithUniqueNamesAndTypeLines()
	{
		Assert.That(Fra, Is.Not.Empty);
		Assert.That(Fra.Select(c => c.Name), Is.Unique);
		Assert.That(Fra.Where(c => string.IsNullOrWhiteSpace(c.TypeLine)), Is.Empty);
	}

	// Proves the snake_case mapping reached the fields the engine reads — a misnamed property
	// deserializes silently to null, and every card would look like a land with no cost.
	[Test]
	public void Fra_EveryCreatureFaceHasPowerAndToughness()
	{
		var faces = Fra.SelectMany(c =>
			c.CardFaces?.Select(f => (f.Name, f.TypeLine, f.Power, f.Toughness))
			?? [(c.Name, c.TypeLine, c.Power, c.Toughness)]
		);
		var creatures = faces.Where(f => f.TypeLine.Contains("Creature")).ToList();

		Assert.That(creatures, Is.Not.Empty);
		Assert.That(
			creatures.Where(f => f.Power is null || f.Toughness is null).Select(f => f.Name),
			Is.Empty
		);
	}

	[Test]
	public void Fra_PrepareCardsKeepBothFaces()
	{
		var prepare = Fra.Where(c => c.Layout == "prepare").ToList();

		Assert.That(prepare, Is.Not.Empty);
		Assert.That(
			prepare.Where(c => c.CardFaces is not { Length: 2 }).Select(c => c.Name),
			Is.Empty
		);
	}

	[Test]
	public void UnknownSet_ThrowsWithTheImportCommand()
	{
		Assert.That(
			() => CardDataSet.Load("XYZ"),
			Throws.ArgumentException.With.Message.Contains("import_scryfall_set.py")
		);
	}
}
