using System;
using System.Collections.Immutable;
using System.Linq;
using System.Text.RegularExpressions;
using ImmutableGameObjects;
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

	/// <summary>
	/// P/T moved out of the rules text and onto its own badge, so a French-vanilla creature now
	/// legitimately renders empty rules text. The check that still matters is narrower: a card
	/// that HAS a mechanic must render text for it. A blank face there means the mapper silently
	/// dropped the mechanic, which is the failure this fixture exists to catch.
	/// </summary>
	[Test]
	public void NoCardWithAMechanic_RendersBlankRulesText()
	{
		var blank = Hollowmere
			.Cards.Where(HasPrintableMechanic)
			.Where(c => string.IsNullOrWhiteSpace(MtgCardMapper.GetRulesText(c)))
			.Select(c => c.Name)
			.ToList();

		Assert.That(blank, Is.Empty, $"Blank card faces: {string.Join(", ", blank)}");
	}

	private static bool HasPrintableMechanic(Card card) =>
		card.HasComponent<SpellComponent>()
		|| card.HasComponent<ActivatedAbilityComponent>()
		|| card.HasComponent<TriggeredAbilityComponent>()
		|| card.HasComponent<StaticAbilityComponent>()
		|| card.HasComponent<FlashbackComponent>()
		|| card.HasComponent<ThresholdComponent>()
		|| card.HasComponent<TransformComponent>()
		|| card.HasComponent<GraveyardCountComponent>();

	// ===== Card face budget =====
	//
	// The rules box shrinks its font to fit, down to a 14pt readability floor, then clips. These
	// tests are what stop a new card from silently crossing that floor: a clipped card is not
	// visibly broken in a screenshot, it just quietly stops telling you what it does.

	/// Rendered lines the rules box needs. The box fits roughly 26 characters per line, which is a
	/// proxy for real text measurement — Godot's font metrics are not available in a unit test.
	private static int WrappedLines(string text) =>
		text.Split('\n').Sum(l => Math.Max(1, (int)Math.Ceiling(l.Length / 26.0)));

	/// <summary>
	/// Six lines is what fits above the 14pt floor. Cards used to reach seven purely by repeating
	/// themselves — "Each creature you control gets +2/+2 until end of turn" followed by "Each
	/// creature you control gains Flying until end of turn" — which the clause merge now folds.
	/// </summary>
	[Test]
	public void NoCardExceedsTheRulesBoxLineBudget()
	{
		var overBudget = Hollowmere
			.Cards.Select(c => (c.Name, Lines: WrappedLines(MtgCardMapper.GetRulesText(c))))
			.Where(x => x.Lines > 6)
			.ToList();

		Assert.That(
			overBudget,
			Is.Empty,
			$"Over the 6-line budget: {string.Join(", ", overBudget.Select(x => $"{x.Name} ({x.Lines})"))}"
		);
	}

	/// <summary>
	/// An additional cast cost is paid before the spell resolves, so it appears nowhere in the
	/// effect text. Bargain at the Crossroads read as a free reanimate and only revealed its
	/// discard when the prompt appeared mid-cast.
	/// </summary>
	[Test]
	public void CardsWithAnAdditionalCastCost_PrintIt()
	{
		var silent = Hollowmere
			.Cards.Where(c => !c.AdditionalCastCosts.IsEmpty)
			.Where(c => !MtgCardMapper.GetRulesText(c).Contains("As an additional cost"))
			.Select(c => c.Name)
			.ToList();

		Assert.That(silent, Is.Empty, $"Hidden cast costs: {string.Join(", ", silent)}");
	}

	/// <summary>
	/// The type-line band fits about 24 characters. Three-subtype cards used to overrun it and
	/// clip mid-word ("ture — Werewolf Human C"), which is why the "Creature — " prefix was
	/// dropped — the P/T badge already says the card is a creature.
	/// </summary>
	[Test]
	public void NoTypeLineOverrunsTheBand()
	{
		var tooLong = Hollowmere
			.Cards.Select(c => MtgCardMapper.GetTypeLine(c))
			.Where(t => t.Length > 24)
			.Distinct()
			.ToList();

		Assert.That(tooLong, Is.Empty, $"Type lines over 24 chars: {string.Join(" | ", tooLong)}");
	}

	/// <summary>
	/// The clause merge combines predicates that share a subject. It must never combine nouns:
	/// "Create 2 Human tokens" + "Create 2 Spirit tokens" merged into "Create 2 Human and Spirit
	/// tokens", which reads as two tokens rather than four. Merging wrongly misprints a card;
	/// failing to merge only costs a line.
	/// </summary>
	[Test]
	public void ClauseMerge_NeverDistributesASharedNoun()
	{
		var bad = Hollowmere
			.Cards.SelectMany(c => MtgCardMapper.GetRulesText(c).Split('\n'))
			.Where(l => Regex.IsMatch(l, @"Create .* and .* tokens?"))
			.Distinct()
			.ToList();

		Assert.That(bad, Is.Empty, $"Merged token clauses: {string.Join(" | ", bad)}");
	}

	/// <summary>
	/// The type line replaces rules text as the "this card face rendered something" guarantee —
	/// it is the one element every card has, and it is what tells a drafter a Zombie from a Spirit.
	/// </summary>
	[Test]
	public void EveryCardInTheSet_HasATypeLine()
	{
		var blank = Hollowmere
			.Cards.Where(c => string.IsNullOrWhiteSpace(MtgCardMapper.GetTypeLine(c)))
			.Select(c => c.Name)
			.ToList();

		Assert.That(blank, Is.Empty, $"Blank type lines: {string.Join(", ", blank)}");
	}

	/// <summary>
	/// The duplication this pass removed: P/T used to be printed into the rules text AND drawn in
	/// the corner badge, from two different sources that disagreed once a lord was on the board.
	/// </summary>
	[Test]
	public void CreatureRulesText_DoesNotRepeatPowerToughness()
	{
		var creature = Hollowmere.Cards.First(c =>
			c.HasComponent<CreatureComponent>() && c.HasComponent<TriggeredAbilityComponent>()
		);
		var stats = creature.GetComponent<CreatureComponent>();

		Assert.That(
			MtgCardMapper.GetRulesText(creature),
			Does.Not.Contain($"{stats.Power}/{stats.Toughness}"),
			$"{creature.Name} still prints its P/T in the rules box"
		);
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

	/// <summary>
	/// A keyword the creature was GIVEN has to print like one it was born with. Fury of the Mere
	/// grants Haste to the whole board from the graveyard, and without this the granted creatures
	/// showed no Haste anywhere while still being attackable — the player's only clue that the
	/// card was working at all was noticing the attack went through.
	/// </summary>
	[Test]
	public void GrantedKeywords_ShowOnTheCreatureThatReceivedThem()
	{
		var (state, ids) = MtgGameFactory.CreateForTesting();
		var battlefieldId = state.GetPlayerZoneId(ids.Player1Id, ZoneType.Battlefield);

		var (withCreature, creature) = state.AddObject(
			new Card
			{
				Name = "Test Bear",
				ManaCost = 2,
				OwnerId = ids.Player1Id,
				ControllerId = ids.Player1Id,
				Components = ImmutableArray.Create<GameComponent>(
					new PermanentComponent(),
					new CreatureComponent { Power = 2, Toughness = 2 },
					new AppliedKeywordComponent { GrantsHaste = true, SourceCardId = 999 }
				),
			},
			parentId: battlefieldId
		);

		Assert.That(
			MtgCardMapper.GetRulesText(creature, withCreature),
			Does.Contain("Haste"),
			"A granted keyword must show in the rules text"
		);
		Assert.That(
			MtgCardMapper.GetRulesText(creature),
			Does.Not.Contain("Haste"),
			"Without a game state only printed keywords show — a draft pack card has no grants"
		);
	}

	/// <summary>
	/// A trigger that only counts one subtype must say so. Champion of the Parish read "Whenever
	/// a creature enters" while only ever counting Humans — text that promises more than the card
	/// does is worse than text that is missing.
	/// </summary>
	[Test]
	public void FilteredEntersTrigger_NamesTheSubtypeItCounts()
	{
		var text = TextFor("Champion of the Parish");
		Assert.That(text, Does.Contain("Human"));
		Assert.That(text, Does.Not.Contain("Whenever a creature enters"));
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
		// A spell can ask the player to choose, so it names a target.
		Assert.That(
			TextFor("Ghoulcaller's Bargain"),
			Does.Contain("target creature card in your graveyard")
		);

		// A TRIGGER cannot ask, so it picks automatically — the text has to say so rather
		// than claim a target the player never gets to choose.
		Assert.That(
			TextFor("Sexton of the Drowned Chapel"),
			Does.Contain("choose a creature card from your graveyard")
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

	/// <summary>
	/// The worst class of text bug: the card promises MORE than it does. Zombie Apocalypse
	/// returns only Zombies but read as "each creature card in your graveyard", because the
	/// graveyard phrase was returned before the subtype narrowing was considered.
	///
	/// Walks every targeting specification for a subtype restriction and requires the rendered
	/// text to mention it.
	/// </summary>
	[Test]
	public void SubtypeRestrictions_AppearInTheText()
	{
		var offenders = new List<string>();

		foreach (var card in Hollowmere.Cards)
		{
			var text = MtgCardMapper.GetRulesText(card);

			foreach (var effect in AllEffects(card))
			{
				var subtype = FindSubtype(effect.TargetingStrategy.Specification);
				if (subtype == null)
					continue;
				if (!text.Contains(subtype, StringComparison.OrdinalIgnoreCase))
					offenders.Add($"{card.Name} (restricted to {subtype})");
			}
		}

		Assert.That(
			offenders.Distinct(),
			Is.Empty,
			"Text omits a subtype restriction: " + string.Join("; ", offenders.Distinct())
		);
	}

	private static IEnumerable<CardEffect> AllEffects(Card card)
	{
		var spell = card.GetComponent<SpellComponent>();
		if (spell != null)
			foreach (var e in spell.Effects)
				yield return e;

		foreach (var t in card.GetComponents<TriggeredAbilityComponent>())
		foreach (var e in t.Effects)
			yield return e;

		foreach (var a in card.GetComponents<ActivatedAbilityComponent>())
		foreach (var e in a.Effects)
			yield return e;
	}

	/// Specs compose with And/Or, so a subtype can sit several levels down.
	private static string? FindSubtype(TargetSpecification? spec) =>
		spec switch
		{
			IsSubtypeSpecification s => s.Subtype,
			AndSpecification a => FindSubtype(a.Left) ?? FindSubtype(a.Right),
			OrSpecification o => FindSubtype(o.Left) ?? FindSubtype(o.Right),
			_ => null,
		};

	[Test]
	public void ZombieApocalypse_SaysItOnlyReturnsZombies()
	{
		Assert.That(TextFor("Zombie Apocalypse"), Does.Contain("Zombie card in your graveyard"));
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
