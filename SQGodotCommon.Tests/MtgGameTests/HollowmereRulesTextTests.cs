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
}
