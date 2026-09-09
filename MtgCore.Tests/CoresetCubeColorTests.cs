using MtgCore;
using NUnit.Framework;

namespace MtgCore.Tests;

/// <summary>
/// Colour assignment for the Core Set Cube.
///
/// The bulk of it is stamped at the section boundary by <c>CardColorExtensions.InColor</c>, and
/// the exceptions are inserted into each card's own builder chain. The failure mode worth testing
/// is not "did a pip get added" but "did it land on the RIGHT card" — an off-by-one during the
/// insertion pass would silently give Baneslayer Angel's second White pip to the card below it,
/// and nothing else in the suite would notice.
/// </summary>
[TestFixture]
public class CoresetCubeColorTests
{
	private static Card Find(string name)
	{
		var card = CoresetCube.Cards.FirstOrDefault(c => c.Name == name);
		Assert.That(card, Is.Not.Null, $"'{name}' is not in the cube");
		return card!;
	}

	[Test]
	public void EveryColouredCard_HasAtLeastOnePip()
	{
		var colourless = CoresetCubeColourlessCreatures
			.Cards.Concat(CoresetCubeColourlessArtifacts.Cards)
			.Concat(CoresetCubeColourlessEquipment.Cards)
			.Select(c => c.Name)
			.ToHashSet();

		var missing = CoresetCube
			.Cards.Where(c => !colourless.Contains(c.Name) && !c.HasSubtype("Land"))
			.Where(c => c.ColorPips.IsEmpty)
			.Select(c => c.Name)
			.ToList();

		Assert.That(missing, Is.Empty, "coloured cards with no pips");
	}

	[Test]
	public void ColourlessCards_HaveNoPips()
	{
		var withPips = CoresetCubeColourlessCreatures
			.Cards.Concat(CoresetCubeColourlessArtifacts.Cards)
			.Concat(CoresetCubeColourlessEquipment.Cards)
			.Where(c => !c.ColorPips.IsEmpty)
			.Select(c => c.Name)
			.ToList();

		Assert.That(withPips, Is.Empty, "colourless cards must stay castable in any deck");
	}

	[TestCase("Baneslayer Angel", ManaColor.White, 2)]
	[TestCase("Day of Judgment", ManaColor.White, 2)]
	[TestCase("Sun Titan", ManaColor.White, 2)]
	[TestCase("Frost Titan", ManaColor.Blue, 2)]
	[TestCase("Time Warp", ManaColor.Blue, 2)]
	[TestCase("Cavalier of Gales", ManaColor.Blue, 3)]
	[TestCase("Grave Titan", ManaColor.Black, 2)]
	[TestCase("Vampire Nighthawk", ManaColor.Black, 2)]
	[TestCase("Massacre Wurm", ManaColor.Black, 3)]
	[TestCase("Sorin Markov", ManaColor.Black, 3)]
	[TestCase("Inferno Titan", ManaColor.Red, 2)]
	[TestCase("Goblin Chieftain", ManaColor.Red, 2)]
	[TestCase("Drakuseth, Maw of Flames", ManaColor.Red, 3)]
	[TestCase("Primeval Titan", ManaColor.Green, 2)]
	[TestCase("Elder Gargaroth", ManaColor.Green, 2)]
	[TestCase("Overrun", ManaColor.Green, 3)]
	[TestCase("Hornet Queen", ManaColor.Green, 3)]
	public void CommittedCards_CarryTheirDoublePip(string name, ManaColor color, int count)
	{
		var card = Find(name);

		Assert.Multiple(() =>
		{
			Assert.That(card.ColorPips[color], Is.EqualTo(count), $"{name} {color} pips");
			Assert.That(card.ColorPips.Total, Is.EqualTo(count), $"{name} has no other colour");
		});
	}

	[TestCase("Lightning Bolt", ManaColor.Red)]
	[TestCase("Llanowar Elves", ManaColor.Green)]
	[TestCase("Doom Blade", ManaColor.Black)]
	[TestCase("Opt", ManaColor.Blue)]
	[TestCase("Pacifism", ManaColor.White)]
	public void OrdinaryCards_TakeTheSectionDefault_OfExactlyOnePip(string name, ManaColor color)
	{
		var card = Find(name);

		Assert.Multiple(() =>
		{
			Assert.That(card.ColorPips[color], Is.EqualTo(1));
			Assert.That(card.ColorPips.Total, Is.EqualTo(1));
		});
	}

	[TestCase("Corpse Knight", ManaColor.White, ManaColor.Black)]
	[TestCase("Risen Reef", ManaColor.Blue, ManaColor.Green)]
	[TestCase("Enigma Drake", ManaColor.Blue, ManaColor.Red)]
	[TestCase("Conclave Mentor", ManaColor.White, ManaColor.Green)]
	[TestCase("Garruk, Apex Predator", ManaColor.Black, ManaColor.Green)]
	[TestCase("Draconic Disciple", ManaColor.Red, ManaColor.Green)]
	public void GoldCards_RequireBothColours(string name, ManaColor first, ManaColor second)
	{
		var card = Find(name);

		Assert.Multiple(() =>
		{
			Assert.That(card.ColorPips[first], Is.EqualTo(1), $"{name} needs {first}");
			Assert.That(card.ColorPips[second], Is.EqualTo(1), $"{name} needs {second}");
			Assert.That(card.ColorPips.Total, Is.EqualTo(2), $"{name} is exactly two colours");
		});
	}
}
