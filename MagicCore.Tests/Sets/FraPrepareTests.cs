using ImmutableGameObjects;
using MagicCore.Actions;
using MagicCore.Cards;
using MagicCore.Effects;
using MagicCore.Rules;
using MagicCore.Sets;
using MagicCore.Turns;

namespace MagicCore.Tests.Sets;

/// <summary>Prepare (Rules/Prepare.cs): the copy in exile, casting it, losing it — and each card's spell firing.</summary>
public class FraPrepareTests : FraBoardTest
{
	static IEnumerable<string> PrepareCards() =>
		CardRegistry.Scripted.Where(n =>
			CardRegistry.Get(n).Components.Any(c => c is PrepareSpellComponent)
		);

	/// <summary>TEST SETUP: enters through EnterBattlefield, so "enters prepared" applies.</summary>
	int Enter(string fra, int player = 0)
	{
		var id = Hand(fra, player == 0 ? me : player);
		s = s.EnterBattlefield(id, player == 0 ? me : player);
		return id;
	}

	Card? CopyOf(int creature) =>
		new[] { me, them }
			.SelectMany(p => s.CardsIn(p, ZoneType.Exile))
			.FirstOrDefault(c => c.GetComponent<PreparedCopyOf>()?.CreatureId == creature);

	void CastCopy(int creature, params int[] targets)
	{
		var copy = CopyOf(creature)!;
		s = s.Act(
			new CastSpellAction
			{
				PlayerId = me,
				CardId = copy.Id,
				Targets = [.. targets],
			}
		);
		Resolve();
	}

	bool AnyCopies() =>
		new[] { me, them }.Any(p => ZoneTypes.Any(z => s.CardsIn(p, z).Any(c => c.IsCopy)));

	static readonly ZoneType[] ZoneTypes =
	[
		ZoneType.Exile,
		ZoneType.Graveyard,
		ZoneType.Hand,
		ZoneType.Library,
		ZoneType.Battlefield,
	];

	[TestCaseSource(nameof(PrepareCards))]
	public void EveryPrepareCard_ItsCopyCasts_UnpreparesIt_AndLeavesNoCopy(string fra)
	{
		Field(TestGames.Creature("Theirs", "1/1"), them); // Unwind History's target
		var creature = Enter(fra);
		s = Prepare.Become(s, creature); // the upkeep and end-step ones: prepared by test setup
		Assert.That(s.GetCard(creature).IsPrepared);

		var cast = s.LegalActions()
			.OfType<CastSpellAction>()
			.FirstOrDefault(c => c.CardId == CopyOf(creature)?.Id);
		Assert.That(cast, Is.Not.Null, "its copy can be cast");
		s = s.Act(cast!);
		Resolve();

		Assert.That(
			(Alive(creature), s.GetCard(creature).IsPrepared, AnyCopies()),
			Is.EqualTo((true, false, false))
		);
	}

	[Test]
	public void SeedSuture_TheCopyResolves_TheCardStaysACreature()
	{
		var medic = Enter("Emergency Phytomedic // Seed Suture");
		var copy = CopyOf(medic);
		Assert.That(
			(copy!.Name, copy.Is(CardType.Sorcery), s.IsIn(copy.Id, me, ZoneType.Exile)),
			Is.EqualTo(("Seed Suture", true, true)),
			"a copy of the spell, in exile"
		);
		var life = Life(me);

		CastCopy(medic, medic);

		Assert.That((s.GetCard(medic).PlusOneCounters, Life(me)), Is.EqualTo((1, life + 1)));
		Assert.That(s.IsOnBattlefield(medic) && !s.GetCard(medic).IsPrepared);
	}

	[Test]
	public void TheCopy_KeepsItsSpellsTiming()
	{
		var medic = Enter("Emergency Phytomedic // Seed Suture");
		var recollector = Enter("Bloodline Recollector // Ancestral Craving");
		s = Prepare.Become(s, recollector);
		s = s.Act(new PassPriorityAction { PlayerId = me });
		s = s.Act(new PassPriorityAction { PlayerId = them }); // beginning of combat

		var casts = s.LegalActions().OfType<CastSpellAction>().Select(c => c.CardId).ToList();
		Assert.That(casts, Does.Not.Contain(CopyOf(medic)!.Id), "Seed Suture is a sorcery");
		Assert.That(
			casts,
			Does.Contain(CopyOf(recollector)!.Id),
			"Ancestral Craving is an instant"
		);
	}

	[Test]
	public void LeavingTheBattlefield_TheCopyCeasesToExist()
	{
		var medic = Enter("Emergency Phytomedic // Seed Suture");
		s = s.Sacrifice(medic);
		Resolve();
		s = s.Act(new PassPriorityAction { PlayerId = me }); // a state-based check
		Assert.That(AnyCopies(), Is.False);
	}

	[Test]
	public void ACastOfACopyThatCeasedToExist_IsRefused_NotACrash()
	{
		var medic = Enter("Emergency Phytomedic // Seed Suture");
		var cast = new CastSpellAction
		{
			PlayerId = me,
			CardId = CopyOf(medic)!.Id,
			Targets = [medic],
		};
		s = s.Sacrifice(medic);
		s = s.Act(new PassPriorityAction { PlayerId = me }); // the state check removes the copy
		s = s.Act(new PassPriorityAction { PlayerId = them }); // back to me: past the priority check

		Assert.That(s.HasObject(cast.CardId), Is.False);
		Assert.That(s.GetGame().PriorityPlayerId, Is.EqualTo(me));
		Assert.That(cast.ValidateAdd(s).IsValid, Is.False);
	}

	[Test]
	public void AlreadyPrepared_BecomingPreparedAgainDoesNothing()
	{
		var medic = Enter("Emergency Phytomedic // Seed Suture");
		s = Prepare.Become(s, medic);
		Assert.That(s.CardsIn(me, ZoneType.Exile).Count(c => c.IsCopy), Is.EqualTo(1));
	}

	[Test]
	public void OnlyItsController_CanCastTheCopy()
	{
		var medic = Enter("Emergency Phytomedic // Seed Suture");
		s = s.Act(new PassPriorityAction { PlayerId = me });
		Assert.That(
			s.LegalActions().OfType<CastSpellAction>().Any(c => c.CardId == CopyOf(medic)!.Id),
			Is.False
		);
	}

	[Test]
	public void StingerquillVoxmancer_PreparesAtYourUpkeep_IfNotPrepared()
	{
		var voxmancer = Enter("Stingerquill Voxmancer // Vicious Verse");
		Assert.That(s.GetCard(voxmancer).IsPrepared, Is.False, "doesn't enter prepared");

		s = TestGames.PassUntil(
			s,
			x => x.GetGame().Step == Step.PreCombatMain && x.GetGame().TurnNumber == 3
		);
		Assert.That(s.GetCard(voxmancer).IsPrepared);

		var life = Life(them);
		CastCopy(voxmancer, them);
		Assert.That(Life(them), Is.EqualTo(life - 1), "Vicious Verse");
	}

	[Test]
	public void BloodlineRecollector_PreparesAtAnEndStepAfterThreeDeaths_AncestralCraving()
	{
		var recollector = Enter("Bloodline Recollector // Ancestral Craving");
		foreach (var name in new[] { "A", "B", "C" })
		{
			var victim = Field(TestGames.Creature(name, "1/1"), them);
			s = s.Sacrifice(victim);
		}
		s = TestGames.PassUntil(s, x => x.GetGame().Step == Step.End);
		Resolve();
		Assert.That(s.GetCard(recollector).IsPrepared);

		var (hand, life) = (HandCount(them), Life(them));
		CastCopy(recollector, them);
		Assert.That((HandCount(them), Life(them)), Is.EqualTo((hand + 3, life - 3)));
	}

	[Test]
	public void DivinerOfVictory_UnwindHistoryBouncesACheapCreatureAndSurveils_WhichPumpsIt()
	{
		var diviner = Enter("Diviner of Victory // Unwind History");
		var cheap = Field(TestGames.CreatureCosting("{1}{G}", "Cheap", "2/2"), them);
		var dear = Field(TestGames.CreatureCosting("{3}{G}", "Dear", "4/4"), them);
		Assert.That(
			s.LegalActions()
				.OfType<CastSpellAction>()
				.Where(c => c.CardId == CopyOf(diviner)!.Id)
				.Select(c => c.Targets[0]),
			Is.EquivalentTo(new[] { cheap }),
			"an opponent's, mana value 3 or less"
		);

		CastCopy(diviner, cheap);

		Assert.That(s.IsIn(cheap, them, ZoneType.Hand) && Alive(dear));
		Assert.That(s.Power(s.GetCard(diviner)), Is.EqualTo(2), "it surveilled: +1/+1");
	}

	[Test]
	public void SemesterForeseer_PeerReviewMakesACadetAndSurveils()
	{
		var foreseer = Hand("Semester Foreseer // Peer Review", me);
		Cast(foreseer, []); // cast for real: its entering surveil resolves here
		var surveils = s.GetPlayer(me).ThisTurn.Surveils;
		Assert.That((surveils, s.GetCard(foreseer).IsPrepared), Is.EqualTo((1, true)));

		CastCopy(foreseer);

		Assert.That(
			s.CardsIn(me, ZoneType.Battlefield).Count(c => c.Name == "Cadet"),
			Is.EqualTo(1)
		);
		Assert.That(s.GetPlayer(me).ThisTurn.Surveils, Is.EqualTo(surveils + 1));
	}

	[Test]
	public void VariableChaser_ArcOfFortune_EachPlayerChooses()
	{
		var chaser = Enter("Variable Chaser // Arc of Fortune");
		var myOld = s.CardsIn(me, ZoneType.Hand).Select(c => c.Id).ToList();
		var theirs = s.CardsIn(them, ZoneType.Hand).Select(c => c.Id).ToList();

		s = s.Act(new CastSpellAction { PlayerId = me, CardId = CopyOf(chaser)!.Id });
		Resolve(choice =>
			choice switch
			{
				YesNoChoice yes when yes.PlayerId == me => [1],
				_ => [],
			}
		);

		Assert.That(HandCount(me), Is.EqualTo(7));
		Assert.That(myOld.All(id => s.IsIn(id, me, ZoneType.Graveyard)), "discarded");
		Assert.That(
			s.CardsIn(them, ZoneType.Hand).Select(c => c.Id),
			Is.EquivalentTo(theirs),
			"they declined"
		);
	}

	[Test]
	public void VoidExtrapolator_OmitVariablesMills_ThresholdGrowsIt()
	{
		var extrapolator = Enter("Void Extrapolator // Omit Variables");
		for (var i = 0; i < 4; i++)
			(s, _) = TestGames.Put(s, me, TestGames.Card("Plains"), ZoneType.Graveyard);
		Assert.That(s.Power(s.GetCard(extrapolator)), Is.EqualTo(2), "4 cards: no threshold");

		CastCopy(extrapolator);

		Assert.That(s.CardsIn(me, ZoneType.Graveyard).Count(), Is.EqualTo(7), "milled three");
		Assert.That(
			(s.Power(s.GetCard(extrapolator)), s.Toughness(s.GetCard(extrapolator))),
			Is.EqualTo((3, 3))
		);
	}

	[Test]
	public void TheorixMetamage_ThresholdGivesPowerAndFlying()
	{
		var metamage = Enter("Theorix Metamage // Omit Variables");
		Assert.That(s.Has(s.GetCard(metamage), "Flying"), Is.False);
		for (var i = 0; i < 7; i++)
			(s, _) = TestGames.Put(s, me, TestGames.Card("Plains"), ZoneType.Graveyard);
		Assert.That(
			(s.Power(s.GetCard(metamage)), s.Has(s.GetCard(metamage), "Flying")),
			Is.EqualTo((3, true))
		);
	}

	[Test]
	public void PompousBattlemage_ImprovisedAct_DiscardToDraw()
	{
		var battlemage = Enter("Pompous Battlemage // Improvised Act");
		var fodder = Hand(TestGames.Card("Forest"), me);
		var hand = HandCount(me);

		s = s.Act(new CastSpellAction { PlayerId = me, CardId = CopyOf(battlemage)!.Id });
		Resolve(choice =>
			choice switch
			{
				ChooseCardsFromHandChoice => [fodder],
				_ => [],
			}
		);

		Assert.That(s.IsIn(fodder, me, ZoneType.Graveyard));
		Assert.That(HandCount(me), Is.EqualTo(hand), "one out, one in");
	}

	[Test]
	public void CarnivorousCultivator_EnrootPutsALandInTheGraveyard_CombatDamageReturnsIt()
	{
		var cultivator = Enter("Carnivorous Cultivator // Enroot");
		var forest = s.CardsIn(me, ZoneType.Library).First(c => c.Is(CardType.Land)).Id;

		s = s.Act(new CastSpellAction { PlayerId = me, CardId = CopyOf(cultivator)!.Id });
		Resolve(choice =>
			choice switch
			{
				ChooseFromLibraryChoice => [forest],
				_ => [],
			}
		);
		Assert.That(s.IsIn(forest, me, ZoneType.Graveyard));

		s = s.UpdateObject(cultivator, s.GetCard(cultivator) with { ControlledSinceTurn = 0 }); // TEST SETUP: not sick
		s = TestGames.PassUntil(s, x => x.IsWaitingForChoice);
		s = s.Choose([them]);
		s = TestGames.PassUntil(s, x => x.GetPendingChoice() is ChooseTargetChoice);
		s = s.Choose([forest]);
		Resolve();
		Assert.That(s.IsIn(forest, me, ZoneType.Hand));
	}

	[Test]
	public void KonstrariImproviser_SoulTetherMakesAHeartwoodForRedOrGreen()
	{
		var improviser = Enter("Konstrari Improviser // Soul Tether");
		CastCopy(improviser);
		var heartwood = s.CardsIn(me, ZoneType.Battlefield).Single(c => c.Name == "Heartwood");
		Assert.That(
			ManaAbilities.ColorsOf(s, heartwood),
			Is.EquivalentTo(new[] { ManaColor.Red, ManaColor.Green })
		);
		Assert.That((heartwood.IsToken, heartwood.Is(CardType.Artifact)), Is.EqualTo((true, true)));
	}

	[Test]
	public void ParadoxShaper_PutsAGraveyardCardOnTheBottom()
	{
		var shaper = Enter("Paradox Shaper // Omit Variables");
		int card;
		(s, card) = TestGames.Put(s, me, TestGames.Card("Plains"), ZoneType.Graveyard);

		s = s.Act(
			new ActivateAbilityAction
			{
				PlayerId = me,
				CardId = shaper,
				Targets = [card],
			}
		);
		Resolve();

		Assert.That(s.CardsIn(me, ZoneType.Library).Last().Id, Is.EqualTo(card));
	}

	[Test]
	public void PrudentFateseer_SurveilingPumpsYourCreatures_OnceATurn()
	{
		var fateseer = Enter("Prudent Fateseer // Peer Review");
		var power = s.Power(s.GetCard(fateseer));
		var surveil = Hand(TestGames.Inline("Peek", "{U}", "Instant", null, new Surveil(1)), me);
		Cast(surveil, []);
		var second = Hand(TestGames.Inline("Peek2", "{U}", "Instant", null, new Surveil(1)), me);
		Cast(second, []);
		Assert.That(s.Power(s.GetCard(fateseer)), Is.EqualTo(power + 1));
	}

	[Test]
	public void VigorbloomVanguard_YourCreaturesWithACounterHaveVigilance()
	{
		Enter("Vigorbloom Vanguard // Seed Suture");
		var bear = Field(TestGames.Creature("Bear", "2/2"), me);
		Assert.That(s.Has(s.GetCard(bear), "Vigilance"), Is.False);
		s = s.UpdateObject(bear, s.GetCard(bear) with { PlusOneCounters = 1 }); // TEST SETUP
		Assert.That(s.Has(s.GetCard(bear), "Vigilance"));
	}

	[Test]
	public void WhiplashWordsmith_FliesAndHastesOnceAnOpponentTookNoncombatDamage()
	{
		var wordsmith = Enter("Whiplash Wordsmith // Vicious Verse");
		Assert.That(Combat.CanAttack(s, s.GetCard(wordsmith)), Is.False, "summoning sick");

		CastCopy(wordsmith, them); // Vicious Verse: 1 noncombat damage

		Assert.That(
			(s.Has(s.GetCard(wordsmith), "Flying"), Combat.CanAttack(s, s.GetCard(wordsmith))),
			Is.EqualTo((true, true))
		);
	}
}
