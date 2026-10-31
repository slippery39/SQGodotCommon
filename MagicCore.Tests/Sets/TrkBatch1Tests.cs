using System.Collections.Immutable;
using ImmutableGameObjects;
using MagicCore.Actions;
using MagicCore.Cards;
using MagicCore.Effects;
using MagicCore.Rules;
using MagicCore.Sets;
using MagicCore.Turns;

namespace MagicCore.Tests.Sets;

/// <summary>
/// MC-5.3 batch 1 (TRK commons and uncommons): the cards that needed a new engine piece (tier 3) or a
/// tested piece in a new combination (tier 2), each against its PRINTED text through real play.
/// Tier-1 cards are checked by their card-sheet entry (MagicCore/TrkCardSheet.md).
/// </summary>
public class TrkBatch1Tests : FraBoardTest
{
	static CardDefinition Trk(string name) => CardRegistry.Get(name, "TRK");

	void To(Step step, int active) =>
		s = TestGames.PassUntil(
			s,
			x => x.GetGame().Step == step && x.GetGame().ActivePlayerId == active
		);

	void Activate(ActivateAbilityAction action, Func<ChoiceAction, IEnumerable<int>>? answer = null)
	{
		s = s.Act(action);
		Resolve(answer);
	}

	// ===== Tier 3: a new engine piece =====

	[Test]
	public void TeaEarlGreyHot_IsChooseOne_AsItIsActivated()
	{
		var tea = Field(Trk("Tea, Earl Grey, Hot"), me);

		var offered = Activations(tea).ToList();
		Assert.That(
			offered.Select(a => a.Modes.Single()),
			Is.EquivalentTo(new[] { 0, 1 }),
			"one activation per mode, chosen as it is activated"
		);
		var gainLife = offered.Single(a => a.Modes.Single() == 1);
		Assert.That(
			s.TryAddAction(gainLife with { Modes = [] }).Success,
			Is.False,
			"no mode chosen"
		);
		Assert.That(
			s.TryAddAction(gainLife with { Modes = [0, 1] }).Success,
			Is.False,
			"choose ONE"
		);

		var theirs = Life(them);
		Activate(offered.Single(a => a.Modes.Single() == 0 && a.Targets.Single() == them));
		Assert.That(Life(them), Is.EqualTo(theirs - 2), "• deals 2 damage to target opponent");
		Assert.That(Alive(tea), Is.False, "sacrificed as the cost");
	}

	[Test]
	public void TeaEarlGreyHot_SecondMode_GainsThree()
	{
		var tea = Field(Trk("Tea, Earl Grey, Hot"), me);
		var mine = Life(me);
		Activate(Activations(tea).Single(a => a.Modes.Single() == 1));
		Assert.That(Life(me), Is.EqualTo(mine + 3), "• You gain 3 life");
	}

	[Test]
	public void Daniels_PutOnTheBottomFromTheGraveyard_GainsTwo_AndOnlyOnce()
	{
		int daniels;
		(s, daniels) = TestGames.Put(s, me, Trk("Daniels, Temporal Agent"), ZoneType.Graveyard);
		var mine = Life(me);

		s = s.Act(Activations(daniels).Single());
		Assert.That(
			s.CardsIn(me, ZoneType.Library).Last().Id,
			Is.EqualTo(daniels),
			"the cost puts it on the bottom of your library"
		);
		Assert.That(
			Activations(daniels),
			Is.Empty,
			"gone from the graveyard: no second activation"
		);

		Resolve();
		Assert.That(Life(me), Is.EqualTo(mine + 2));
	}

	[Test]
	public void CulberAndStamets_AddsTwoManaOfTheChosenColor()
	{
		var culber = Hand(Trk("Culber and Stamets, Star-Crossed"), me);
		s = s.Act(new CastSpellAction { PlayerId = me, CardId = culber });
		Resolve(choice =>
			choice switch
			{
				ChooseModesChoice => [1],
				ChooseColorChoice => [ChooseColorChoice.IdOf(ManaColor.Blue)],
				_ => choice.Options.Take(choice.MinChoices).Select(o => o.Id),
			}
		);
		Assert.That(s.GetPlayer(me).ManaPool[ManaColor.Blue], Is.EqualTo(2));
	}

	[Test]
	public void VGer_LooksAtTheirHand_ThenDraws()
	{
		var secret = Hand(TestCards.Fodder, them);
		var vger = Hand(Trk("V'Ger, the Intruder"), me);
		var cards = HandCount(me);
		Assert.That(s.GetCard(secret).KnownTo & Knowers.Opponent, Is.EqualTo(Knowers.None));

		CastWithTrigger(vger, [0], them);

		Assert.That(
			s.GetCard(secret).KnownTo & Knowers.Opponent,
			Is.EqualTo(Knowers.Opponent),
			"you (its owner's opponent) now know it"
		);
		Assert.That(
			HandCount(me),
			Is.EqualTo(cards - 1 + 1),
			"V'Ger left the hand, then draw a card"
		);
	}

	// ===== Tier 2: a tested piece, a new combination =====

	[Test]
	public void Sickbay_CountersAreOnePlusTheLifeYouGainedThisTurn()
	{
		Field(Trk("Sickbay"), me);
		var bear = Field(TestGames.Creature("Bear", "2/2"), me);
		var elf = Hand(TestGames.Creature("Elf", "1/1"), me);
		Cast(elf, []); // "Whenever a creature you control enters, you gain 1 life."

		To(Step.BeginCombat, me);
		Resolve(choice =>
			choice switch
			{
				ChooseTargetChoice => [bear],
				_ => choice.Options.Take(choice.MinChoices).Select(o => o.Id),
			}
		);
		Assert.That(s.GetCard(bear).PlusOneCounters, Is.EqualTo(1 + 1));
	}

	[Test]
	public void ACountWithPlus_NegatesWhole()
	{
		var onePlusThree = new Count(3) with { Plus = 1 };
		Assert.That(
			onePlusThree.Value(s, ImmutableDictionary<string, object>.Empty),
			Is.EqualTo(4)
		);
		Assert.That(
			onePlusThree.Negated.Value(s, ImmutableDictionary<string, object>.Empty),
			Is.EqualTo(-4),
			"-(3 + 1), not -3 + 1"
		);
	}

	[Test]
	public void RedAlert_CostsTwoLess_WhileACreatureIsAttackingYou()
	{
		var alert = Hand(Trk("Red Alert!"), me);
		Assert.That(Costs.Of(s, me, s.GetCard(alert)).ManaValue, Is.EqualTo(3));

		var raider = Field(TestGames.Creature("Raider", "2/2"), them);
		s = s.UpdateObject(raider, s.GetCard(raider) with { AttackingId = me }); // TEST SETUP: attacking me
		Assert.That(Costs.Of(s, me, s.GetCard(alert)).ManaValue, Is.EqualTo(1));

		s = s.UpdateObject(raider, s.GetCard(raider) with { AttackingId = them }); // attacking someone else
		Assert.That(Costs.Of(s, me, s.GetCard(alert)).ManaValue, Is.EqualTo(3));
	}

	[Test]
	public void Locutus_DiscardedFromHand_DealsTwo_AndGainsTwo()
	{
		var locutus = Hand(Trk("Locutus, the One Who Speaks"), me);
		var bear = Field(TestGames.Creature("Bear", "2/2"), them);
		var mine = Life(me);

		Activate(Activations(locutus).Single(a => a.Targets.Single() == bear));

		Assert.That(Alive(bear), Is.False, "2 damage from the discarded card");
		Assert.That(Life(me), Is.EqualTo(mine + 2));
		Assert.That(s.IsIn(locutus, me, ZoneType.Graveyard));
	}

	[Test]
	public void SpatialAnomaly_SacrificedItDealsTwo_AndMakesANewPlanet()
	{
		var anomaly = Field(Trk("Spatial Anomaly"), me);
		var bear = Field(TestGames.Creature("Bear", "2/2"), them);

		Activate(Activations(anomaly).Single(a => a.Targets.Single() == bear));

		Assert.That(Alive(bear), Is.False, "the sacrificed land still deals the damage");
		Assert.That(Alive(anomaly), Is.False);
		Assert.That(
			s.CardsIn(me, ZoneType.Battlefield).Single(c => c.Name == "New Planet").IsTapped
		);
	}

	[Test]
	public void MunitionsEnthusiast_WhenItDies_DealsOne()
	{
		var enthusiast = Field(Trk("Munitions Enthusiast"), me);
		var theirs = Life(them);
		var kill = Hand(TestCards.Kill(), me);
		s = s.Act(
			new CastSpellAction
			{
				PlayerId = me,
				CardId = kill,
				Targets = [enthusiast],
			}
		);
		Resolve(choice =>
			choice switch
			{
				ChooseTargetChoice => [them],
				_ => choice.Options.Take(choice.MinChoices).Select(o => o.Id),
			}
		);
		Assert.That(Life(them), Is.EqualTo(theirs - 1));
	}

	[Test]
	public void Khaaaan_DealsTwiceX_AndExilesWhatWouldDie()
	{
		var khan = Hand(Trk("Khaaaaaaaaaaaannn!"), me);
		var brute = Field(TestGames.Creature("Brute", "4/4"), them);
		s = s.Act(
			new CastSpellAction
			{
				PlayerId = me,
				CardId = khan,
				X = 2,
				Targets = [brute],
			}
		);
		Resolve();
		Assert.That(
			s.IsIn(brute, them, ZoneType.Exile),
			"twice 2 is lethal to a 4/4 — exiled, not dead"
		);
	}
}
