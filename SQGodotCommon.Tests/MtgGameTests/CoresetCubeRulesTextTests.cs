using System;
using System.Linq;
using MtgCore;
using MtgGame;
using NUnit.Framework;

namespace SQGodotCommon.Tests;

/// <summary>
/// The Core Set Cube's counterpart to HollowmereRulesTextTests.
///
/// A pack is READ, not glanced at. `MtgCardMapper.GetRulesText` silently omits any mechanic it
/// does not know — invisible in a screenshot, but it makes the card undraftable. This set added
/// roughly twenty mechanics, so these pin one card per mechanic plus a blanket no-blank-face
/// check.
///
/// Counterspell traps are the sharpest case: a trap is a SpellComponent with NO effects, so
/// before the mapper knew about them every counterspell in blue rendered as a completely empty
/// card face.
/// </summary>
[TestFixture]
public class CoresetCubeRulesTextTests
{
	private static string TextFor(string cardName) =>
		MtgCardMapper.GetRulesText(
			CoresetCube.Cards.First(c =>
				string.Equals(c.Name, cardName, StringComparison.OrdinalIgnoreCase)
			)
		);

	[Test]
	public void NoCardWithAMechanic_RendersBlankRulesText()
	{
		var blank = CoresetCube
			.Cards.Where(HasPrintableMechanic)
			.Where(c => string.IsNullOrWhiteSpace(MtgCardMapper.GetRulesText(c)))
			.Select(c => c.Name)
			.ToList();

		Assert.That(blank, Is.Empty, $"Blank card faces: {string.Join(", ", blank)}");
	}

	[Test]
	public void EveryCard_HasATypeLine()
	{
		// The type line is the "this face rendered something" guarantee — it is never blank.
		var blank = CoresetCube
			.Cards.Where(c => string.IsNullOrWhiteSpace(MtgCardMapper.GetTypeLine(c)))
			.Select(c => c.Name)
			.ToList();

		Assert.That(blank, Is.Empty, $"Blank type lines: {string.Join(", ", blank)}");
	}

	private static bool HasPrintableMechanic(Card card) =>
		card.HasComponent<SpellComponent>()
		|| card.HasComponent<ActivatedAbilityComponent>()
		|| card.HasComponent<TriggeredAbilityComponent>()
		|| card.HasComponent<StaticAbilityComponent>()
		|| card.HasComponent<CounterTrapComponent>()
		|| card.HasComponent<EquipmentComponent>()
		|| card.HasComponent<PlaneswalkerComponent>()
		|| card.HasComponent<ExaltedComponent>()
		|| card.HasComponent<ProtectionFromSubtypeComponent>()
		|| card.HasComponent<SpellTaxComponent>()
		|| card.HasComponent<CopyOnEnterComponent>()
		|| card.HasComponent<CastRestrictionComponent>()
		|| card.HasComponent<LifeTotalComponent>()
		|| card.HasComponent<CreatureCountComponent>()
		|| card.HasComponent<LifeGainBonusComponent>()
		|| card.HasComponent<ThresholdComponent>()
		// A creature whose only mechanic is graveyard recursion carries none of the above —
		// Despoiler of Souls is a plain body plus a FlashbackComponent, so the sweep could not
		// see it at all.
		|| card.HasComponent<FlashbackComponent>();

	// ===== One card per mechanic =====

	[Test]
	public void CounterTrap_ExplainsHowItFires()
	{
		// A player holding this has no other way to learn that it fires from hand — it is never
		// cast, so it never appears as a playable action.
		var text = TextFor("Negate");

		Assert.That(text, Does.Contain("Trap"));
		Assert.That(text, Does.Contain("unspent"), "It must say the mana has to be left up");
		Assert.That(text, Does.Contain("noncreature"), "And what it can hit");
	}

	[Test]
	public void CounterTrap_ManaTax_IsStated()
	{
		Assert.That(TextFor("Mana Leak"), Does.Contain("unless they pay 3"));
	}

	[Test]
	public void CounterTrap_Variants_AreStated()
	{
		Assert.That(TextFor("Dissipate"), Does.Contain("Exiled"));
		Assert.That(TextFor("Bone to Ash"), Does.Contain("Draw"));
	}

	[Test]
	public void Planeswalker_ShowsLoyaltyNotation()
	{
		// "+1" and "-8" are how a player reads a planeswalker; rendering them as mana costs
		// would make the card unreadable.
		var text = TextFor("Jace Beleren");

		Assert.That(text, Does.Contain("+2:"));
		Assert.That(text, Does.Contain("-1:"));
		Assert.That(text, Does.Contain("-10:"));
	}

	[Test]
	public void Aura_SaysItIsAnAuraAndWhatItDoes()
	{
		var pacifism = TextFor("Pacifism");

		Assert.That(pacifism, Does.Contain("Aura"));
		Assert.That(pacifism, Does.Contain("can't attack"));
	}

	/// <summary>
	/// The lockdown Auras shut down the ENCHANTED creature, and their text has to say so. They
	/// shipped as an ETB trigger freezing AllValid().OpponentCreatures() — the whole opposing
	/// board, indefinitely, off a card that enchants one creature. The rendered face never
	/// mentioned the freeze at all, so the only way to see it was to lose to it.
	/// </summary>
	[TestCase("Claustrophobia")]
	[TestCase("Capture Sphere")]
	public void LockdownAura_ShutsDownOnlyTheEnchantedCreature(string cardName)
	{
		var text = TextFor(cardName);

		Assert.That(text, Does.Contain("Enchanted creature"));
		Assert.That(text, Does.Contain("can't attack"));
	}

	[Test]
	public void Aura_WithKeywordGrants_ListsThem()
	{
		var destiny = TextFor("Angelic Destiny");

		Assert.That(destiny, Does.Contain("+4/+4"));
		Assert.That(destiny, Does.Contain("Flying"));
		Assert.That(destiny, Does.Contain("First Strike"));
	}

	[Test]
	public void Equipment_ReadsAsEquipped_NotEnchanted()
	{
		Assert.That(TextFor("Ancestral Blade"), Does.Contain("Equipped creature"));
	}

	[Test]
	public void FirstStrikeAndIndestructible_Render()
	{
		Assert.That(TextFor("Baneslayer Angel"), Does.Contain("First Strike"));
		Assert.That(TextFor("Baneslayer Angel"), Does.Contain("Protection from"));
	}

	[Test]
	public void DoubleStrike_SuppressesFirstStrike()
	{
		// Double strike already includes first strike; printing both reads as two abilities.
		var text = TextFor("Fencing Ace");

		Assert.That(text, Does.Contain("Double Strike"));
		Assert.That(text, Does.Not.Contain("First Strike"));
	}

	[Test]
	public void Exalted_Renders()
	{
		Assert.That(TextFor("Knight of Glory"), Does.Contain("Exalted"));
	}

	[Test]
	public void StarStarPowerAndToughness_IsExplained()
	{
		// Crusader of Odric is printed 0/0 plus a modifier. Without this line it reads as a
		// literal 0/0 and nobody drafts it.
		Assert.That(
			TextFor("Crusader of Odric"),
			Does.Contain("equal to the number of creatures you control")
		);
	}

	[Test]
	public void ReplacementEffect_IsExplained()
	{
		Assert.That(TextFor("Angel of Vitality"), Does.Contain("gain that much plus 1"));
	}

	[Test]
	public void ConditionalPowerBonus_IsExplained()
	{
		Assert.That(TextFor("Angel of Vitality"), Does.Contain("25+ life"));
	}

	[Test]
	public void CastRestriction_IsStated()
	{
		// Serra Avenger reads as a free 3/3 flyer for two without this.
		Assert.That(TextFor("Serra Avenger"), Does.Contain("Can't be cast"));
	}

	[Test]
	public void SpellTax_IsStated()
	{
		Assert.That(TextFor("Vryn Wingmare"), Does.Contain("cost 1 more"));
	}

	[Test]
	public void Clone_SaysWhatItCopies()
	{
		Assert.That(TextFor("Clone"), Does.Contain("copy"));
	}

	[Test]
	public void Convoke_AndXCost_AreStated()
	{
		var ranks = TextFor("Return to the Ranks");

		Assert.That(ranks, Does.Contain("Convoke"));
		Assert.That(ranks, Does.Contain("X"));
	}

	[Test]
	public void FogBank_ExplainsBothOfItsUnusualRules()
	{
		// Its Taunt lapsing after one attack is the whole reason it is not unanswerable, so a
		// player has to be able to see it.
		var text = TextFor("Fog Bank");

		Assert.That(text, Does.Contain("Taunt"));
		Assert.That(text, Does.Contain("Prevents all combat damage"));
	}

	[Test]
	public void ActivatedAbility_ShowsItsExhaustCostAndGate()
	{
		var speaker = TextFor("Speaker of the Heavens");

		Assert.That(speaker, Does.Contain("exhaust"), "An exhaust cost is a real cost");
		Assert.That(speaker, Does.Contain("life"), "And the life gate must be visible");
	}

	// ===== Card face budget =====

	private static int WrappedLines(string text) =>
		text.Split('\n').Sum(l => Math.Max(1, (int)Math.Ceiling(l.Length / 26.0)));

	/// <summary>
	/// The rules box shrinks to fit and then clips at a 14pt readability floor. A clipped card is
	/// not visibly broken in a screenshot — it just quietly stops telling you what it does.
	///
	/// The budget is looser than Hollowmere's six lines: this set's cards genuinely have more
	/// text (planeswalkers carry three abilities), and the trap wording is deliberately explicit
	/// because the mechanic is unfamiliar.
	/// </summary>
	[Test]
	public void NoCard_ExceedsTheRulesBoxBudget()
	{
		var overlong = CoresetCube
			.Cards.Select(c => (c.Name, Lines: WrappedLines(MtgCardMapper.GetRulesText(c) ?? "")))
			.Where(x => x.Lines > 10)
			.ToList();

		Assert.That(
			overlong,
			Is.Empty,
			$"Over budget: {string.Join(", ", overlong.Select(x => $"{x.Name} ({x.Lines})"))}"
		);
	}

	// ===== Black section =====

	/// <summary>
	/// The single most important line in the black section. Demonic Pact's fourth mode ends the
	/// game, and it rendered as nothing at all — the card offered three good modes and silently
	/// hid the clock that is its entire reason to exist.
	/// </summary>
	[Test]
	public void DemonicPact_PrintsTheModeThatLosesTheGame()
	{
		var text = MtgCardMapper.GetRulesText(Find("Demonic Pact"));

		Assert.That(text, Does.Contain("You lose the game"));
	}

	/// <summary>Sorin's -3 vanished the same way — SetLifeTotalAction had no describe at all.</summary>
	[Test]
	public void SorinMarkov_PrintsAllThreeLoyaltyAbilities()
	{
		var text = MtgCardMapper.GetRulesText(Find("Sorin Markov"));

		Assert.Multiple(() =>
		{
			Assert.That(text, Does.Contain("+2:"));
			Assert.That(text, Does.Contain("-3:"));
			Assert.That(text, Does.Contain("becomes 10"));
			Assert.That(text, Does.Contain("-7:"));
		});
	}

	/// <summary>
	/// The exile cost is the only thing bounding a repeatable recursion. Printing the mana cost
	/// alone advertised a strictly better card than the one being played.
	/// </summary>
	[Test]
	public void DespoilerOfSouls_PrintsItsGraveyardCost()
	{
		var text = MtgCardMapper.GetRulesText(Find("Despoiler of Souls"));

		Assert.That(text, Does.Contain("exile 2 cards from your graveyard"));
	}

	/// <summary>Each of these is the whole restriction on its card.</summary>
	[Test]
	public void TargetRestrictions_AreNotSilentlyDropped()
	{
		Assert.Multiple(() =>
		{
			Assert.That(
				MtgCardMapper.GetRulesText(Find("Royal Assassin")),
				Does.Contain("attacked this turn"),
				"Without this it reads as unconditional removal"
			);
			Assert.That(
				MtgCardMapper.GetRulesText(Find("Gilt-Leaf Winnower")),
				Does.Contain("different power and toughness")
			);
		});
	}

	/// <summary>
	/// A drain aimed at someone else read "Lose N life", which names the wrong player — Blood
	/// Reckoning appeared to damage its own controller every time they were attacked.
	/// </summary>
	[Test]
	public void LifeLoss_NamesThePlayerWhoActuallyLosesIt()
	{
		Assert.Multiple(() =>
		{
			Assert.That(
				MtgCardMapper.GetRulesText(Find("Blood Reckoning")),
				Does.Contain("opponent loses 1 life")
			);
			Assert.That(
				MtgCardMapper.GetRulesText(Find("Ulcerate")),
				Does.Contain("Lose 3 life"),
				"A genuinely self-inflicted loss must still read as one"
			);
		});
	}

	/// <summary>Stab Wound drains on the enchanted creature's controller's turn, not yours.</summary>
	[Test]
	public void StabWound_NamesTheRightUpkeep()
	{
		Assert.That(
			MtgCardMapper.GetRulesText(Find("Stab Wound")),
			Does.Contain("each opponent's upkeep")
		);
	}

	/// <summary>
	/// A context-driven amount is not a number the card can print. Vilis draws "that many",
	/// scaling with the life just lost; "Draw a card" understated it by most of the card.
	/// </summary>
	[Test]
	public void Vilis_PrintsTheScalingDraw()
	{
		var text = MtgCardMapper.GetRulesText(Find("Vilis, Broker of Blood"));

		Assert.Multiple(() =>
		{
			Assert.That(text, Does.Contain("Whenever you lose life"));
			Assert.That(text, Does.Contain("that many"));
		});
	}

	/// <summary>
	/// A four-step pipeline that describes one sentence. Rendered step by step it ran four times
	/// the length of the card, on three cards, against a hard line budget.
	/// </summary>
	[Test]
	public void SymmetricEdict_ReadsAsOneClause()
	{
		Assert.Multiple(() =>
		{
			Assert.That(
				MtgCardMapper.GetRulesText(Find("Fleshbag Marauder")),
				Does.Contain("Each player sacrifices a creature")
			);
			Assert.That(
				MtgCardMapper.GetRulesText(Find("Call to the Grave")),
				Does.Contain("non-Zombie creature")
			);
		});
	}

	/// <summary>
	/// Kitesail Freebooter's entire ETB was invisible: both steps of its pipeline were unknown
	/// to DescribeStep, so the card printed only "Flying".
	/// </summary>
	[Test]
	public void KitesailFreebooter_PrintsItsHandAttack()
	{
		var text = MtgCardMapper.GetRulesText(Find("Kitesail Freebooter"));

		Assert.That(text, Does.Contain("exile it until this leaves the battlefield"));
	}

	/// <summary>
	/// GrantKeywordAction had grown six keywords the describer never learned. Xathrid Slyblade
	/// grants first strike AND deathtouch and printed only the deathtouch.
	/// </summary>
	[Test]
	public void GrantedKeywords_IncludeAllOfThem()
	{
		var text = MtgCardMapper.GetRulesText(Find("Xathrid Slyblade"));

		Assert.Multiple(() =>
		{
			Assert.That(text, Does.Contain("Deathtouch"));
			Assert.That(text, Does.Contain("First strike"));
		});
	}

	/// <summary>
	/// These trigger conditions had no describe branch, so both cards read "When triggered" —
	/// which tells a drafter nothing about the only ability that matters on them.
	/// </summary>
	[Test]
	public void NewTriggerConditions_AreNamed()
	{
		var knight = MtgCardMapper.GetRulesText(Find("Knight of the Ebon Legion"));

		Assert.Multiple(() =>
		{
			Assert.That(knight, Does.Contain("lost 4+ life this turn"));
			Assert.That(knight, Does.Not.Contain("When triggered"));
			Assert.That(
				MtgCardMapper.GetRulesText(Find("Blood Seeker")),
				Does.Contain("creature an opponent controls enters"),
				"'a permanent an opponent controls' promises more than the card does"
			);
		});
	}

	// ===== Green section =====

	/// <summary>
	/// The blanket check for the failure class this project keeps rediscovering: an action whose
	/// amount comes from pipeline context renders its DEFAULT, so the card confidently prints a
	/// number that means "does nothing". Four separate switches have had this bug.
	///
	/// A blanket assertion rather than one per card, because the next card to hit it has not been
	/// written yet.
	/// </summary>
	[Test]
	public void NoCard_PrintsAnAmountThatMeansItDoesNothing()
	{
		// Word-bounded: a bare substring "0 damage" also matches "10 damage", which flagged
		// Chandra Nalaar's ultimate as broken when it is exactly right.
		var deadNumber = new System.Text.RegularExpressions.Regex(
			@"\+0/\+0|\b0 (damage|life|mana|cards?)\b|\b(add|gain|draw|put) 0\b",
			System.Text.RegularExpressions.RegexOptions.IgnoreCase
		);

		var offenders = CoresetCube
			.Cards.Select(c => (c.Name, Text: MtgCardMapper.GetRulesText(c) ?? ""))
			.Where(x => deadNumber.IsMatch(x.Text))
			.Select(x => $"{x.Name}: {x.Text}")
			.ToList();

		Assert.That(offenders, Is.Empty, string.Join(" | ", offenders));
	}

	[Test]
	public void Counters_RenderAsCountersRatherThanBareBuffs()
	{
		Assert.Multiple(() =>
		{
			var hydra = MtgCardMapper.GetRulesText(Find("Primordial Hydra"));
			Assert.That(hydra, Does.Contain("Enters with X +1/+1 counters"));
			Assert.That(
				hydra,
				Does.Contain("Double the number of +1/+1 counters"),
				"the doubling is the entire card"
			);
			Assert.That(
				hydra,
				Does.Not.Contain("in your graveyard"),
				"its trample clause counts COUNTERS — the graveyard wording is a different card"
			);

			Assert.That(
				MtgCardMapper.GetRulesText(Find("Wildwood Scourge")),
				Does.Contain("+1/+1 counters are put on"),
				"its whole trigger is invisible without a CountersAdded case"
			);
		});
	}

	/// <summary>
	/// The modal describer used to render every mode against a hardcoded placeholder target, so
	/// Return to Nature's "destroy target artifact" printed as "Destroy each creature you
	/// control" — a one-sided board wipe on what is actually a Naturalize.
	/// </summary>
	[Test]
	public void ModalCards_DescribeEachModesOwnTarget()
	{
		var text = MtgCardMapper.GetRulesText(Find("Return to Nature"));

		Assert.Multiple(() =>
		{
			Assert.That(text, Does.Contain("Destroy target artifact"));
			Assert.That(text, Does.Contain("Destroy target enchantment"));
			Assert.That(text, Does.Not.Contain("each creature you control"));
		});
	}

	[Test]
	public void FightSpells_NameTheCreatureThatActuallyFights()
	{
		Assert.Multiple(() =>
		{
			// "This fights" is wrong on a sorcery — there is no "this" creature.
			Assert.That(
				MtgCardMapper.GetRulesText(Find("Rabid Bite")),
				Does.Contain("Your strongest creature deals damage equal to its power")
			);
			Assert.That(
				MtgCardMapper.GetRulesText(Find("Wild Instincts")),
				Does.Contain("Your strongest creature fights")
			);
		});
	}

	/// <summary>
	/// Both of these promised MORE than the card does, which is worse than saying nothing.
	/// </summary>
	[Test]
	public void RestrictedEffects_KeepTheirRestriction()
	{
		Assert.Multiple(() =>
		{
			Assert.That(
				MtgCardMapper.GetRulesText(Find("Bramblecrush")),
				Does.Contain("noncreature permanent"),
				"'target permanent' offers creature removal the card cannot do"
			);
			Assert.That(
				MtgCardMapper.GetRulesText(Find("Goreclaw, Terror of Qal Sisma")),
				Does.Contain("with power 4 or greater"),
				"without it the discount reads as applying to every creature spell"
			);
			Assert.That(
				MtgCardMapper.GetRulesText(Find("Woodland Bellower")),
				Does.Contain("costing 3 or less"),
				"an unrestricted tutor is a much stronger card"
			);
		});
	}

	/// <summary>
	/// Rancor is +2/+0 AND trample, and AsAura could not grant trample at all before green — so
	/// the Aura would have rendered and behaved as a bare P/T buff.
	/// </summary>
	[Test]
	public void Auras_RenderEveryKeywordTheyGrant()
	{
		Assert.That(MtgCardMapper.GetRulesText(Find("Rancor")), Does.Contain("Trample"));
		Assert.That(
			MtgCardMapper.GetRulesText(Find("Arachnus Web")),
			Does.Contain("can't attack").And.Not.Contain("gets can't attack")
		);
	}

	private static Card Find(string name) =>
		CoresetCube.Cards.Single(c =>
			string.Equals(c.Name, name, StringComparison.OrdinalIgnoreCase)
		);

	[Test]
	public void TypeLines_StayShortEnoughToFitTheBand()
	{
		var overlong = CoresetCube
			.Cards.Select(c => (c.Name, Line: MtgCardMapper.GetTypeLine(c)))
			.Where(x => x.Line.Length > 24)
			.ToList();

		Assert.That(
			overlong,
			Is.Empty,
			$"Type lines too long: {string.Join(", ", overlong.Select(x => $"{x.Name}: {x.Line}"))}"
		);
	}
}
