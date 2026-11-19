using ImmutableGameObjects;
using MagicCore.Actions;
using MagicCore.Cards;
using MagicCore.Effects;
using Nu = NUnit.Framework.Is;

namespace MagicCore.Tests.Sets;

/// <summary>
/// MC-4.4h2a, LSVPower — black's hand options: YouChooseTheyDiscard's Filter (Inquisition, Duress) and exile until
/// the source leaves (Deep-Cavern Bat), escalate (Collective Brutality), a copy by the target (Chain of Smog).
/// </summary>
public class LsvBatch2h2aTests : FraBoardTest
{
	static CardDefinition Bear => TestGames.Creature("Bear", "2/2");

	/// <summary>A {3} sorcery: mana value 3.</summary>
	static CardDefinition Dear => TestGames.Inline("Dear", "{3}", "Sorcery");

	static CardDefinition Land => TestGames.Card("Swamp");

	List<ChoiceAction> CastAt(int card, int target, Func<ChoiceAction, IEnumerable<int>> answer)
	{
		s = s.Act(
			new CastSpellAction
			{
				PlayerId = me,
				CardId = card,
				Targets = [target],
			}
		);
		return ResolveAsking(answer);
	}

	[Test]
	public void InquisitionOfKozilek_NonlandManaValueThreeOrLess()
	{
		var three = Hand(Dear, them);
		var four = Hand(TestGames.Inline("Four", "{4}", "Sorcery"), them);
		var land = Hand(Land, them);
		var asked = CastAt(
			Hand(Lsv("Inquisition of Kozilek"), me),
			them,
			On<ChooseCardsFromHandChoice>(three)
		);
		var offered = Offered<ChooseCardsFromHandChoice>(asked).ToList();
		Assert.That(offered, Nu.Not.Contains(four), "mana value 3 or less");
		Assert.That(offered, Nu.Not.Contains(land), "nonland");
		Assert.That(InGraveyard(three));
	}

	[Test]
	public void Duress_NoncreatureNonland_AnArtifactCreatureIsACreature()
	{
		var sorcery = Hand(Dear, them);
		var golem = Hand(TestGames.Inline("Golem", "{2}", "Artifact Creature — Golem"), them);
		var bear = Hand(Bear, them);
		var land = Hand(Land, them);
		var asked = CastAt(Hand(Lsv("Duress"), me), them, On<ChooseCardsFromHandChoice>(sorcery));
		var offered = Offered<ChooseCardsFromHandChoice>(asked).ToList();
		Assert.That(offered, Has.Member(sorcery));
		Assert.That(offered, Nu.Not.Contains(golem), "an artifact creature is a creature");
		Assert.That(offered, Nu.Not.Contains(bear).And.Not.Contains(land));
		Assert.That(InGraveyard(sorcery));
	}

	[Test]
	public void DeepCavernBat_ExilesFromTheirHand_BackWhenItLeaves()
	{
		var spell = Hand(Dear, them);
		var land = Hand(Land, them);
		var bat = Hand(Lsv("Deep-Cavern Bat"), me);
		var asked = CastAsking(bat, On<ChooseCardsFromHandChoice>(spell));
		Assert.That(Offered<ChooseCardsFromHandChoice>(asked), Nu.Not.Contains(land), "nonland");
		Assert.That(s.IsIn(spell, them, ZoneType.Exile));
		Cast(Hand(TestCards.Kill(), me), [], bat);
		Assert.That(s.IsIn(spell, them, ZoneType.Hand), "back to its owner's hand");
	}

	[Test]
	public void DeepCavernBat_MayExileNothing()
	{
		var spell = Hand(Dear, them);
		CastAsking(Hand(Lsv("Deep-Cavern Bat"), me), On<ChooseCardsFromHandChoice>());
		Assert.That(s.IsIn(spell, them, ZoneType.Hand));
	}

	[Test]
	public void CollectiveBrutality_EscalatedTwice_DiscardsTwo_AllThreeModes()
	{
		var instant = Hand(TestGames.Inline("Zap", "{1}", "Instant"), them);
		var bear = Field(Bear, them);
		var mine = new[] { Hand(TestCards.Fodder, me), Hand(TestCards.Fodder, me) };
		var brutality = Hand(Lsv("Collective Brutality"), me);
		var (life, theirs) = (Life(me), Life(them));
		s = s.Act(
			new CastSpellAction
			{
				PlayerId = me,
				CardId = brutality,
				Modes = [0, 1, 2],
				Targets = [them, bear, them],
			}
		);
		Resolve(c =>
			c switch
			{
				ChooseCardsFromHandChoice h when h.PlayerId == me => mine,
				ChooseCardsFromHandChoice => [instant],
				_ => c.Options.Take(c.MinChoices).Select(o => o.Id),
			}
		);
		Assert.That(mine.All(InGraveyard), "escalate: a discard for each mode beyond the first");
		Assert.That(InGraveyard(instant));
		Assert.That(Alive(bear), Nu.False, "-2/-2");
		Assert.That((Life(me), Life(them)), Nu.EqualTo((life + 2, theirs - 2)));
	}

	[Test]
	public void CollectiveBrutality_OneCardInHand_AtMostTwoModes()
	{
		Field(Bear, them); // the -2/-2 mode's target
		foreach (var card in s.CardsIn(me, ZoneType.Hand).ToList()) // TEST SETUP: the opening hand out
			s = s.MoveCard(card.Id, s.ZoneId(me, ZoneType.Graveyard));
		Hand(TestCards.Fodder, me);
		var casts = Casts(Hand(Lsv("Collective Brutality"), me)).ToList();
		Assert.That(casts.Any(c => c.Modes.Length == 2), "one mode beyond the first");
		Assert.That(casts.Any(c => c.Modes.Length == 3), Nu.False, "two discards needed");
	}

	[Test]
	public void ChainOfSmog_TheyDiscardTwo_ThenMayCopyItAtMe()
	{
		for (var i = 0; i < 3; i++)
			Hand(TestCards.Fodder, them);
		Hand(TestCards.Fodder, me);
		Hand(TestCards.Fodder, me);
		var smog = Hand(Lsv("Chain of Smog"), me);
		var (mine, theirs) = (HandCount(me) - 1, HandCount(them));
		var asked = CastAt(
			smog,
			them,
			c =>
				c switch
				{
					YesNoChoice y when y.PlayerId == them => [1],
					ChooseTargetChoice => [me],
					_ => c.Options.Take(c.MinChoices).Select(o => o.Id),
				}
		);
		Assert.That(HandCount(them), Nu.EqualTo(theirs - 2));
		Assert.That(HandCount(me), Nu.EqualTo(mine - 2), "their copy, at me");
		Assert.That(asked.OfType<YesNoChoice>().Count(), Nu.EqualTo(2), "then I may copy theirs");
		Assert.That(InGraveyard(smog));
	}
}
