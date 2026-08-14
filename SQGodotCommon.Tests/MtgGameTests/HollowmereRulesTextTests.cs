using System.Linq;
using MtgCore;
using MtgGame;
using NUnit.Framework;

namespace SQGodotCommon.Tests;

/// <summary>
/// The draft UI is for READING cards — a pack is studied, not glanced at. A card whose
/// mechanic the mapper does not know renders with that mechanic silently missing, which is
/// invisible in a screenshot but makes the card undraftable.
///
/// These tests pin the text for one representative card per mechanic added with Hollowmere,
/// plus a blanket check that no card in the set renders blank.
/// </summary>
[TestFixture]
public class HollowmereRulesTextTests
{
	private static string TextFor(string cardName) =>
		MtgCardMapper.GetRulesText(Hollowmere.Cards.First(c => c.Name == cardName));

	[Test]
	public void NoCardInTheSet_RendersBlank()
	{
		var blank = Hollowmere
			.Cards.Where(c => string.IsNullOrWhiteSpace(MtgCardMapper.GetRulesText(c)))
			.Select(c => c.Name)
			.ToList();

		Assert.That(blank, Is.Empty, $"Blank card faces: {string.Join(", ", blank)}");
	}

	/// <summary>
	/// Flashback changes how a card is drafted more than almost any other property in this
	/// set, so a missing cost is a real gameplay problem rather than a cosmetic one.
	/// </summary>
	[Test]
	public void FlashbackCost_IsShown()
	{
		Assert.That(TextFor("Ghoulcaller's Bargain"), Does.Contain("Flashback 6"));
	}

	[Test]
	public void Threshold_IsShown()
	{
		var text = TextFor("Nightfall Reveler");
		Assert.That(text, Does.Contain("Threshold"));
		Assert.That(text, Does.Contain("7+"));
		Assert.That(text, Does.Contain("Flying"));
	}

	/// <summary>
	/// Wonder does nothing while it is on the battlefield, so omitting the zone would make
	/// the card read as an ordinary anthem.
	/// </summary>
	[Test]
	public void GraveyardActiveStatic_SaysSoExplicitly()
	{
		var text = TextFor("Wonder of the Drowned");
		Assert.That(text, Does.Contain("While this is in your graveyard"));
		Assert.That(text, Does.Contain("Flying"));
	}

	[Test]
	public void Werewolf_ShowsItsNightFaceAndFlipCondition()
	{
		var text = TextFor("Village Messenger");
		Assert.That(text, Does.Contain("Moonrise Stalker"));
		Assert.That(text, Does.Contain("no spells were cast last turn"));
	}

	[Test]
	public void Madness_ReadsAsMadnessRatherThanAGenericTrigger()
	{
		Assert.That(TextFor("Fiery Temper"), Does.Contain("Madness"));
	}

	[Test]
	public void Mill_NamesWhoIsMilled()
	{
		Assert.That(TextFor("Thought Scour"), Does.Contain("mills 2"));
		Assert.That(TextFor("Grim Excavation"), Does.Contain("You mill 4"));
	}

	[Test]
	public void Deathtouch_IsShown()
	{
		Assert.That(TextFor("Nighthawk Penitent"), Does.Contain("Deathtouch"));
	}

	[Test]
	public void DynamicPowerToughness_IsExplained()
	{
		Assert.That(
			TextFor("Splinterbone Horror"),
			Does.Contain("number of cards in your graveyard")
		);
	}

	/// <summary>
	/// Creature recursion and spell flashback share a component but behave differently — the
	/// creature stays on the battlefield rather than being exiled — so the text differs too.
	/// </summary>
	[Test]
	public void CreatureRecursion_ReadsDifferentlyFromSpellFlashback()
	{
		var text = TextFor("Gravecrawler");
		Assert.That(text, Does.Contain("Cast from graveyard"));
		Assert.That(text, Does.Not.Contain("Flashback"));
	}

	[Test]
	public void Landfall_IsNamed()
	{
		Assert.That(TextFor("Cryptwalk Scarab"), Does.Contain("Landfall"));
	}

	[Test]
	public void DrainEffects_AreDescribed()
	{
		Assert.That(TextFor("Blood Artist"), Does.Contain("loses 1 life"));
	}

	/// <summary>
	/// Phrases that mean the mapper fell back rather than describing the card. Each was a real
	/// bug: a generic " to target" suffix spliced into sentences that could not take it
	/// ("Put to target onto the battlefield"), filters reported as "matching creatures", and
	/// self-referential effects reading "it" instead of "this".
	/// </summary>
	/// Note the specific shapes: "to target" is CORRECT in "Deal 3 damage to target creature",
	/// so the assertion has to name the broken verb-plus-suffix forms rather than the substring.
	[TestCase("matching")]
	[TestCase("Destroy to target")]
	[TestCase("Put to target")]
	[TestCase("Exile to target")]
	[TestCase("Return to target")]
	[TestCase("target any target")]
	[TestCase("When triggered")]
	[TestCase("Grants nothing")]
	public void NoCard_UsesFallbackWording(string phrase)
	{
		var offenders = Hollowmere
			.Cards.Where(c =>
				MtgCardMapper.GetRulesText(c).Contains(phrase, StringComparison.OrdinalIgnoreCase)
			)
			.Select(c => c.Name)
			.ToList();

		Assert.That(offenders, Is.Empty, $"'{phrase}' in: {string.Join(", ", offenders)}");
	}

	/// <summary>
	/// A lord whose filter is not described is unreadable — "creatures get +1/+1" hides which
	/// creatures, and that is the entire card.
	/// </summary>
	[Test]
	public void TribalLord_NamesTheTribeItBuffs()
	{
		var text = TextFor("Drogskol Captain");
		Assert.That(text, Does.Contain("Other Spirits you control get +1/+1"));
		Assert.That(text, Does.Contain("Hexproof"));
	}

	[Test]
	public void TargetedGraveyardEffects_NameTheZone()
	{
		Assert.That(
			TextFor("Ghoulcaller's Bargain"),
			Does.Contain("target creature card in your graveyard")
		);
		Assert.That(
			TextFor("Sexton of the Drowned Chapel"),
			Does.Contain("Return target creature card in your graveyard to your hand")
		);
	}

	/// <summary>
	/// A madness creature puts ITSELF onto the battlefield, so "it" is ambiguous. The context
	/// key carrying that is different from the one targeted effects use, so it needs its own
	/// handling.
	/// </summary>
	[Test]
	public void SelfReferentialEffects_SayThis()
	{
		Assert.That(TextFor("Twitching Ghoul"), Does.Contain("Put this onto the battlefield"));
	}

	[Test]
	public void BurnSpells_SayTheyCanGoToTheFace()
	{
		Assert.That(TextFor("Fiery Temper"), Does.Contain("Deal 3 damage to any target"));
	}

	[Test]
	public void MassEffects_ReadAsEachRatherThanTarget()
	{
		Assert.That(
			TextFor("Archangel of Vigils"),
			Does.Contain("Each Human you control gets +2/+2")
		);
	}
}
