using ImmutableGameObjects;
using MagicCore.Actions;
using MagicCore.Cards;
using MagicCore.Rules;
using MagicCore.Sets.Fra;

namespace MagicCore.Tests.Sets;

/// <summary>
/// Every scripted FRA card's ability FIRES — tested by its consequence, through real casting and
/// the real stack, never by inspecting the script. An inert card throws no error.
/// </summary>
public class FraCardTests
{
	GameState s = null!;
	int me,
		them;

	/// <summary>The starting player's first main phase; both players have two of each basic land.</summary>
	[SetUp]
	public void Board()
	{
		s = TestGames.InFirstMain();
		me = s.GetGame().StartingPlayerId;
		them = s.OpponentOf(me);
		foreach (var player in new[] { me, them })
		foreach (var land in new[] { "Plains", "Island", "Swamp", "Mountain", "Forest" })
		{
			Field(TestGames.Card(land), player);
			Field(TestGames.Card(land), player);
		}
	}

	int Field(CardDefinition card, int player)
	{
		int id;
		(s, id) = TestGames.Put(s, player, card, ZoneType.Battlefield);
		return id;
	}

	int Field(string fra, int player) => Field(FraCards.Get(fra), player);

	/// <summary>
	/// Replaces <c>s</c> — call it on its own line. Inside <c>s.Act(new … { CardId = Hand(…) })</c>
	/// C# reads <c>s</c> first, so the action runs on the state before the card existed.
	/// </summary>
	int Hand(CardDefinition card, int player)
	{
		int id;
		(s, id) = TestGames.Put(s, player, card, ZoneType.Hand);
		return id;
	}

	int Hand(string fra, int player) => Hand(FraCards.Get(fra), player);

	/// <summary>Casts as the player holding priority, then resolves the whole stack.</summary>
	void Cast(int cardId, params int[] targets)
	{
		var player = s.GetCard(cardId).OwnerId;
		s = s.Act(
			new CastSpellAction
			{
				PlayerId = player,
				CardId = cardId,
				Targets = [.. targets],
			}
		);
		Resolve();
	}

	/// <summary>Passes until the stack is empty, answering each choice with <paramref name="answer"/> (default: the first options).</summary>
	void Resolve(Func<ChoiceAction, IEnumerable<int>>? answer = null)
	{
		for (var i = 0; i < 100; i++)
		{
			if (s.GetPendingChoice() is { } choice)
				s = s.Choose(
					answer?.Invoke(choice)
						?? choice.Options.Take(choice.MinChoices).Select(o => o.Id)
				);
			else if (!s.StackIsEmpty())
				s = s.Act(new PassPriorityAction { PlayerId = s.GetGame().PriorityPlayerId });
			else
				return;
		}
	}

	bool OnBattlefield(int card) => s.IsOnBattlefield(card);

	int HandCount(int player) => s.CardsIn(player, ZoneType.Hand).Count();

	int Life(int player) => s.GetPlayer(player).Life;

	void ToNextTurn() =>
		s = TestGames.PassUntil(
			s,
			x => x.GetGame().Step == Step.Upkeep && x.GetGame().ActivePlayerId == them
		);

	// ===== White =====

	[Test]
	public void ProphesiedEnd_DestroysANonAttacker_AndItsControllerDraws()
	{
		var bear = Field(TestGames.Creature("Bear", "2/2"), them);
		var hand = HandCount(them);

		Cast(Hand("Prophesied End", me), bear);

		Assert.That(s.IsIn(bear, them, ZoneType.Graveyard));
		Assert.That(HandCount(them), Is.EqualTo(hand + 1));
	}

	[Test]
	public void ProphesiedEnd_AnAttackerDiesWithNoDraw()
	{
		var bear = Field(TestGames.Creature("Bear", "2/2"), me);
		var end = Hand("Prophesied End", me);
		s = TestGames.PassUntil(s, x => x.IsWaitingForChoice);
		s = s.Choose([them]);
		var hand = HandCount(me);

		Cast(end, bear);

		Assert.That(s.IsIn(bear, me, ZoneType.Graveyard));
		Assert.That(HandCount(me), Is.EqualTo(hand - 1), "only the spell left the hand");
	}

	[Test]
	public void Thalia_TaxesOnlyHerOpponentsNoncreatureSpells()
	{
		Field("Thalia, the Survivor", them);
		var gaspId = Hand("Last Gasp", me);
		var bearId = Hand(TestGames.Creature("Bear", "2/2"), me);
		var theirGaspId = Hand("Last Gasp", them);
		var (gasp, bear, theirGasp) = (
			s.GetCard(gaspId),
			s.GetCard(bearId),
			s.GetCard(theirGaspId)
		);

		Assert.That(Costs.Of(s, me, gasp).ManaValue, Is.EqualTo(gasp.ManaCost.ManaValue + 1));
		Assert.That(Costs.Of(s, me, bear).ManaValue, Is.EqualTo(bear.ManaCost.ManaValue));
		Assert.That(
			Costs.Of(s, them, theirGasp).ManaValue,
			Is.EqualTo(theirGasp.ManaCost.ManaValue)
		);
	}

	// ===== Black =====

	[Test]
	public void LastGasp_ShrinksUntilEndOfTurn_AndKillsWhatItShrinksToZero()
	{
		var small = Field(TestGames.Creature("Small", "3/3"), them);
		var big = Field(TestGames.Creature("Big", "3/4"), them);

		Cast(Hand("Last Gasp", me), small);
		Cast(Hand("Last Gasp", me), big);

		Assert.That(OnBattlefield(small), Is.False);
		Assert.That(s.Toughness(s.GetCard(big)), Is.EqualTo(1));
		ToNextTurn();
		Assert.That(s.Toughness(s.GetCard(big)), Is.EqualTo(4), "the effect ended in cleanup");
	}

	[Test]
	public void TerminalCriticism_OnlyBlueOrRed_AndGainsLife()
	{
		var red = Field("Heartstring Puller", them);
		var green = Field("Greenhouse Propagator", them);
		var criticism = Hand("Terminal Criticism", me);
		var life = Life(me);

		var legalTargets = s.LegalActions()
			.OfType<CastSpellAction>()
			.Where(a => a.CardId == criticism)
			.Select(a => a.Targets[0]);
		Assert.That(legalTargets, Does.Contain(red).And.Not.Contain(green));

		Cast(criticism, red);
		Assert.That(OnBattlefield(red), Is.False);
		Assert.That(Life(me), Is.EqualTo(life + 1));
	}

	[Test]
	public void ExtendedAbsence_ExilesAndDrains()
	{
		var bear = Field(TestGames.Creature("Bear", "2/2"), them);
		var (mine, theirs) = (Life(me), Life(them));

		Cast(Hand("Extended Absence", me), bear);

		Assert.That(s.IsIn(bear, them, ZoneType.Exile));
		Assert.That((Life(me), Life(them)), Is.EqualTo((mine + 1, theirs - 1)));
	}

	[Test]
	public void ExtendedAbsence_WhoseTargetIsGone_DoesNothingAtAll()
	{
		var bear = Field(TestGames.Creature("Bear", "2/2"), them);
		var absence = Hand("Extended Absence", me);
		var (mine, theirs) = (Life(me), Life(them));
		s = s.Act(
			new CastSpellAction
			{
				PlayerId = me,
				CardId = absence,
				Targets = [bear],
			}
		);

		s = s.MoveCard(bear, s.ZoneId(them, ZoneType.Hand)); // TEST SETUP: the target leaves in response
		Resolve();

		Assert.That((Life(me), Life(them)), Is.EqualTo((mine, theirs)), "fizzled: no drain either");
		Assert.That(s.IsIn(absence, me, ZoneType.Graveyard));
	}

	[Test]
	public void CastAwayDoubt_DrawsTwo_HitsBothPlayers()
	{
		var hand = HandCount(me);
		var (mine, theirs) = (Life(me), Life(them));

		Cast(Hand("Cast Away Doubt", me));

		Assert.That(HandCount(me), Is.EqualTo(hand + 2));
		Assert.That((Life(me), Life(them)), Is.EqualTo((mine - 2, theirs - 2)));
	}

	[Test]
	public void RankRat_TheOpponentDiscards_TheirChoice()
	{
		var hand = HandCount(them);
		var deciders = new List<int>();

		var rat = Hand("Rank Rat", me);
		s = s.Act(new CastSpellAction { PlayerId = me, CardId = rat });
		Resolve(choice =>
		{
			deciders.Add(s.DecidingPlayerId());
			return choice.Options.Take(choice.MinChoices).Select(o => o.Id);
		});

		Assert.That(HandCount(them), Is.EqualTo(hand - 1));
		Assert.That(deciders, Is.EqualTo(new[] { them }));
	}

	[Test]
	public void RampartHunter_PumpsAndGrantsDeathtouch_UntilEndOfTurn()
	{
		var bear = Field(TestGames.Creature("Bear", "2/2"), me);

		var hunter = Hand("Rampart Hunter", me);
		s = s.Act(new CastSpellAction { PlayerId = me, CardId = hunter });
		Resolve(choice => [bear]);

		Assert.That((s.Power(s.GetCard(bear)), s.Toughness(s.GetCard(bear))), Is.EqualTo((4, 4)));
		Assert.That(s.Has(s.GetCard(bear), "Deathtouch"));
		ToNextTurn();
		Assert.That(s.Power(s.GetCard(bear)), Is.EqualTo(2));
		Assert.That(s.Has(s.GetCard(bear), "Deathtouch"), Is.False);
	}

	[Test]
	public void ScreechingSoulbreaker_AttackingDrainsOne()
	{
		var bird = Field("Screeching Soulbreaker", me);
		var (mine, theirs) = (Life(me), Life(them));

		s = TestGames.PassUntil(s, x => x.IsWaitingForChoice);
		s = s.Choose([them]);
		Resolve();

		Assert.That(s.GetCard(bird).AttackingId, Is.EqualTo(them));
		Assert.That(
			(Life(me), Life(them)),
			Is.EqualTo((mine + 1, theirs - 1)),
			"before combat damage"
		);
	}

	[Test]
	public void LegendRule_KeepsTheNewestYargle()
	{
		var old = Field("Yargle, Glutton of Urborg", me);
		var fresh = Hand("Yargle, Glutton of Urborg", me);

		Cast(fresh);

		Assert.That(OnBattlefield(fresh));
		Assert.That(s.IsIn(old, me, ZoneType.Graveyard));
	}

	// ===== Red =====

	[Test]
	public void WrathOfTheBloodmane_DealsFour_CheaperWithALegend()
	{
		var wrath = Hand("Wrath of the Bloodmane", me);
		var printed = s.GetCard(wrath).ManaCost.ManaValue;
		Assert.That(Costs.Of(s, me, s.GetCard(wrath)).ManaValue, Is.EqualTo(printed));
		Field("Yargle, Glutton of Urborg", me);
		Assert.That(Costs.Of(s, me, s.GetCard(wrath)).ManaValue, Is.EqualTo(printed - 1));

		var four = Field(TestGames.Creature("Four", "4/4"), them);
		var five = Field(TestGames.Creature("Five", "4/5"), them);
		Cast(wrath, four);
		Cast(Hand("Wrath of the Bloodmane", me), five);

		Assert.That(OnBattlefield(four), Is.False);
		Assert.That(s.GetCard(five).Damage, Is.EqualTo(4));
	}

	[Test]
	public void ArtifistAcumen_TeamFirstStrike_AndDraws()
	{
		var a = Field(TestGames.Creature("A", "2/2"), me);
		var b = Field(TestGames.Creature("B", "2/2"), me);
		var theirs = Field(TestGames.Creature("Theirs", "2/2"), them);
		var hand = HandCount(me);

		Cast(Hand("Artifist Acumen", me));

		Assert.That(s.Has(s.GetCard(a), "First strike") && s.Has(s.GetCard(b), "First strike"));
		Assert.That(s.Has(s.GetCard(theirs), "First strike"), Is.False);
		Assert.That(
			HandCount(me),
			Is.EqualTo(hand + 1),
			"the Acumen was put in hand and cast; one card drawn"
		);
	}

	[Test]
	public void HeartstringPuller_MakesACadet_ThatCeasesToExistWhenItDies()
	{
		Cast(Hand("Heartstring Puller", me));
		var cadet = s.CardsIn(me, ZoneType.Battlefield).Single(c => c.IsToken);
		Assert.That((cadet.Name, s.Power(cadet), s.Toughness(cadet)), Is.EqualTo(("Cadet", 2, 2)));

		Cast(Hand("Last Gasp", me), cadet.Id);

		Assert.That(s.HasObject(cadet.Id), Is.False);
	}

	[Test]
	public void ChandrasEmberling_GrowsOnNoncreatureSpellsOnly()
	{
		var emberling = Field("Chandra's Emberling", me);

		Cast(Hand("Last Gasp", me), Field(TestGames.Creature("Bear", "2/2"), them));
		Cast(Hand(TestGames.Creature("Ally", "1/1"), me));

		Assert.That(s.GetCard(emberling).PlusOneCounters, Is.EqualTo(1));
	}

	[Test]
	public void Arni_GetsTheEnteringCreaturesPower()
	{
		var arni = Field("Arni, Renowned Champion", me);
		var printed = s.Power(s.GetCard(arni));

		Cast(Hand(TestGames.Creature("Brute", "3/3"), me));

		Assert.That(s.Power(s.GetCard(arni)), Is.EqualTo(printed + 3));
	}

	[Test]
	public void Koth_LandfallPings_AndAMountainAddsRed()
	{
		Field("Koth, the Geomancer", me);
		var theirs = Life(them);

		var mountain = Hand(TestGames.Card("Mountain"), me);
		s = s.Act(new PlayLandAction { PlayerId = me, CardId = mountain });
		Resolve();

		Assert.That(Life(them), Is.EqualTo(theirs - 1));
		Assert.That(s.GetPlayer(me).ManaPool.Red, Is.EqualTo(1));
	}

	// ===== Green =====

	[Test]
	public void TethermagesAdvantage_PumpsGrantsReachAndUntaps()
	{
		var bear = Field(TestGames.Creature("Bear", "2/2"), me);
		s = s.UpdateObject(bear, s.GetCard(bear) with { IsTapped = true }); // TEST SETUP

		Cast(Hand("Tethermage's Advantage", me), bear);

		var card = s.GetCard(bear);
		Assert.That(
			(s.Power(card), s.Toughness(card), s.Has(card, "Reach"), card.IsTapped),
			Is.EqualTo((4, 4, true, false))
		);
	}

	[Test]
	public void Yoshimaru_EntersAndFights_ThenCountersOnlyNonlegendary()
	{
		var bear = Field(TestGames.Creature("Bear", "3/3"), me);
		var victim = Field(TestGames.Creature("Victim", "2/2"), them);
		var yoshimaru = Hand("Yoshimaru, Scrappy Stray", me);

		s = s.Act(new CastSpellAction { PlayerId = me, CardId = yoshimaru });
		Resolve(choice => [((ChooseTargetChoice)choice).Index == 0 ? bear : victim]);

		Assert.That(OnBattlefield(victim), Is.False);
		Assert.That(s.GetCard(bear).Damage, Is.EqualTo(2));

		var counterTargets = s.LegalActions()
			.OfType<ActivateAbilityAction>()
			.Select(a => a.Targets[0])
			.ToList();
		Assert.That(counterTargets, Does.Contain(bear).And.Not.Contain(yoshimaru));
		s = s.Act(
			s.LegalActions().OfType<ActivateAbilityAction>().First(a => a.Targets[0] == bear)
		);
		Resolve();
		Assert.That(s.GetCard(bear).PlusOneCounters, Is.EqualTo(1));
	}

	[Test]
	public void GreenhousePropagator_GainsOnAnotherCreature_AndTapsForGreenOnceSettled()
	{
		var settled = Field("Greenhouse Propagator", me);
		var life = Life(me);
		var fresh = Hand("Greenhouse Propagator", me);

		Cast(fresh);

		Assert.That(Life(me), Is.EqualTo(life + 1), "the settled one saw the new one enter");
		Assert.That(ManaAbilities.CanTap(s, s.GetCard(settled)));
		Assert.That(ManaAbilities.CanTap(s, s.GetCard(fresh)), Is.False, "summoning sick");
	}

	// ===== Empower Jace and planeswalkers =====

	Card? Jace(int player) =>
		s.CardsIn(player, ZoneType.Battlefield).FirstOrDefault(EmpowerJace.IsJaceToken);

	/// <summary>Activates the loyalty ability with this cost on <paramref name="walker"/> and resolves it.</summary>
	void Loyalty(
		int walker,
		int cost,
		Func<ChoiceAction, IEnumerable<int>>? answer = null,
		params int[] targets
	)
	{
		var move = s.LegalActions()
			.OfType<ActivateLoyaltyAction>()
			.First(a =>
				a.CardId == walker
				&& Actions.Loyalty.AbilitiesOf(s, s.GetCard(walker))[a.AbilityIndex].Cost == cost
				&& (targets.Length == 0 || a.Targets.SequenceEqual(targets))
			);
		s = s.Act(move);
		Resolve(answer);
	}

	[Test]
	public void EmpowerJace_CreatesTheToken_ThenLoadsTheSameOne()
	{
		Cast(Hand("Mindseeker Oculus", me));
		var jace = Jace(me)!;
		Assert.That(
			(jace.Name, jace.Loyalty, jace.Colors.Single()),
			Is.EqualTo(("Jace", 4, ManaColor.Blue))
		);

		Cast(Hand("Keeper of the Quiet Hour", me));

		Assert.That(
			s.CardsIn(me, ZoneType.Battlefield).Count(EmpowerJace.IsJaceToken),
			Is.EqualTo(1)
		);
		Assert.That(Jace(me)!.Loyalty, Is.EqualTo(4 + 2));
	}

	[Test]
	public void JaceToken_MinusOneSurveils_MinusThreeDraws()
	{
		Cast(Hand("Protege's Awakening", me)); // Empower Jace 6, draw a card
		var jace = Jace(me)!.Id;
		var top = s.CardsIn(me, ZoneType.Library).First().Id;

		Loyalty(jace, -1, choice => [top]);
		Assert.That(s.IsIn(top, me, ZoneType.Graveyard), "surveiled into the graveyard");

		s = TestGames.PassUntil(
			s,
			x => x.GetGame().TurnNumber == 3 && x.GetGame().Step == Step.PreCombatMain
		);
		var hand = HandCount(me);
		Loyalty(jace, -3);
		Assert.That(HandCount(me), Is.EqualTo(hand + 1));
		Assert.That(s.GetCard(jace).Loyalty, Is.EqualTo(6 - 1 - 3));
	}

	[Test]
	public void ProtegesAwakening_EmpowersSixAndDraws()
	{
		var hand = HandCount(me);
		Cast(Hand("Protege's Awakening", me));
		Assert.That(Jace(me)!.Loyalty, Is.EqualTo(6));
		Assert.That(HandCount(me), Is.EqualTo(hand + 1));
	}

	[TestCase("Arcane Amphisbaena", 2)]
	[TestCase("Keeper of the Quiet Hour", 2)]
	[TestCase("Mindseeker Oculus", 4)]
	[TestCase("Sanctum Lurker", 1)]
	public void EntersAndEmpowers(string card, int loyalty)
	{
		Cast(Hand(card, me));
		Assert.That(Jace(me)!.Loyalty, Is.EqualTo(loyalty));
	}

	[Test]
	public void AcademicAscent_PumpsFlyingAndEmpowersTwo()
	{
		var bear = Field(TestGames.Creature("Bear", "2/2"), me);
		Cast(Hand("Academic Ascent", me), bear);

		Assert.That(
			(s.Power(s.GetCard(bear)), s.Has(s.GetCard(bear), "Flying")),
			Is.EqualTo((4, true))
		);
		Assert.That(Jace(me)!.Loyalty, Is.EqualTo(2));
	}

	[Test]
	public void NoAdmittance_ThreeToAnyTarget_IncludingAPlaneswalker()
	{
		var theirs = Life(them);
		Cast(Hand("No Admittance", me), them);
		Assert.That(Life(them), Is.EqualTo(theirs - 3));
		Assert.That(Jace(me)!.Loyalty, Is.EqualTo(1));

		var walker = Hand(TestGames.Planeswalker("Walker", 3), them);
		s = s.EnterBattlefield(walker, them); // TEST SETUP: theirs, at printed loyalty 3
		Cast(Hand("No Admittance", me), walker);
		Assert.That(s.IsIn(walker, them, ZoneType.Graveyard), "3 damage took all 3 loyalty");
	}

	[Test]
	public void RepurposedEnforcer_AttackingEmpowersByCreatureCount()
	{
		Field("Repurposed Enforcer", me);
		Field(TestGames.Creature("Ally", "1/1"), me);

		s = TestGames.PassUntil(s, x => x.IsWaitingForChoice);
		while (s.GetPendingChoice() is DeclareAttackerChoice)
			s = s.Choose([them]);
		Resolve();

		Assert.That(Jace(me)!.Loyalty, Is.EqualTo(2));
	}

	[Test]
	public void TamsResistance_CounterAndVigilance_OrJustEmpowerWithNoTarget()
	{
		var bear = Field(TestGames.Creature("Bear", "2/2"), me);
		Cast(Hand("Tam's Resistance", me), bear);
		Assert.That(
			(s.GetCard(bear).PlusOneCounters, s.Has(s.GetCard(bear), "Vigilance")),
			Is.EqualTo((1, true))
		);

		Cast(Hand("Tam's Resistance", me), 0); // "up to one" — none chosen, so it does not fizzle
		Assert.That(Jace(me)!.Loyalty, Is.EqualTo(4 + 4));
	}

	[Test]
	public void WayOfTheHealer_GrantsMinusTwo_CadetAndSurveil()
	{
		Cast(Hand("Way of the Healer", me));
		var jace = Jace(me)!.Id;
		Assert.That(s.GetCard(jace).Loyalty, Is.EqualTo(5));

		Loyalty(jace, -2, choice => []);

		Assert.That(
			s.CardsIn(me, ZoneType.Battlefield).Count(c => c.Name == "Cadet"),
			Is.EqualTo(1)
		);
		Assert.That(s.GetCard(jace).Loyalty, Is.EqualTo(3));
	}

	[Test]
	public void WayOfTheMentor_GainingLifeAddsLoyalty()
	{
		Cast(Hand("Way of the Mentor", me));
		var before = Jace(me)!.Loyalty;

		Cast(Hand("Terminal Criticism", me), Field("Heartstring Puller", them)); // "You gain 1 life."

		Assert.That(Jace(me)!.Loyalty, Is.EqualTo(before + 1));
	}

	[Test]
	public void SanctumLurker_WalkersSurviveZero_AndGrantPlusTwoDrain()
	{
		Cast(Hand("Sanctum Lurker", me));
		var jace = Jace(me)!.Id;
		Loyalty(jace, -1, choice => []);
		Assert.That(OnBattlefield(jace), "0 loyalty, still here");

		s = TestGames.PassUntil(
			s,
			x => x.GetGame().TurnNumber == 3 && x.GetGame().Step == Step.PreCombatMain
		);
		var (mine, theirs) = (Life(me), Life(them));
		Loyalty(jace, 2);
		Assert.That(
			(Life(me), Life(them), s.GetCard(jace).Loyalty),
			Is.EqualTo((mine + 1, theirs - 1, 2))
		);
	}

	// Damage beyond the loyalty there once left Jace at -3: alive under the Lurker, but every
	// ability, even the +2, refused as unaffordable.
	[Test]
	public void ExcessDamageToAWalker_StopsAtZeroLoyalty()
	{
		Cast(Hand("Sanctum Lurker", me)); // Empower Jace 1
		var jace = Jace(me)!.Id;

		// 3 damage at 1 loyalty, then the spell's own Empower Jace 1 lands on the same token:
		// 0 + 1 with the floor, -2 + 1 without it.
		Cast(Hand("No Admittance", me), jace);

		Assert.That(OnBattlefield(jace), "the Lurker keeps it");
		Assert.That(s.GetCard(jace).Loyalty, Is.EqualTo(1));
	}

	[Test]
	public void WayOfThePyromancer_GrantsPlusOneAddRed()
	{
		Cast(Hand("Way of the Pyromancer", me));
		Loyalty(Jace(me)!.Id, 1);
		Assert.That(s.GetPlayer(me).ManaPool.Red, Is.EqualTo(1));
	}

	[Test]
	public void WayOfTheWarlord_GrantsMinusFour_TwoToACreatureAndTwoToAPlayer()
	{
		Cast(Hand("Way of the Warlord", me));
		var bear = Field(TestGames.Creature("Bear", "2/2"), them);
		var theirs = Life(them);

		Loyalty(Jace(me)!.Id, -4, null, bear, them);

		Assert.That(OnBattlefield(bear), Is.False);
		Assert.That(Life(them), Is.EqualTo(theirs - 2));
	}

	[Test]
	public void WayOfTheWildspeaker_GrantsMinusFour_ABeast()
	{
		Cast(Hand("Way of the Wildspeaker", me));
		Loyalty(Jace(me)!.Id, -4);

		var beast = s.CardsIn(me, ZoneType.Battlefield).Single(c => c.Name == "Beast");
		Assert.That(
			(s.Power(beast), s.Toughness(beast), s.Has(beast, "Trample"), beast.IsToken),
			Is.EqualTo((4, 4, true, true))
		);
	}

	[Test]
	public void AvatarOfBurgeoningEchoes_LandfallEmpowers_AndGrantsMinusTen()
	{
		Field("Avatar of Burgeoning Echoes", me);
		var island = Hand(TestGames.Card("Island"), me);
		s = s.Act(new PlayLandAction { PlayerId = me, CardId = island });
		Resolve();
		var jace = Jace(me)!.Id;
		Assert.That(s.GetCard(jace).Loyalty, Is.EqualTo(2));

		s = s.UpdateObject(jace, s.GetCard(jace) with { Loyalty = 10 }); // TEST SETUP
		var bear = Field(TestGames.Creature("Bear", "2/2"), me);
		var lands = s.CardsIn(me, ZoneType.Battlefield).Count(c => c.Is(CardType.Land));
		Loyalty(jace, -10, null, bear);

		Assert.That(s.GetCard(bear).PlusOneCounters, Is.EqualTo(lands));
	}

	// ===== Last-known information =====

	[Test]
	public void ATriggerWhoseSourceNoLongerExists_StillResolves()
	{
		var pinger = TestGames.Creature("Pinger", "1/1") with
		{
			Components =
			[
				new Effects.TriggeredAbilityComponent(
					Effects.TriggerEvent.SelfEnters,
					[new Effects.DealDamage(Effects.Ref.EachOpponent, 1)]
				),
			],
		};
		var id = Hand(pinger, me);
		var theirs = Life(them);
		s = s.Act(new CastSpellAction { PlayerId = me, CardId = id });
		s = s.Act(new PassPriorityAction { PlayerId = me });
		s = s.Act(new PassPriorityAction { PlayerId = them }); // the creature resolves; its trigger goes on the stack
		Assert.That(s.StackIsEmpty(), Is.False);

		s = s.RemoveObject(id); // TEST SETUP: gone entirely, as a dead token is
		Resolve();

		Assert.That(Life(them), Is.EqualTo(theirs - 1));
	}

	// ===== Coverage =====

	[Test]
	public void EveryScriptedName_IsARealFraCard() =>
		Assert.That(FraCards.Scripted.Where(n => !FraCards.AllNames.Contains(n)), Is.Empty);

	[Test]
	public void BothSampleDecks_AreFullyImplemented()
	{
		foreach (var deck in FraDecks.All.Values)
			Assert.That(
				deck.Where(c => !FraCards.IsImplemented(c.Data.Name)).Select(c => c.Data.Name),
				Is.Empty
			);
	}
}
