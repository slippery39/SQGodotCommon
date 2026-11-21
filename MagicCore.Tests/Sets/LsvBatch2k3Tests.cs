using ImmutableGameObjects;
using MagicCore.Actions;
using MagicCore.Cards;
using MagicCore.Effects;
using MagicCore.Rules;
using MagicCore.Sets;
using MagicCore.Targeting;
using MagicCore.Turns;
using Is = MagicCore.Filters.Is;
using Nu = NUnit.Framework.Is;

namespace MagicCore.Tests.Sets;

/// <summary>
/// MC-4.4k3, LSVPower — green's four new pieces, FIRING against their printed text: crew (Esika's Chariot), earthbend
/// and "an additional {G}" (Badgermole Cub), bestow (Springheart Nantuko) and day/night (Outland Liberator // Frenzied
/// Trapbreaker).
/// </summary>
public class LsvBatch2k3Tests : FraBoardTest
{
	static CardDefinition Bear => TestGames.Creature("Bear", "2/2");
	static CardDefinition Ogre => TestGames.Creature("Ogre", "4/4");

	static IEnumerable<int> Defaults(ChoiceAction c) =>
		c.Options.Take(c.MinChoices).Select(o => o.Id);

	List<Card> Named(int p, string name) =>
		[.. s.CardsIn(p, ZoneType.Battlefield).Where(c => c.Name == name)];

	int AForest() => s.CardsIn(me, ZoneType.Battlefield).First(c => c.Name == "Forest").Id;

	/// <summary>The next turn's first main phase, nothing cast on the way.</summary>
	void NextTurn()
	{
		var turn = s.GetGame().TurnNumber;
		s = TestGames.PassUntil(
			s,
			x =>
				x.GetGame().TurnNumber > turn
				&& x.GetGame().Step == Step.PreCombatMain
				&& x.GetGame().PriorityPlayerId != 0
		);
	}

	/// <summary>I play a land from my hand; its landfall resolves, each choice answered as told.</summary>
	List<ChoiceAction> LandEnters(Func<ChoiceAction, IEnumerable<int>> answer)
	{
		var forest = Hand(TestGames.Card("Forest"), me);
		s = s.Act(new PlayLandAction { PlayerId = me, CardId = forest });
		return ResolveAsking(answer);
	}

	// ===== crew =====

	[Test]
	public void EsikasChariot_Crew4_AnArtifactCreatureUntilEndOfTurn_ItsPrinted44()
	{
		var chariot = Field(Lsv("Esika's Chariot"), me);
		var ogre = Field(Ogre, me);
		Assert.That(!s.GetCard(chariot).Is(CardType.Creature), "a Vehicle is no creature uncrewed");
		s = s.Act(Activations(chariot).Single());
		ResolveAsking(Defaults);
		var crewed = s.GetCard(chariot);
		Assert.That(s.GetCard(ogre).IsTapped, "the Ogre's 4 power crews it");
		Assert.That(
			(
				crewed.Is(CardType.Creature),
				crewed.Is(CardType.Artifact),
				s.Power(crewed),
				s.Toughness(crewed)
			),
			Nu.EqualTo((true, true, 4, 4)),
			"an artifact creature, its printed 4/4"
		);
		Assert.That(
			Activations(chariot),
			Nu.Empty,
			"other creatures crew it: a crewed Chariot can't tap itself for more"
		);
		NextTurn();
		Assert.That(!s.GetCard(chariot).Is(CardType.Creature), "until end of turn");
		Assert.That(s.GetCard(chariot).Is(CardType.Artifact), "still the artifact it was");
	}

	[Test]
	public void EsikasChariot_Crew_NotWithLessThanFourPower()
	{
		var chariot = Field(Lsv("Esika's Chariot"), me);
		Field(Bear, me);
		Assert.That(Activations(chariot), Nu.Empty, "2 power");
	}

	[Test]
	public void EsikasChariot_Enters_TwoGreen22Cats_Attacks_ACopyOfATokenOfMine()
	{
		var chariot = Hand(Lsv("Esika's Chariot"), me);
		Cast(chariot, []);
		var cats = Named(me, "Cat");
		Assert.That(
			cats.Select(c => (s.Power(c), s.Toughness(c), c.IsToken, c.Colors.Single().ToString())),
			Nu.EqualTo(new[] { (2, 2, true, "Green"), (2, 2, true, "Green") }),
			"two 2/2 green Cat tokens"
		);
		NextTurn(); // theirs
		NextTurn(); // mine: nothing of mine is summoning sick
		s = s.Act(Activations(chariot).Single());
		ResolveAsking(Defaults);
		Assert.That(Named(me, "Cat").All(c => c.IsTapped), "the two Cats' 4 power crews it");
		AttackWith(On<ChooseTargetChoice>(cats[0].Id), chariot);
		Assert.That(Named(me, "Cat"), Has.Count.EqualTo(3), "a copy of target token you control");
		Assert.That(Life(them), Nu.EqualTo(20 - 4), "and the Chariot's 4");
	}

	// ===== earthbend =====

	int Earthbend()
	{
		var forest = AForest();
		CastAsking(Hand(Lsv("Badgermole Cub"), me), On<ChooseTargetChoice>(forest));
		return forest;
	}

	[Test]
	public void BadgermoleCub_Earthbend1_TheLandA00HasteCreatureWithACounter_StillALand()
	{
		var forest = Earthbend();
		var land = s.GetCard(forest);
		Assert.That(
			(land.Is(CardType.Land), land.Is(CardType.Creature), land.PlusOneCounters),
			Nu.EqualTo((true, true, 1)),
			"a creature that's still a land, a +1/+1 counter"
		);
		Assert.That((s.Power(land), s.Toughness(land)), Nu.EqualTo((1, 1)), "0/0 and the counter");
		Assert.That(s.Has(land, "Haste"), "with haste");
		NextTurn();
		Assert.That(s.GetCard(forest).Is(CardType.Creature), "no duration: it stays a creature");
	}

	[Test]
	public void Earthbent_Dies_ReturnsToTheBattlefieldTapped_ALandAgain()
	{
		var forest = Earthbend();
		Cast(Hand(TestCards.Kill(), me), [], forest);
		var back = s.GetCard(forest);
		Assert.That(
			Alive(forest) && back.IsTapped,
			"when it dies, return it to the battlefield tapped"
		);
		Assert.That(
			(back.Is(CardType.Creature), back.PlusOneCounters),
			Nu.EqualTo((false, 0)),
			"a new object: the land it is"
		);
	}

	[Test]
	public void Earthbent_Exiled_ReturnsToTheBattlefieldTapped()
	{
		var forest = Earthbend();
		Cast(
			Hand(TestCards.Targeted("Banish", TargetSpec.Creature, new Exile(Ref.Target())), me),
			[],
			forest
		);
		Assert.That(Alive(forest) && s.GetCard(forest).IsTapped, "or is exiled");
	}

	[Test]
	public void Earthbent_ReturnedToHand_StaysThere()
	{
		var forest = Earthbend();
		Cast(
			Hand(
				TestCards.Targeted(
					"Bounce",
					TargetSpec.Creature,
					new MoveTo(Ref.Target(), ZoneType.Hand)
				),
				me
			),
			[],
			forest
		);
		Assert.That(s.IsIn(forest, me, ZoneType.Hand), "it neither died nor was exiled");
	}

	static CardDefinition Elf =>
		TestGames.Creature("Elf", "1/1") with
		{
			Components = [new ManaAbilityComponent(ManaColor.Green)],
		};

	int Mana(int card) => ManaAbilities.YieldOf(s, s.GetCard(card)).Units.Length;

	[Test]
	public void BadgermoleCub_ACreatureITapForMana_AnAdditionalGreen()
	{
		var mine = Field(Elf, me);
		var theirs = Field(Elf, them);
		Assert.That(Mana(mine), Nu.EqualTo(1), "an Elf alone: {G}");
		var forest = Earthbend();
		Assert.That(
			ManaAbilities.YieldOf(s, s.GetCard(mine)).Units.Select(u => u.Single()),
			Nu.EqualTo(new[] { ManaColor.Green, ManaColor.Green }),
			"whenever you tap a creature for mana, add an additional {G}"
		);
		Assert.That(
			Mana(forest),
			Nu.EqualTo(2),
			"the earthbent Forest is a creature tapped for mana"
		);
		Assert.That(
			Mana(s.CardsIn(me, ZoneType.Battlefield).First(c => c.Name == "Island").Id),
			Nu.EqualTo(1),
			"a land that is no creature: nothing more"
		);
		Assert.That(Mana(theirs), Nu.EqualTo(1), "whenever YOU tap: their Elf makes {G}");
	}

	static CardDefinition Bird =>
		TestGames.Creature("Bird", "0/1") with
		{
			Components = [new ManaAbilityComponent(ManaColor.Green) { AnyOneColor = true }],
		};

	[Test]
	public void BadgermoleCub_BesideACreatureOfAnyOneColour_TheAdditionalManaIsStillGreen()
	{
		Field(Lsv("Badgermole Cub"), me);
		var bird = Field(Bird, me);
		TapMyLands();
		Assert.That(
			ManaPayment.Find(s, me, ManaCost.Parse("{U}{G}"), 0),
			Nu.Not.Null,
			"the Bird's {U} and the additional {G}"
		);
		Assert.That(
			ManaPayment.Find(s, me, ManaCost.Parse("{U}{U}"), 0),
			Nu.Null,
			"an additional {G} — never a second mana of the colour chosen"
		);
		Assert.That(
			ManaPayment.Find(s, me, ManaCost.Parse("{U}"), 0)!.Single(p => p.Floats).Color,
			Nu.EqualTo(ManaColor.Green),
			"unspent, it floats as {G}"
		);
		s = s.Act(
			new TapForManaAction
			{
				PlayerId = me,
				CardId = bird,
				Color = ManaColor.Blue,
			}
		);
		var pool = s.GetPlayer(me).ManaPool;
		Assert.That(
			(pool.Blue, pool.Green, pool.Total),
			Nu.EqualTo((1, 1, 2)),
			"tapped by hand for {U}"
		);
	}

	[Test]
	public void AnAdditionalManaOfOneColour_PaidFirst_DoesNotChooseItsSourcesColour()
	{
		Field(Bird, me);
		s = GetEmblem.ToCommandZone(s, me, "Red Tide", [new ExtraMana(Is.Creature, ManaColor.Red)]); // TEST SETUP: "whenever you tap a creature for mana, add an additional {R}" this turn
		TapMyLands();
		Assert.That(
			ManaPayment.Find(s, me, ManaCost.Parse("{R}{G}"), 0),
			Nu.Not.Null,
			"the additional {R} pays {R}; the Bird's one mana is still any colour — {G}"
		);
	}

	[Test]
	public void BadgermoleCub_ACreatureTappedForManaWithAnotherCost_AnAdditionalGreen()
	{
		Field(Lsv("Badgermole Cub"), me);
		var lumberjack = Field(Lsv("Orcish Lumberjack"), me);
		var move = s.LegalActions()
			.OfType<TapForManaAction>()
			.First(a => a.CardId == lumberjack && a.OfTheOther == 1);
		s = s.Act(move);
		var pool = s.GetPlayer(me).ManaPool;
		Assert.That(
			(pool.Red, pool.Green, pool.Total),
			Nu.EqualTo((2, 2, 4)),
			"{T}, Sacrifice a Forest: its three mana, and the Cub's {G} for the creature tapped"
		);
	}

	[Test]
	public void Crew_OnACopyUntilEndOfTurn_TheOriginalComesBackAsItWas()
	{
		var chariot = Field(Lsv("Esika's Chariot"), me);
		var bear = Field(Bear, me);
		Field(Ogre, me);
		var mirror = new CardDefinition(
			TestGames.Inline("Mirror", "{1}", "Instant").Data,
			[
				new SpellEffectsComponent(
					[new BecomeCopyUntilEndOfTurn(Ref.Target(0), Ref.Target(1))]
				)
				{
					Targets = [TargetSpec.Creature, Scripting.Target(Is.Artifact)],
				},
			]
		);
		Cast(Hand(mirror, me), [], bear, chariot);
		s = s.Act(Activations(bear).Single()); // the Bear, a Chariot until end of turn, crewed by the Ogre
		ResolveAsking(Defaults);
		Assert.That(s.GetCard(bear).Is(CardType.Creature | CardType.Artifact), "a crewed copy");
		NextTurn();
		var back = s.GetCard(bear);
		Assert.That(
			(back.Name, back.Is(CardType.Creature), back.Is(CardType.Artifact), s.Power(back)),
			Nu.EqualTo(("Bear", true, false, 2)),
			"the crew's stamp ends before the copy does: the Bear it was"
		);
	}

	// ===== bestow =====

	/// <summary>Nantuko cast for its bestow cost on this creature; the cast only, nothing resolved.</summary>
	int CastBestowed(int host)
	{
		var nantuko = Hand(Lsv("Springheart Nantuko"), me);
		s = s.Act(Casts(nantuko).Single(c => c.Targets.Contains(host)));
		return nantuko;
	}

	[Test]
	public void SpringheartNantuko_Bestowed_AnAuraOnTheCreature_Plus1Plus1_NoCreatureEntered()
	{
		var bear = Field(Bear, me);
		Field(
			TestCards.WithTrigger(
				Ogre,
				TriggerEvent.AnotherCreatureYouControlEnters,
				new GainLife(Ref.You, 1)
			),
			me
		);
		var nantuko = Hand(Lsv("Springheart Nantuko"), me);
		Assert.That(
			Casts(nantuko).Count(c => c.Targets.IsEmpty),
			Nu.EqualTo(1),
			"the creature spell is still offered beside the bestowed casts"
		);
		var lands = UntappedLands();
		var bestowed = Casts(nantuko).Single(c => c.Targets.Contains(bear));
		Assert.That(Describe.Action(s, bestowed), Does.Contain("(bestow)"));
		s = s.Act(bestowed);
		Resolve();
		var aura = s.GetCard(nantuko);
		Assert.That(lands - UntappedLands(), Nu.EqualTo(2), "bestow {1}{G}");
		Assert.That(
			(aura.AttachedToId, aura.Is(CardType.Creature), aura.Is(CardType.Enchantment)),
			Nu.EqualTo((bear, false, true)),
			"an Aura on the Bear — not a creature"
		);
		Assert.That(s.HasSubtype(aura, "Aura"), "an Aura");
		Assert.That(
			(s.Power(s.GetCard(bear)), s.Toughness(s.GetCard(bear))),
			Nu.EqualTo((3, 3)),
			"enchanted creature gets +1/+1"
		);
		Assert.That(Life(me), Nu.EqualTo(20), "an Aura entered: no 'whenever a creature enters'");
	}

	[Test]
	public void SpringheartNantuko_Bestowed_ItsCreatureDies_ACreatureAgain()
	{
		var bear = Field(Bear, me);
		var nantuko = CastBestowed(bear);
		Resolve();
		Cast(Hand(TestCards.Kill(), me), [], bear);
		var insect = s.GetCard(nantuko);
		Assert.That(Alive(nantuko), "it isn't put into the graveyard as an Aura is");
		Assert.That(
			(insect.AttachedToId, insect.Is(CardType.Creature), s.Power(insect)),
			Nu.EqualTo((0, true, 1)),
			"a creature again, its own 1/1"
		);
		Assert.That(!s.HasSubtype(insect, "Aura"), "no Aura");
	}

	[Test]
	public void SpringheartNantuko_Bestowed_ItsTargetGoneAsItResolves_ItResolvesACreature()
	{
		var bear = Field(Bear, me);
		var nantuko = CastBestowed(bear);
		s = s.Sacrifice(bear); // TEST SETUP: the Bear gone in response
		Resolve();
		Assert.That(
			Alive(nantuko) && s.GetCard(nantuko).Is(CardType.Creature),
			"not countered: an enchantment creature (CR 702.103e)"
		);
	}

	[Test]
	public void SpringheartNantuko_Bestowed_ItsTargetHexproofAsItResolves_ACreatureOnNothing()
	{
		var theirs = Field(Bear, them);
		var nantuko = CastBestowed(theirs);
		s = s.UpdateObject(
			theirs,
			s.GetCard(theirs).WithComponent(new UntilEndOfTurn(0, 0, "Hexproof"))
		); // TEST SETUP: it gains hexproof in response
		Resolve();
		var insect = s.GetCard(nantuko);
		Assert.That(
			(Alive(nantuko), insect.AttachedToId, insect.Is(CardType.Creature)),
			Nu.EqualTo((true, 0, true)),
			"an illegal target: it resolves a creature, attached to nothing"
		);
		Assert.That(s.Power(s.GetCard(theirs)), Nu.EqualTo(2), "their Bear gets nothing");
	}

	[Test]
	public void SpringheartNantuko_Landfall_Attached_PayForACopyOfThatCreature()
	{
		var bear = Field(Bear, me);
		CastBestowed(bear);
		Resolve();
		var lands = UntappedLands();
		var asked = LandEnters(c =>
			c switch
			{
				YesNoChoice => [1],
				_ => Defaults(c),
			}
		);
		Assert.That(asked.OfType<YesNoChoice>().Count(), Nu.EqualTo(1), "you may pay {1}{G}");
		Assert.That(UntappedLands(), Nu.EqualTo(lands + 1 - 2), "the new Forest, less {1}{G}");
		Assert.That(
			Named(me, "Bear"),
			Has.Count.EqualTo(2),
			"a token that's a copy of that creature"
		);
		Assert.That(Named(me, "Insect"), Nu.Empty, "a token was created this way: no Insect");
	}

	[Test]
	public void SpringheartNantuko_Landfall_AttachedAndNotPaid_AnInsect()
	{
		var bear = Field(Bear, me);
		CastBestowed(bear);
		Resolve();
		LandEnters(Defaults);
		Assert.That(Named(me, "Bear"), Has.Count.EqualTo(1), "not paid");
		var insect = Named(me, "Insect").Single();
		Assert.That(
			(s.Power(insect), s.Toughness(insect), insect.Colors.Single().ToString()),
			Nu.EqualTo((1, 1, "Green")),
			"a 1/1 green Insect creature token"
		);
	}

	[Test]
	public void SpringheartNantuko_Landfall_OnTheirCreature_AnInsectUnasked()
	{
		var theirs = Field(Bear, them);
		CastBestowed(theirs);
		Resolve();
		var asked = LandEnters(Defaults);
		Assert.That(
			asked.OfType<YesNoChoice>(),
			Nu.Empty,
			"a creature YOU control: nothing to pay for"
		);
		Assert.That(Named(me, "Insect"), Has.Count.EqualTo(1), "the Insect");
	}

	[Test]
	public void SpringheartNantuko_Landfall_ACreatureItself_AnInsectUnasked()
	{
		Field(Lsv("Springheart Nantuko"), me);
		Field(Bear, me);
		var asked = LandEnters(Defaults);
		Assert.That(
			asked.OfType<YesNoChoice>(),
			Nu.Empty,
			"attached to nothing: nothing to pay for"
		);
		Assert.That(Named(me, "Insect"), Has.Count.EqualTo(1), "the Insect");
	}

	// ===== day / night =====

	const string Liberator = "Outland Liberator // Frenzied Trapbreaker";

	Daytime Now => s.GetGame().Daytime;

	[Test]
	public void OutlandLiberator_Enters_ItBecomesDay_ATurnWithNoSpells_Night_ItTransforms()
	{
		Assert.That(Now, Nu.EqualTo(Daytime.Neither), "neither day nor night to begin with");
		var liberator = Hand(Lsv(Liberator), me);
		Cast(liberator, []);
		Assert.That(Now, Nu.EqualTo(Daytime.Day), "a daybound permanent: it becomes day");
		NextTurn(); // theirs — I cast a spell on mine
		Assert.That(
			(Now, s.GetCard(liberator).Name),
			Nu.EqualTo((Daytime.Day, Liberator)),
			"a spell was cast that turn: still day"
		);
		NextTurn(); // mine — they cast none on theirs
		var wolf = s.GetCard(liberator);
		Assert.That(Now, Nu.EqualTo(Daytime.Night), "no spells during their own turn: night");
		Assert.That(
			(wolf.Name, s.Power(wolf), s.Toughness(wolf)),
			Nu.EqualTo(("Frenzied Trapbreaker", 3, 3)),
			"it transforms as it becomes night"
		);
	}

	[Test]
	public void Night_TwoSpellsOnATurn_Day_OneIsNotEnough()
	{
		var liberator = Hand(Lsv(Liberator), me);
		Cast(liberator, []);
		s = DayNight.Become(s, Daytime.Night); // TEST SETUP: night
		Assert.That(s.GetCard(liberator).Name, Nu.EqualTo("Frenzied Trapbreaker"));
		NextTurn(); // theirs — one spell on mine (the Liberator)
		Assert.That(Now, Nu.EqualTo(Daytime.Night), "one spell: still night");
		NextTurn(); // mine
		Cast(Hand(TestCards.Fodder, me), []);
		Cast(Hand(TestCards.Fodder, me), []);
		NextTurn();
		Assert.That(
			(Now, s.GetCard(liberator).Name),
			Nu.EqualTo((Daytime.Day, Liberator)),
			"at least two spells during their own turn: day, and it transforms back"
		);
	}

	[Test]
	public void OutlandLiberator_EntersAtNight_Transformed()
	{
		s = DayNight.Become(s, Daytime.Night); // TEST SETUP: night
		var liberator = Hand(Lsv(Liberator), me);
		Cast(liberator, []);
		Assert.That(s.GetCard(liberator).Name, Nu.EqualTo("Frenzied Trapbreaker"));
		Assert.That(Now, Nu.EqualTo(Daytime.Night), "it stays night");
	}

	[Test]
	public void OutlandLiberator_OneAndSacrifice_DestroysAnArtifactOrEnchantment()
	{
		var liberator = Field(Lsv(Liberator), me);
		var trinket = Field(TestCards.Trinket, them);
		Field(Bear, them);
		var lands = UntappedLands();
		s = s.Act(Activations(liberator).Single());
		Resolve();
		Assert.That(lands - UntappedLands(), Nu.EqualTo(1), "{1}");
		Assert.That(
			InGraveyard(liberator) && InGraveyard(trinket),
			"sacrificed; the artifact destroyed"
		);
	}

	[Test]
	public void FrenziedTrapbreaker_Attacks_DestroysAnArtifactTheDefendingPlayerControls()
	{
		var liberator = Field(Lsv(Liberator), me);
		Field(TestCards.Trinket, me);
		var theirs = Field(TestCards.Trinket, them);
		s = DayNight.Become(s, Daytime.Night); // TEST SETUP: night
		var asked = new List<ChoiceAction>();
		AttackWith(
			c =>
			{
				asked.Add(c);
				return Defaults(c);
			},
			liberator
		);
		Assert.That(
			Offered<ChooseTargetChoice>(asked),
			Nu.EqualTo(new[] { theirs }),
			"defending player controls — never my own Trinket"
		);
		Assert.That(InGraveyard(theirs), "destroyed");
		Assert.That(Life(them), Nu.EqualTo(20 - 3), "the back face's 3 power");
	}
}
