using System;
using System.Linq;
using MtgCore;
using MtgGame;
using NUnit.Framework;

namespace SQGodotCommon.Tests;

/// <summary>
/// CMB's counterpart to <c>HollowmereRulesTextTests</c> and <c>CoresetCubeRulesTextTests</c>.
///
/// **This set is not drafted, and it still needs this.** `MtgCardMapper.GetRulesText` silently omits
/// any mechanic it does not know, and CMB introduced four things that render — readying a creature,
/// creating a token copy, removing a +1/+1 counter as a cost, and a counter bonus. A missing case
/// prints a card that confidently says less than it does, which is worse than saying nothing: the
/// project has already shipped a modal spell whose rendered text described the OPPOSITE of the card.
///
/// The dump is the point as much as the assertions — the standing rule is to READ every new card's
/// rendered text, not to trust that it is non-empty.
/// </summary>
[TestFixture]
public class ComboProvingRulesTextTests
{
	private static string TextFor(string cardName) =>
		MtgCardMapper.GetRulesText(
			ComboProving.Cards.First(c =>
				string.Equals(c.Name, cardName, StringComparison.OrdinalIgnoreCase)
			)
		);

	[Test]
	public void NoCardWithAMechanic_RendersBlankRulesText()
	{
		var blank = ComboProving
			.Cards.Where(HasPrintableMechanic)
			.Where(c => string.IsNullOrWhiteSpace(MtgCardMapper.GetRulesText(c)))
			.Select(c => c.Name)
			.ToList();

		Assert.That(blank, Is.Empty, $"Blank card faces: {string.Join(", ", blank)}");
	}

	[Test]
	public void EveryCard_HasATypeLine()
	{
		var blank = ComboProving
			.Cards.Where(c => string.IsNullOrWhiteSpace(MtgCardMapper.GetTypeLine(c)))
			.Select(c => c.Name)
			.ToList();

		Assert.That(blank, Is.Empty, $"Blank type lines: {string.Join(", ", blank)}");
	}

	/// <summary>The untap primitive — "ready", never "untap"; there is no tapping in this engine.</summary>
	[Test]
	public void TheUntapper_SaysItReadiesACreature()
	{
		var text = TextFor("Mirevale Deceiver");
		TestContext.Out.WriteLine(text);

		Assert.Multiple(() =>
		{
			Assert.That(text, Does.Contain("Ready").IgnoreCase);
			Assert.That(text, Does.Not.Contain("untap").IgnoreCase, "the engine's word is 'ready'");
		});
	}

	/// <summary>The copy primitive. Without a mapper case this whole card face was blank.</summary>
	[Test]
	public void TheCopier_SaysItMakesATokenCopy()
	{
		var text = TextFor("Twinflame Artisan");
		TestContext.Out.WriteLine(text);

		Assert.That(text, Does.Contain("copy").IgnoreCase);
	}

	/// <summary>
	/// The +1/+1 counter COST — the only thing limiting the Ballista's damage ability.
	///
	/// **This test first passed while the bug was live, and that is worth keeping.** Asserting on
	/// "counter" matched the card's OTHER line ("Enters with X +1/+1 counters"), so it went green
	/// while the ability itself rendered "Fling Spore (free): Deal 1 damage to any target" — an
	/// unlimited free damage engine as far as the face was concerned. It now asserts the ability
	/// line specifically and that the word "free" is absent, which is what actually distinguishes
	/// the two states.
	/// </summary>
	[Test]
	public void TheBallista_StatesItsCounterCostOnTheAbilityItself()
	{
		var text = TextFor("Sporeback Ballista");
		TestContext.Out.WriteLine(text);

		var flingLine =
			text.Split('\n').FirstOrDefault(l => l.Contains("Fling", StringComparison.Ordinal))
			?? "";

		Assert.Multiple(() =>
		{
			Assert.That(flingLine, Is.Not.Empty, "the damage ability did not render at all");
			Assert.That(
				flingLine,
				Does.Contain("+1/+1 counter"),
				"the ability's cost is missing from its own line"
			);
			Assert.That(
				flingLine,
				Does.Not.Contain("free"),
				"it rendered as a free ability — the cost fell through DescribeCost"
			);
		});
	}

	/// <summary>
	/// **"loses 0 life" is worse than a blank card**: it describes half of a two-card kill as doing
	/// nothing. `GainLifeAction` already handled a context-driven amount; `LoseLifeAction` only
	/// special-cased the reveal key, so every other context key fell through to the literal 0.
	/// </summary>
	[Test]
	public void TheDrainHalf_DoesNotPrintZeroLife()
	{
		var text = TextFor("Covenant of Thorns");
		TestContext.Out.WriteLine(text);

		Assert.Multiple(() =>
		{
			Assert.That(text, Does.Not.Contain("0 life"), "context-driven amount printed as zero");
			Assert.That(text, Does.Contain("that much"), "it should say 'that much life'");
		});
	}

	/// <summary>The drain pair — "that much" is the whole card; a flat number would be a lie.</summary>
	[Test]
	public void TheDrainPair_ExplainsBothHalves()
	{
		var bond = TextFor("Covenant of Thorns");
		var blood = TextFor("Sanguine Reciprocity");
		TestContext.Out.WriteLine($"Covenant of Thorns: {bond}\nSanguine Reciprocity: {blood}");

		Assert.Multiple(() =>
		{
			Assert.That(bond, Does.Contain("life").IgnoreCase);
			Assert.That(blood, Does.Contain("life").IgnoreCase);
		});
	}

	[Test]
	[Explicit("Diagnostic — read every CMB card's rendered face.")]
	public void DumpEveryCardFace()
	{
		foreach (var card in ComboProving.Cards)
			TestContext.Out.WriteLine(
				$"=== {card.Name}  ({MtgCardMapper.GetTypeLine(card)})"
					+ $"  {MtgCardMapper.GetPowerToughness(card, null)}\n"
					+ $"    {MtgCardMapper.GetRulesText(card).Replace("\n", "\n    ")}"
			);
	}

	private static bool HasPrintableMechanic(Card card) =>
		card.HasComponent<SpellComponent>()
		|| card.HasComponent<ActivatedAbilityComponent>()
		|| card.HasComponent<TriggeredAbilityComponent>()
		|| card.HasComponent<StaticAbilityComponent>()
		|| card.HasComponent<CounterTrapComponent>()
		|| card.HasComponent<EquipmentComponent>();
}
