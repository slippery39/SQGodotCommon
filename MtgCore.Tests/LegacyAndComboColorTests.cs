using MtgCore;
using NUnit.Framework;

namespace MtgCore.Tests;

/// <summary>
/// Colour assignment for the Legacy pool and the Combo Proving Ground.
///
/// These exist because the first pass at assigning them was WRONG and shipped silently: the
/// insertion script anchored on any quoted occurrence of a card's name, so a name mentioned in
/// another card's doc comment put the pip on whatever card was defined next. Llanowar Elves came
/// out needing UUBRG and Lotus Bloom — a colourless artifact — needed UU, and every test still
/// passed. Nothing but an explicit expected table catches that.
/// </summary>
[TestFixture]
public class LegacyAndComboColorTests
{
	private static Card Find(IReadOnlyList<Card> pool, string name)
	{
		var card = pool.FirstOrDefault(c => c.Name == name);
		Assert.That(card, Is.Not.Null, $"'{name}' is missing from the pool");
		return card!;
	}

	// ===== LEGACY =====

	[TestCase("Lightning Bolt", "R")]
	[TestCase("Llanowar Elves", "G")]
	[TestCase("Ancestral Recall", "U")]
	[TestCase("Reanimate", "B")]
	[TestCase("Path to Exile", "W")]
	[TestCase("Wrath of God", "WW")]
	[TestCase("Liliana of the Veil", "BB")]
	[TestCase("Goblin Chieftain", "RR")]
	[TestCase("Primeval Titan", "GG")]
	[TestCase("Mahamoti Djinn", "UU")]
	[TestCase("Lightning Helix", "WR")]
	[TestCase("Geist of Saint Traft", "WU")]
	[TestCase("Qasali Pridemage", "WG")]
	[TestCase("Siege Rhino", "WBG")]
	public void LegacyCard_HasExactlyThesePips(string name, string expected)
	{
		Assert.That(Find(CardLibrary.All, name).ColorPips.ToPipString(), Is.EqualTo(expected));
	}

	/// <summary>
	/// Artifacts and lands must stay colourless. A land is the source of colour, not a consumer of
	/// it — a pip here would make it uncastable by the very colour it exists to supply — and the
	/// artifacts are what let Affinity and Storm function across colours at all.
	/// </summary>
	[TestCase("Sol Ring")]
	[TestCase("Mox Pearl")]
	[TestCase("Lotus Bloom")]
	[TestCase("Cranial Plating")]
	[TestCase("Bonesplitter")]
	[TestCase("Frogmite")]
	[TestCase("Myr Enforcer")]
	[TestCase("Arcbound Ravager")]
	[TestCase("Thought Monitor")]
	[TestCase("Gitaxian Probe")]
	[TestCase("Gut Shot")]
	[TestCase("Seat of the Synod")]
	[TestCase("Glimmervoid")]
	[TestCase("Field of the Dead")]
	public void ColourlessLegacyCard_HasNoPips(string name)
	{
		Assert.That(Find(CardLibrary.All, name).ColorPips.IsEmpty, Is.True);
	}

	[Test]
	public void EveryLegacyNonArtifactNonLand_HasAPip()
	{
		var missing = CardLibrary
			.All.Where(c => !c.HasSubtype("Land") && !c.HasSubtype("Artifact"))
			.Where(c => c.ColorPips.IsEmpty)
			.Select(c => c.Name)
			.ToList();

		// Every one of these is colourless in paper too, so the list is a statement of intent
		// rather than a backlog:
		//   Gitaxian Probe / Gut Shot — Phyrexian mana. The engine models them at cost 0, which IS
		//     the pay-life-instead-of-colour half, so colourless is the faithful reading.
		//   Lotus Bloom, Throne of Bone, Iron Golem — colourless artifacts that simply carry no
		//     "Artifact" subtype in this engine, so the filter above cannot see them.
		Assert.That(
			missing,
			Is.EquivalentTo(
				new[]
				{
					"Gitaxian Probe",
					"Gut Shot",
					"Lotus Bloom",
					"Throne of Bone",
					"Iron Golem",
				}
			)
		);
	}

	// ===== LANDS PRODUCE COLOUR =====

	[TestCase("Seat of the Synod", "U")]
	[TestCase("Valakut, the Molten Pinnacle", "R")]
	[TestCase("Simic Growth Chamber", "UG")]
	[TestCase("Glimmervoid", "WUBRG")]
	public void SpecialLand_ProducesTheColoursItShould(string name, string produces)
	{
		var land = Find(CardLibrary.All, name);
		var component = land.GetComponent<LandColorComponent>();

		Assert.That(component, Is.Not.Null, $"{name} produces no colour");
		Assert.That(component!.Produces.ToPipString(), Is.EqualTo(produces));
	}

	// ===== COMBO PROVING =====

	[TestCase("Consign to Rot", "B")]
	[TestCase("Raise the Sunken", "B")]
	[TestCase("Aurex, the Sevenfold", "B")]
	[TestCase("Sunken Colossus", "U")]
	[TestCase("Hoofthunder Colossus", "G")]
	[TestCase("Ironscale Marshal", "G")]
	[TestCase("Ironscale Rite", "G")]
	[TestCase("Almsgiver Acolyte", "W")]
	[TestCase("Sanguine Reciprocity", "B")]
	[TestCase("Covenant of Thorns", "B")]
	public void ComboProvingCard_HasExactlyThesePips(string name, string expected)
	{
		Assert.That(Find(ComboProving.Cards, name).ColorPips.ToPipString(), Is.EqualTo(expected));
	}

	[Test]
	public void TheTwinPackage_IsBlueUntappersAndRedCopiers()
	{
		var illusionists = ComboProving
			.Cards.Where(c => c.HasSubtype(ComboProving.Illusionist))
			.ToList();

		Assert.That(illusionists, Is.Not.Empty, "the untapper half must exist");
		Assert.That(
			illusionists.All(c => c.ColorPips.ToPipString() == "U"),
			Is.True,
			"untappers are blue"
		);
	}

	/// <summary>
	/// The counters package is artifact creatures, and artifacts are colourless. This also keeps
	/// the artifact/affinity overlap the set documents intact.
	/// </summary>
	[TestCase("Sporeback Ballista")]
	[TestCase("Scrapyard Ravager")]
	[TestCase("Scrapyard Servitor")]
	public void TheArtifactCountersPieces_StayColourless(string name)
	{
		Assert.That(Find(ComboProving.Cards, name).ColorPips.IsEmpty, Is.True);
	}
}
