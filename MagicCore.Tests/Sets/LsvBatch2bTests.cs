using ImmutableGameObjects;
using MagicCore.Actions;
using MagicCore.Cards;
using MagicCore.Effects;
using MagicCore.Sets;
using Nu = NUnit.Framework.Is;

namespace MagicCore.Tests.Sets;

/// <summary>
/// MC-4.4b, LSVPower's batch 2 — the mana batch 1 left out: the Spirit Guides (a mana ability of a card in the
/// HAND, spent only when the battlefield can't pay) and Delighted Halfling (two abilities, one restricted to
/// legendary spells, whose spell can't be countered) — each FIRES through real casting, against its printed text.
/// </summary>
public class LsvBatch2bTests : FraBoardTest
{
	static CardDefinition Lsv(string name) => CardRegistry.Get(name, "LSV");

	static CardDefinition Boon =>
		TestGames.Inline("Boon", "{1}", "Instant", null, new GainLife(Ref.You, 1));

	static CardDefinition GreenBoon =>
		TestGames.Inline("Green Boon", "{G}", "Instant", null, new GainLife(Ref.You, 1));

	static CardDefinition LegendaryBoon =>
		TestGames.Inline(
			"Legendary Boon",
			"{G}",
			"Legendary Sorcery",
			null,
			new GainLife(Ref.You, 1)
		);

	void TapAllMyLands()
	{
		foreach (
			var land in s.CardsIn(me, ZoneType.Battlefield).Where(c => c.Is(CardType.Land)).ToList()
		)
			s = s.Tap(land.Id);
	}

	bool InExile(int card) => s.IsIn(card, me, ZoneType.Exile);

	// ===== The Spirit Guides: "Exile this card from your hand: Add {G}." =====

	[Test]
	public void ElvishSpiritGuide_PaysFromTheHand_AndIsExiled()
	{
		var guide = Hand(Lsv("Elvish Spirit Guide"), me);
		var boon = Hand(GreenBoon, me);
		TapAllMyLands();
		var life = Life(me);
		Cast(boon, []);
		Assert.That(InExile(guide), Nu.True, "exiled to pay");
		Assert.That(Life(me), Nu.EqualTo(life + 1), "its {G} paid for the spell");
	}

	[Test]
	public void SimianSpiritGuide_MakesRed_NotGreen()
	{
		var guide = Hand(Lsv("Simian Spirit Guide"), me);
		var bolt = Hand(Lsv("Lightning Bolt"), me);
		var green = Hand(GreenBoon, me);
		TapAllMyLands();
		Assert.That(Casts(green), Nu.Empty, "{R} can't pay {G}");
		var life = Life(them);
		Cast(bolt, [], them);
		Assert.That(InExile(guide), Nu.True);
		Assert.That(Life(them), Nu.EqualTo(life - 3));
	}

	[Test]
	public void SpiritGuide_SpentLast_ALandPaysFirst()
	{
		var guide = Hand(Lsv("Elvish Spirit Guide"), me);
		var boon = Hand(GreenBoon, me);
		Cast(boon, []);
		Assert.That(s.IsIn(guide, me, ZoneType.Hand), Nu.True, "a Forest paid; the card stays");
	}

	[Test]
	public void SpiritGuide_OnTheBattlefield_MakesNoMana()
	{
		Field(Lsv("Elvish Spirit Guide"), me);
		var boon = Hand(Boon, me);
		TapAllMyLands();
		Assert.That(Casts(boon), Nu.Empty, "its ability works only from the hand");
	}

	[Test]
	public void SpiritGuides_OnlyAsManyAsTheLandsLeaveOwed()
	{
		var first = Hand(Lsv("Elvish Spirit Guide"), me);
		var second = Hand(Lsv("Elvish Spirit Guide"), me);
		var spell = Hand(
			TestGames.Inline("Twin Boon", "{G}{G}", "Instant", null, new GainLife(Ref.You, 1)),
			me
		);
		TapAllMyLands();
		s = s.Untap(
			s.CardsIn(me, ZoneType.Battlefield).First(c => c.Subtypes.Contains("Forest")).Id
		);
		Cast(spell, []);
		Assert.That(
			new[] { first, second }.Count(InExile),
			Nu.EqualTo(1),
			"a Forest, then one card"
		);
	}

	[Test]
	public void SpiritGuide_ExiledForMana_IsAnActivationKaiWinnSees()
	{
		var kai = Field(CardRegistry.Get("Kai Winn, Faithless Leader", "TRK"), them);
		s = s.UpdateObject(
			kai,
			s.GetCard(kai).WithComponent(new ChosenName("Elvish Spirit Guide"))
		);
		Hand(Lsv("Elvish Spirit Guide"), me);
		var boon = Hand(GreenBoon, me);
		TapAllMyLands();
		var life = Life(me);
		Cast(boon, []);
		Assert.That(Life(me), Nu.EqualTo(life + 1 - 3), "the boon's 1, Kai Winn's 3");
	}

	[Test]
	public void SpiritGuides_CountTowardX()
	{
		Hand(Lsv("Elvish Spirit Guide"), me);
		Hand(Lsv("Simian Spirit Guide"), me);
		var spell = Hand(
			TestGames.Inline("X Boon", "{X}", "Instant", null, new GainLife(Ref.You, Count.X)),
			me
		);
		TapAllMyLands();
		Assert.That(Casts(spell).Max(c => c.X), Nu.EqualTo(2));
	}

	[Test]
	public void SpiritGuide_NeverSpentForSunburstsColour()
	{
		var guide = Hand(Lsv("Simian Spirit Guide"), me);
		var explosives = Hand(CardRegistry.Get("Engineered Explosives"), me);
		TapAllMyLands();
		foreach (
			var forest in s.CardsIn(me, ZoneType.Battlefield)
				.Where(c => c.Subtypes.Contains("Forest"))
				.Take(2)
				.ToList()
		)
			s = s.Untap(forest.Id);
		s = s.Act(Casts(explosives).Single(c => c.X == 2));
		Resolve();
		Assert.That(
			s.IsIn(guide, me, ZoneType.Hand),
			Nu.True,
			"two Forests pay; red isn't worth a card"
		);
		Assert.That(s.GetCard(explosives).ChargeCounters, Nu.EqualTo(1));
	}

	[Test]
	public void SpiritGuides_AreNoLastingSource_ForTheSealedBuilder() =>
		Assert.That(global::MagicCore.Limited.SealedBuilder.Fixes("Elvish Spirit Guide"), Nu.Empty);

	// ===== Delighted Halfling: {C}, or any colour for a legendary spell that can't be countered =====

	[Test]
	public void DelightedHalfling_ColorOnlyForALegendarySpell()
	{
		Field(Lsv("Delighted Halfling"), me);
		var green = Hand(GreenBoon, me);
		var boon = Hand(Boon, me);
		TapAllMyLands();
		Assert.That(Casts(green), Nu.Empty, "{G} for a nonlegendary spell: only its {C}");
		Cast(boon, []);
		Assert.That(
			s.CardsIn(me, ZoneType.Graveyard).Any(c => c.Id == boon),
			Nu.True,
			"its {C} pays generic"
		);
	}

	[Test]
	public void DelightedHalfling_ALegendarySpellItPaysFor_CantBeCountered()
	{
		Field(Lsv("Delighted Halfling"), me);
		var legend = Hand(LegendaryBoon, me);
		TapAllMyLands();
		s = s.Act(new CastSpellAction { PlayerId = me, CardId = legend });
		Assert.That(
			s.GetCard(legend).GetComponent<OnStackComponent>()!.CantBeCountered,
			Nu.True,
			"that spell can't be countered"
		);
	}

	[Test]
	public void DelightedHalfling_ALegendarySpellPaidByLands_CanBeCountered()
	{
		Field(Lsv("Delighted Halfling"), me);
		var legend = Hand(LegendaryBoon, me);
		s = s.Act(new CastSpellAction { PlayerId = me, CardId = legend });
		Assert.That(
			s.GetCard(legend).GetComponent<OnStackComponent>()!.CantBeCountered,
			Nu.False,
			"a Forest paid: not its mana"
		);
	}

	[Test]
	public void DelightedHalfling_TappedByHand_OffersItsColorlessToo()
	{
		var halfling = Field(Lsv("Delighted Halfling"), me);
		Assert.That(
			s.TapOptions(me, halfling).Select(t => t.Color),
			Nu.EquivalentTo(ManaAbilities.AnyColor.Add(ManaColor.Colorless))
		);
		s = s.Act(
			new TapForManaAction
			{
				PlayerId = me,
				CardId = halfling,
				Color = ManaColor.Colorless,
			}
		);
		var pool = s.GetPlayer(me).ManaPool;
		Assert.That(pool[ManaColor.Colorless], Nu.EqualTo(1), "unrestricted {C}");
		Assert.That(pool.Restricted, Nu.Empty);
	}

	[Test]
	public void DelightedHalfling_ItsColorlessCountsTowardANonlegendaryX()
	{
		Field(Lsv("Delighted Halfling"), me);
		var spell = Hand(
			TestGames.Inline("X Boon", "{X}", "Instant", null, new GainLife(Ref.You, Count.X)),
			me
		);
		TapAllMyLands();
		Assert.That(Casts(spell).Max(c => c.X), Nu.EqualTo(1));
	}

	[Test]
	public void DelightedHalfling_IsNoFiveColourFixer() =>
		Assert.That(
			global::MagicCore.Limited.SealedBuilder.Fixes("Delighted Halfling"),
			Nu.Not.Contains(ManaColor.Green)
		);

	[Test]
	public void DelightedHalfling_ItsColorlessIsTheUnrestrictedOne_WhateverTheOrder()
	{
		var printed = Lsv("Delighted Halfling");
		Field(printed with { Components = [.. printed.Components.Reverse()] }, me);
		var boon = Hand(Boon, me);
		TapAllMyLands();
		s = s.Act(new CastSpellAction { PlayerId = me, CardId = boon });
		Assert.That(
			s.GetCard(boon).GetComponent<OnStackComponent>()!.CantBeCountered,
			Nu.False,
			"{C} from the unrestricted ability: no rider"
		);
	}
}
