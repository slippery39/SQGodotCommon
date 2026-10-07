using ImmutableGameObjects;
using MagicCore.Actions;
using MagicCore.Cards;
using MagicCore.Effects;
using MagicCore.Rules;
using MagicCore.Sets.Fra;
using MagicCore.Turns;

namespace MagicCore.Tests.Sets;

/// <summary>Step triggers, per-turn tracking, prowess, block and loyalty-activation triggers: each card's ability fires.</summary>
public class FraTriggerTests : FraBoardTest
{
	/// <summary>An inline instant with no target — "you cast a noncreature spell".</summary>
	int Spark() =>
		Hand(
			TestGames.Inline($"Spark{s.NextId}", "{R}", "Instant", null, new GainLife(Ref.You, 1)),
			me
		);

	void To(Step step, int active) =>
		s = TestGames.PassUntil(
			s,
			x => x.GetGame().Step == step && x.GetGame().ActivePlayerId == active
		);

	void Loyalty(int walker, int cost, params int[] targets)
	{
		var index = global::MagicCore
			.Actions.Loyalty.AbilitiesOf(s, s.GetCard(walker))
			.FindIndex(a => a.Cost == cost);
		s = s.Act(
			new ActivateLoyaltyAction
			{
				PlayerId = me,
				CardId = walker,
				AbilityIndex = index,
				Targets = [.. targets],
			}
		);
		Resolve();
	}

	List<Card> Named(string name, int player) =>
		[.. s.CardsIn(player, ZoneType.Battlefield).Where(c => c.Name == name)];

	int Cast(string fra)
	{
		var id = Hand(fra, me);
		Cast(id, []);
		return id;
	}

	void Attack()
	{
		s = TestGames.PassUntil(s, x => x.IsWaitingForChoice);
		while (s.GetPendingChoice() is DeclareAttackerChoice)
			s = s.Choose([them]);
	}

	[Test]
	public void VerdantKraken_EveryUpkeep_MakesAForestTentacleThatTapsForGreen()
	{
		Field(FraCards.Get("Verdant Kraken"), me);

		To(Step.Upkeep, them);
		Resolve();
		var tentacle = Named("Forest Tentacle", me).Single();
		Assert.That(
			(
				tentacle.IsToken,
				tentacle.Is(CardType.Land),
				s.Power(tentacle),
				s.Toughness(tentacle)
			),
			Is.EqualTo((true, true, 3, 3)),
			"theirs too: \"each player's upkeep\", and you create it"
		);
		Assert.That(ManaAbilities.ColorsOf(tentacle), Is.EqualTo(new[] { ManaColor.Green }));
		Assert.That(ManaAbilities.CanTap(s, tentacle), Is.False, "summoning sick");

		To(Step.Upkeep, me);
		Resolve();
		Assert.That(Named("Forest Tentacle", me), Has.Count.EqualTo(2));
		Assert.That(ManaAbilities.CanTap(s, s.GetCard(tentacle.Id)), "sick only until your turn");
	}

	[Test]
	public void FrostbitePyromental_DrawsTwoOnCombatDamage_AndIsSacrificedAtTheEndStep()
	{
		var pyromental = Field(FraCards.Get("Frostbite Pyromental"), me);
		var (hand, life) = (HandCount(me), Life(them));

		Attack();
		s = TestGames.PassUntil(s, x => x.GetGame().Step == Step.PostCombatMain);
		Assert.That((HandCount(me), Life(them)), Is.EqualTo((hand + 2, life - 4)));
		Assert.That(Alive(pyromental));

		To(Step.End, me);
		Resolve();
		Assert.That(s.IsIn(pyromental, me, ZoneType.Graveyard));
	}

	[Test]
	public void EyeOfJace_SurveilsFirst_ThenAtSevenItSacrificesAndDrains()
	{
		var eye = Field(FraCards.Get("Eye of Jace"), me);
		for (var i = 0; i < 6; i++)
			(s, _) = TestGames.Put(s, me, TestGames.Card("Plains"), ZoneType.Graveyard);
		To(Step.Upkeep, me);
		var (mine, theirs) = (Life(me), Life(them));

		Resolve(choice => choice.Options.Select(o => o.Id)); // surveil the card into the graveyard: 7

		Assert.That(s.IsIn(eye, me, ZoneType.Graveyard));
		Assert.That((Life(me), Life(them)), Is.EqualTo((mine + 2, theirs - 2)));
	}

	[Test]
	public void EyeOfJace_UnderSeven_JustSurveils()
	{
		var eye = Field(FraCards.Get("Eye of Jace"), me);
		for (var i = 0; i < 6; i++)
			(s, _) = TestGames.Put(s, me, TestGames.Card("Plains"), ZoneType.Graveyard);
		To(Step.Upkeep, me);
		var life = Life(them);

		Resolve(); // keep the card on top: still 6

		Assert.That((Alive(eye), Life(them)), Is.EqualTo((true, life)));
	}

	[Test]
	public void TraxosAcademyGuardian_CostsTwoLessAfterANoncreatureSpell_AndHasProwess()
	{
		var traxos = Field(FraCards.Get("Traxos, Academy Guardian"), me);
		var inHand = Hand("Traxos, Academy Guardian", me);
		Assert.That(Costs.Of(s, me, s.GetCard(inHand)).ManaValue, Is.EqualTo(4));

		Cast(Spark(), []);
		Assert.That(Costs.Of(s, me, s.GetCard(inHand)).ManaValue, Is.EqualTo(2));
		Assert.That(s.Power(s.GetCard(traxos)), Is.EqualTo(2), "prowess: 1 + 1");

		To(Step.Upkeep, them);
		Assert.That(
			Costs.Of(s, me, s.GetCard(inHand)).ManaValue,
			Is.EqualTo(4),
			"\"this turn\" resets as a turn begins"
		);
	}

	[Test]
	public void EdgarMoonlitSovereign_GrowsAtYourEndStep_IfYouCastNoSpell()
	{
		var edgar = Field(FraCards.Get("Edgar, Moonlit Sovereign"), me);
		To(Step.End, me);
		Resolve();
		Assert.That(s.GetCard(edgar).PlusOneCounters, Is.EqualTo(2));
	}

	[Test]
	public void EdgarMoonlitSovereign_ASpellThisTurn_StopsTheTrigger()
	{
		var edgar = Field(FraCards.Get("Edgar, Moonlit Sovereign"), me);
		Cast(Spark(), []);
		To(Step.End, me);
		Assert.That(s.StackIsEmpty(), "never triggered (CR 603.4)");
		Assert.That(s.GetCard(edgar).PlusOneCounters, Is.EqualTo(0));
	}

	[Test]
	public void EdgarMoonlitSovereign_ASpellInResponse_CountersTheTriggerOnResolution()
	{
		var edgar = Field(FraCards.Get("Edgar, Moonlit Sovereign"), me);
		To(Step.End, me);
		Assert.That(s.StackIsEmpty(), Is.False, "triggered: no spell yet");

		var spark = Spark();
		s = s.Act(new CastSpellAction { PlayerId = me, CardId = spark });
		Resolve();

		Assert.That(s.GetCard(edgar).PlusOneCounters, Is.EqualTo(0), "checked again (CR 603.4)");
	}

	[Test]
	public void EdgarMoonlitSovereign_ActivatedGrowsOnlyCreaturesWithACounter()
	{
		var edgar = Field(FraCards.Get("Edgar, Moonlit Sovereign"), me);
		var countered = Field(TestGames.Creature("Countered", "2/2"), me);
		var plain = Field(TestGames.Creature("Plain", "2/2"), me);
		s = s.UpdateObject(countered, s.GetCard(countered) with { PlusOneCounters = 1 }); // TEST SETUP

		s = s.Act(new ActivateAbilityAction { PlayerId = me, CardId = edgar });
		Resolve();

		Assert.That(
			(
				s.GetCard(countered).PlusOneCounters,
				s.GetCard(plain).PlusOneCounters,
				s.GetCard(edgar).PlusOneCounters
			),
			Is.EqualTo((2, 0, 0))
		);
	}

	[Test]
	public void TetsukoUmezawaPursuer_ASmallBlockerCostsItsController1()
	{
		var tetsuko = Field(FraCards.Get("Tetsuko Umezawa, Pursuer"), me);
		Field(TestGames.Creature("Wall", "1/5"), them);
		var life = Life(them);

		Attack();
		s = TestGames.PassUntil(s, x => x.GetPendingChoice() is DeclareBlockerChoice);
		s = s.Choose([tetsuko]);
		Resolve();

		Assert.That(Life(them), Is.EqualTo(life - 1), "power 1 — before any combat damage");
	}

	[Test]
	public void TetsukoUmezawaPursuer_TheBlockerLeavingFirst_StillCostsItsController1()
	{
		var tetsuko = Field(FraCards.Get("Tetsuko Umezawa, Pursuer"), me);
		var token = Field(TestGames.Creature("Token", "1/1"), them);
		s = s.UpdateObject(token, s.GetCard(token) with { IsToken = true }); // TEST SETUP
		var life = Life(them);

		Attack();
		s = TestGames.PassUntil(s, x => x.GetPendingChoice() is DeclareBlockerChoice);
		s = s.Choose([tetsuko]);
		s = s.Sacrifice(token); // TEST SETUP: gone in response — a token ceases to exist
		Resolve();

		Assert.That(s.HasObject(token), Is.False);
		Assert.That(Life(them), Is.EqualTo(life - 1), "its controller as it last was (CR 608.2h)");
	}

	[Test]
	public void TetsukoUmezawaPursuer_ABigBlockerDoesNotTrigger_AndProwessPumps()
	{
		var tetsuko = Field(FraCards.Get("Tetsuko Umezawa, Pursuer"), me);
		Field(TestGames.Creature("Bear", "2/2"), them);
		Cast(Spark(), []);
		Assert.That(s.Power(s.GetCard(tetsuko)), Is.EqualTo(3), "prowess");
		var life = Life(them);

		Attack();
		s = TestGames.PassUntil(s, x => x.GetPendingChoice() is DeclareBlockerChoice);
		s = s.Choose([tetsuko]);
		Assert.That(s.StackIsEmpty() && Life(them) == life);
	}

	[Test]
	public void JiangYangguNeverAlone_MakesMowu_AndUntapsYourTokensAtYourEndStep()
	{
		var jiang = Cast("Jiang Yanggu, Never Alone");
		var mowu = Named("Mowu", me).Single();
		Assert.That(
			(mowu.IsToken, mowu.Is(Supertype.Legendary), s.Power(mowu), s.Toughness(mowu)),
			Is.EqualTo((true, true, 3, 3))
		);

		s = s.UpdateObject(mowu.Id, s.GetCard(mowu.Id) with { IsTapped = true }); // TEST SETUP
		s = s.UpdateObject(jiang, s.GetCard(jiang) with { IsTapped = true });
		To(Step.End, me);
		Resolve();

		Assert.That(
			(s.GetCard(mowu.Id).IsTapped, s.GetCard(jiang).IsTapped),
			Is.EqualTo((false, true)),
			"tokens only"
		);
	}

	[Test]
	public void WayOfTheMindSculptor_DrawsWhenALoyaltyAbilityRemovesTwoOrMore()
	{
		Cast("Way of the Mind Sculptor");
		var jace = Jace(me)!.Id;
		var hand = HandCount(me);

		Loyalty(jace, -3); // "Draw a card", and the Way draws another
		Assert.That(HandCount(me), Is.EqualTo(hand + 2));

		To(Step.Upkeep, them);
		To(Step.PreCombatMain, me);
		hand = HandCount(me);
		Loyalty(jace, -1, []); // surveil 1 — removes only one
		Assert.That(HandCount(me), Is.EqualTo(hand));
	}

	[Test]
	public void TheTheorist_DrawsOnTheirDrawStep_AndPlusOneMakesAnIllusion()
	{
		var theorist = Cast("The Theorist, Jace Beleren");
		Assert.That(s.GetCard(theorist).Loyalty, Is.EqualTo(3));

		Loyalty(theorist, 1);
		var illusion = Named("Illusion", me).Single();
		Assert.That(
			(illusion.IsToken, s.Power(illusion), s.Toughness(illusion)),
			Is.EqualTo((true, 1, 1))
		);
		Assert.That(s.GetCard(theorist).Loyalty, Is.EqualTo(4));

		To(Step.Draw, them);
		var hand = HandCount(me);
		Resolve();
		Assert.That(HandCount(me), Is.EqualTo(hand + 1));
	}

	[Test]
	public void TheTheorist_MinusTwoBouncesTheirArtifactOrCreature()
	{
		var theorist = Cast("The Theorist, Jace Beleren");
		var bear = Field(TestGames.Creature("Bear", "2/2"), them);

		Loyalty(theorist, -2, bear);

		Assert.That(s.IsIn(bear, them, ZoneType.Hand));
	}

	[Test]
	public void TheTheorist_MinusSix_DrawsThreeThenCountersByHandSize()
	{
		var theorist = Cast("The Theorist, Jace Beleren");
		var bear = Field(TestGames.Creature("Bear", "2/2"), me);
		s = s.UpdateObject(theorist, s.GetCard(theorist) with { Loyalty = 6 }); // TEST SETUP
		var hand = HandCount(me);

		Loyalty(theorist, -6);

		Assert.That(HandCount(me), Is.EqualTo(hand + 3));
		Assert.That(s.GetCard(bear).PlusOneCounters, Is.EqualTo(hand + 3), "counted after drawing");
	}

	[Test]
	public void AjaniUnrelenting_EveryActivationMakesACadet_PlusOnePumpsAndHastesIt()
	{
		var ajani = Cast("Ajani Unrelenting");

		Loyalty(ajani, 1);

		var cadet = Named("Cadet", me).Single();
		Assert.That(
			(s.Power(cadet), s.Has(cadet, "Haste")),
			Is.EqualTo((3, true)),
			"the Cadet trigger resolves first, so the +1 includes it"
		);
	}

	[Test]
	public void AjaniUnrelenting_MinusTwo_DiscardsYourHandAndDrawsPerCreature()
	{
		var ajani = Cast("Ajani Unrelenting");
		Field(TestGames.Creature("Bear", "2/2"), me);
		var discarded = s.CardsIn(me, ZoneType.Hand).Select(c => c.Id).ToList();

		Loyalty(ajani, -2);

		Assert.That(discarded.All(id => s.IsIn(id, me, ZoneType.Graveyard)));
		Assert.That(HandCount(me), Is.EqualTo(2), "the Bear and the Cadet");
	}

	[Test]
	public void AjaniUnrelenting_MinusThree_SparesOnlyYourTokens()
	{
		var ajani = Cast("Ajani Unrelenting");
		var mine = Field(TestGames.Creature("Mine", "4/4"), me);
		var theirs = Field(TestGames.Creature("Theirs", "4/4"), them);

		Loyalty(ajani, -3);

		Assert.That((Alive(mine), Alive(theirs)), Is.EqualTo((false, false)));
		Assert.That(Named("Cadet", me), Has.Count.EqualTo(1), "a token you control");
	}

	// ===== 7c-2 leftovers: beginning of combat, graveyard triggers, "becomes the target", more "this turn" =====

	/// <summary>An inline instant: "[effects]" — something done this turn without a real card.</summary>
	int Instant(params GameAction[] effects) =>
		Hand(TestGames.Inline($"Instant{s.NextId}", "{U}", "Instant", null, effects), me);

	int GuidingHydraWithX(int x)
	{
		var hydra = Hand("Guiding Hydra", me);
		s = s.Act(
			new CastSpellAction
			{
				PlayerId = me,
				CardId = hydra,
				X = x,
			}
		);
		Resolve();
		return hydra;
	}

	[Test]
	public void GuidingHydra_EntersWithX_AndAtCombatMovesACounterOntoEachOther()
	{
		var hydra = GuidingHydraWithX(3);
		var bear = Field(TestGames.Creature("Bear", "2/2"), me);
		Assert.That(s.GetCard(hydra).PlusOneCounters, Is.EqualTo(3));

		To(Step.BeginCombat, me);
		Resolve(choice =>
			choice switch
			{
				YesNoChoice => [1],
				_ => [],
			}
		);

		Assert.That(
			(s.GetCard(hydra).PlusOneCounters, s.GetCard(bear).PlusOneCounters),
			Is.EqualTo((2, 1)),
			"one off the Hydra, one on each OTHER creature"
		);
	}

	[Test]
	public void GuidingHydra_SayingNo_ChangesNothing()
	{
		var hydra = GuidingHydraWithX(3);
		var bear = Field(TestGames.Creature("Bear", "2/2"), me);

		To(Step.BeginCombat, me);
		Resolve(); // no

		Assert.That(
			(s.GetCard(hydra).PlusOneCounters, s.GetCard(bear).PlusOneCounters),
			Is.EqualTo((3, 0))
		);
	}

	[Test]
	public void DesperateFuturescribe_PumpsAnotherCreature_OrCountersItIfYouSurveilled()
	{
		Field(FraCards.Get("Desperate Futurescribe"), me);
		var bear = Field(TestGames.Creature("Bear", "2/2"), me);

		To(Step.BeginCombat, me);
		Resolve(choice =>
			choice switch
			{
				ChooseTargetChoice => [bear],
				_ => [],
			}
		);
		Assert.That(
			(s.Power(s.GetCard(bear)), s.GetCard(bear).PlusOneCounters),
			Is.EqualTo((3, 0)),
			"+1/+1 until end of turn"
		);

		To(Step.Upkeep, them);
		To(Step.PreCombatMain, me);
		Cast(Instant(new Surveil(1)), []);
		To(Step.BeginCombat, me);
		Resolve(choice =>
			choice switch
			{
				ChooseTargetChoice => [bear],
				_ => [],
			}
		);
		Assert.That(
			(s.Power(s.GetCard(bear)), s.GetCard(bear).PlusOneCounters),
			Is.EqualTo((3, 1)),
			"a counter instead"
		);
	}

	[Test]
	public void DarklightPhoenix_ReturnsFromTheGraveyardAtCombat_AfterTwoDeaths()
	{
		int phoenix;
		(s, phoenix) = TestGames.Put(s, me, FraCards.Get("Darklight Phoenix"), ZoneType.Graveyard);
		var a = Field(TestGames.Creature("A", "1/1"), me);
		var b = Field(TestGames.Creature("B", "1/1"), them);
		s = s.Sacrifice(a); // TEST SETUP: one creature dies
		To(Step.BeginCombat, me);
		Resolve();
		Assert.That(s.IsIn(phoenix, me, ZoneType.Graveyard), "only one died");

		To(Step.Upkeep, them);
		To(Step.PreCombatMain, me);
		s = s.Sacrifice(b); // anyone's creature counts
		var c = Field(TestGames.Creature("C", "1/1"), me);
		s = s.Sacrifice(c);
		To(Step.BeginCombat, me);
		Resolve();
		Assert.That(Alive(phoenix), "two died this turn");
	}

	[Test]
	public void RuricTharBiomagus_DrawsWhenAnOpponentTargetsIt_NotWhenYouDo()
	{
		var ruric = Field(FraCards.Get("Ruric Thar, Biomagus"), me);
		var gasp = Hand("Last Gasp", them);
		var hand = HandCount(me);

		s = s.Act(new PassPriorityAction { PlayerId = me });
		s = s.Act(
			new CastSpellAction
			{
				PlayerId = them,
				CardId = gasp,
				Targets = [ruric],
			}
		);
		Resolve();
		Assert.That(HandCount(me), Is.EqualTo(hand + 1));

		var mine = Hand("Last Gasp", me);
		hand = HandCount(me);
		Cast(mine, [], ruric);
		Assert.That(HandCount(me), Is.EqualTo(hand - 1), "just the Gasp cast");
	}

	[Test]
	public void RuricTharBiomagus_TargetedTwiceByOneSpell_DrawsOnce()
	{
		var ruric = Field(FraCards.Get("Ruric Thar, Biomagus"), me);
		var twin = Hand(
			new CardDefinition(
				TestGames.Inline("Twin Bolt", "{R}", "Instant").Data,
				[
					new SpellEffectsComponent([new DealDamage(Ref.Target(0), 1)])
					{
						Targets =
						[
							new Targeting.TargetSpec(Targeting.TargetKind.Creature),
							new Targeting.TargetSpec(Targeting.TargetKind.Creature),
						],
					},
				]
			),
			them
		);
		var hand = HandCount(me);

		s = s.Act(new PassPriorityAction { PlayerId = me });
		s = s.Act(
			new CastSpellAction
			{
				PlayerId = them,
				CardId = twin,
				Targets = [ruric, ruric],
			}
		);
		Resolve();

		Assert.That(HandCount(me), Is.EqualTo(hand + 1), "it becomes the target once");
	}

	[Test]
	public void RuricTharBiomagus_ProwessTwice()
	{
		var ruric = Field(FraCards.Get("Ruric Thar, Biomagus"), me);
		Cast(Spark(), []);
		Assert.That(s.Power(s.GetCard(ruric)), Is.EqualTo(4 + 2));
	}

	[Test]
	public void LyraTolarianArchangel_AnAngelAtTheEndStep_IfYouDrewThree()
	{
		Field(FraCards.Get("Lyra, Tolarian Archangel"), me);
		Cast(Instant(new DrawCards(Ref.You, 2)), []);
		To(Step.End, me);
		Resolve();
		Assert.That(Named("Angel", me), Is.Empty, "two drawn");

		To(Step.Upkeep, them);
		s = s.Act(new PassPriorityAction { PlayerId = them });
		Cast(Instant(new DrawCards(Ref.You, 3)), []); // their turn — "each end step"
		To(Step.End, them);
		Resolve();
		var angel = Named("Angel", me).Single();
		Assert.That(
			(angel.IsToken, s.Has(angel, "Flying"), s.Power(angel)),
			Is.EqualTo((true, true, 3))
		);
	}

	[Test]
	public void LyraTolarianArchangel_ActivatedDrawsTwoOnCombatDamage_UntilEndOfTurn()
	{
		var lyra = Field(FraCards.Get("Lyra, Tolarian Archangel"), me);
		s = s.Act(new ActivateAbilityAction { PlayerId = me, CardId = lyra });
		Resolve();
		var hand = HandCount(me);

		Attack();
		s = TestGames.PassUntil(s, x => x.GetGame().Step == Step.PostCombatMain);
		Assert.That(HandCount(me), Is.EqualTo(hand + 2));

		To(Step.Upkeep, them);
		Assert.That(s.GetCard(lyra).HasComponent<UntilEndOfTurn>(), Is.False, "gone at cleanup");
	}

	[Test]
	public void GerminateRecruits_ACadetPerLifeGainedThisTurn()
	{
		Cast(Instant(new GainLife(Ref.You, 3)), []);
		Cast(Hand("Germinate Recruits", me), []);
		Assert.That(Named("Cadet", me), Has.Count.EqualTo(3));
	}

	int OnTop(CardDefinition card)
	{
		int id;
		(s, id) = TestGames.Put(s, me, card, ZoneType.Library);
		s = s.MoveObjectToFront(id, s.ZoneId(me, ZoneType.Library)); // TEST SETUP: index 0 is the top
		return id;
	}

	[Test]
	public void EnlightenedConfidant_GainedLife_SurveilsAndReturnsACheapCard()
	{
		Field(FraCards.Get("Enlightened Confidant"), me);
		Cast(Instant(new GainLife(Ref.You, 2)), []);
		var cheap = OnTop(TestGames.CreatureCosting("{1}{G}", "Cheap", "2/2"));

		To(Step.End, me);
		Resolve(choice =>
			choice switch
			{
				ChooseFromLibraryTopChoice => [cheap],
				_ => [],
			}
		);

		Assert.That(s.IsIn(cheap, me, ZoneType.Hand), "mana value 2, two life gained");
	}

	[Test]
	public void EnlightenedConfidant_TooExpensive_StaysInTheGraveyard()
	{
		Field(FraCards.Get("Enlightened Confidant"), me);
		Cast(Instant(new GainLife(Ref.You, 2)), []);
		var dear = OnTop(TestGames.CreatureCosting("{2}{G}", "Dear", "2/2"));

		To(Step.End, me);
		Resolve(choice =>
			choice switch
			{
				ChooseFromLibraryTopChoice => [dear],
				_ => [],
			}
		);

		Assert.That(s.IsIn(dear, me, ZoneType.Graveyard));
	}

	[Test]
	public void EnlightenedConfidant_NoLifeGained_NoTrigger()
	{
		Field(FraCards.Get("Enlightened Confidant"), me);
		To(Step.End, me);
		Assert.That(s.StackIsEmpty());
	}

	[Test]
	public void SurveillancePhantasm_AttacksOnlyAfterYouSurveil()
	{
		var phantasm = Field(FraCards.Get("Surveillance Phantasm"), me);
		Assert.That(Combat.CanAttack(s, s.GetCard(phantasm)), Is.False, "defender");

		s = s.Act(new ActivateAbilityAction { PlayerId = me, CardId = phantasm });
		Resolve();

		Assert.That(Combat.CanAttack(s, s.GetCard(phantasm)));
	}

	[Test]
	public void WayOfTheParadox_ALoyaltyActivationGainsOneAndAnExtraLandDrop()
	{
		Cast("Way of the Paradox");
		var (first, second) = (
			Hand(TestGames.Card("Forest"), me),
			Hand(TestGames.Card("Forest"), me)
		);
		s = s.Act(new PlayLandAction { PlayerId = me, CardId = first });
		Assert.That(
			s.LegalActions().OfType<PlayLandAction>().Any(a => a.CardId == second),
			Is.False,
			"one land a turn"
		);
		var life = Life(me);

		Loyalty(Jace(me)!.Id, -1);

		Assert.That(Life(me), Is.EqualTo(life + 1));
		Assert.That(s.LegalActions().OfType<PlayLandAction>().Any(a => a.CardId == second));
	}
}
