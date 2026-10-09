using ImmutableGameObjects;
using MagicCore.Actions;
using MagicCore.Cards;
using MagicCore.Effects;
using MagicCore.Sets.Fra;
using MagicCore.Turns;

namespace MagicCore.Tests.Sets;

/// <summary>Rares and mythics (8a) — each ability fires.</summary>
public class FraRareTests : FraBoardTest
{
	static CardDefinition Trinket => TestGames.Inline("Trinket", "{1}", "Artifact");

	static CardDefinition Typed(CardDefinition card, string typeLine) =>
		card with
		{
			Data = card.Data with { TypeLine = typeLine },
		};

	static CardDefinition Kill =>
		new(
			TestGames.Inline("Kill", "{1}", "Instant").Data,
			[
				new SpellEffectsComponent([new Destroy(Ref.Target())])
				{
					Targets = [new Targeting.TargetSpec(Targeting.TargetKind.Creature)],
				},
			]
		);

	IEnumerable<Card> Named(int player, string name) =>
		s.CardsIn(player, ZoneType.Battlefield).Where(c => c.Name == name);

	void AttackWith(params int[] attackers)
	{
		s = TestGames.PassUntil(s, x => x.IsWaitingForChoice);
		while (s.GetPendingChoice() is DeclareAttackerChoice declare)
			s = s.Choose([attackers.Contains(declare.CreatureId) ? them : CombatOption.None]);
		s = TestGames.PassUntil(s, x => x.GetGame().Step == Step.PostCombatMain);
	}

	[Test]
	public void AeridKonstrari_AHeartwoodOnEnteringAndDying_AndForSix()
	{
		var aerid = Hand("Aerid Konstrari", me);
		Cast(aerid, []);
		Assert.That(Named(me, "Heartwood").Count(), Is.EqualTo(1));

		s = s.Act(Activations(aerid).Single());
		Resolve();
		Assert.That(Named(me, "Heartwood").Count(), Is.EqualTo(2));
		Assert.That(
			s.Power(s.GetCard(aerid)),
			Is.EqualTo(5 + 2),
			"+X/+0 for the two artifacts, the new one too"
		);

		Cast(Hand(Kill, me), [], aerid);
		Assert.That(Named(me, "Heartwood").Count(), Is.EqualTo(3), "and one as it dies");
	}

	[Test]
	public void CraterclawColossus_YourCreaturesGetTrampleAndPlusOnePerArtifact()
	{
		Field(Trinket, me);
		var bear = Field(TestGames.Creature("Bear", "2/2"), me);
		Cast(Hand("Craterclaw Colossus", me), []);
		var card = s.GetCard(bear);
		Assert.That(
			(s.Power(card), s.Has(card, "Trample")),
			Is.EqualTo((2 + 2, true)),
			"the Trinket and the Colossus"
		);
	}

	[Test]
	public void IngrisStingerquill_EachAttackerPingsThem_AndACadetWithHaste()
	{
		var ingris = Field(FraCards.Get("Ingris Stingerquill"), me);
		var bear = Field(TestGames.Creature("Bear", "2/2"), me);
		var life = Life(them);
		AttackWith(bear);
		Assert.That(Life(them), Is.EqualTo(life - 1 - 2), "1 from Ingris, 2 from combat");

		s = s.Act(Activations(ingris).Single());
		Resolve();
		var cadet = Named(me, "Cadet").Single();
		Assert.That(s.Has(cadet, "Haste"));
	}

	[Test]
	public void OverwriteTheMultiverse_ExilesEveryCreature_JaceForEach()
	{
		var a = Field(TestGames.Creature("A", "2/2"), me);
		var b = Field(TestGames.Creature("B", "2/2"), them);
		var c = Field(TestGames.Creature("C", "2/2"), them);
		Cast(Hand("Overwrite the Multiverse", me), []);
		Assert.That(
			(
				s.IsIn(a, me, ZoneType.Exile),
				s.IsIn(b, them, ZoneType.Exile),
				s.IsIn(c, them, ZoneType.Exile)
			),
			Is.EqualTo((true, true, true))
		);
		Assert.That(Jace(me)?.Loyalty, Is.EqualTo(3));
	}

	[Test]
	public void EchoverseFulcrum_LootsOnEntering_AndExilesItselfToWipeTheBoard()
	{
		Hand(Trinket, me);
		var fulcrum = Hand("The Echoverse Fulcrum", me);
		var hand = HandCount(me);
		Cast(fulcrum, []);
		Assert.That(HandCount(me), Is.EqualTo(hand - 1), "draw one, discard one");

		var a = Field(TestGames.Creature("A", "2/2"), me);
		var b = Field(TestGames.Creature("B", "2/2"), them);
		s = s.Act(Activations(fulcrum).Single());
		Resolve();
		Assert.That((Alive(a), Alive(b)), Is.EqualTo((false, false)));
		Assert.That(s.IsIn(fulcrum, me, ZoneType.Exile));
	}

	[Test]
	public void Tarmogoyf_CountsCardTypesInAllGraveyards()
	{
		var goyf = Field(FraCards.Get("Tarmogoyf"), me);
		Assert.That((s.Power(s.GetCard(goyf)), s.Toughness(s.GetCard(goyf))), Is.EqualTo((0, 1)));
		(s, _) = TestGames.Put(s, me, TestGames.Creature("Dead", "1/1"), ZoneType.Graveyard);
		(s, _) = TestGames.Put(
			s,
			them,
			TestGames.Inline("Spent", "{1}", "Instant"),
			ZoneType.Graveyard
		);
		(s, _) = TestGames.Put(
			s,
			them,
			TestGames.Inline("Also", "{1}", "Instant"),
			ZoneType.Graveyard
		);
		(s, _) = TestGames.Put(s, them, TestGames.Card("Forest"), ZoneType.Graveyard);
		Assert.That(
			(s.Power(s.GetCard(goyf)), s.Toughness(s.GetCard(goyf))),
			Is.EqualTo((3, 4)),
			"creature, instant, land — in either graveyard"
		);
	}

	[Test]
	public void DarkMatterManipulator_MillsThree_AndTwoPowerPerSeven()
	{
		var manipulator = Hand("Dark Matter Manipulator", me);
		var graveyard = s.CardsIn(me, ZoneType.Graveyard).Count();
		Cast(manipulator, []);
		Assert.That(s.CardsIn(me, ZoneType.Graveyard).Count(), Is.EqualTo(graveyard + 3));
		while (s.CardsIn(me, ZoneType.Graveyard).Count() < 14)
			(s, _) = TestGames.Put(s, me, TestGames.Creature("Dead", "1/1"), ZoneType.Graveyard);
		Assert.That(s.Power(s.GetCard(manipulator)), Is.EqualTo(1 + 2 * 2));
	}

	[Test]
	public void SimulacrumShaper_ABasicLandOntoTheBattlefieldTapped_AndDrawsAsItDies()
	{
		int plains;
		(s, plains) = TestGames.Put(s, me, TestGames.Card("Plains"), ZoneType.Library);
		var shaper = Hand("Simulacrum Shaper", me);
		s = s.Act(new CastSpellAction { PlayerId = me, CardId = shaper });
		Resolve(choice =>
			choice switch
			{
				ChooseFromLibraryChoice => [plains],
				_ => [],
			}
		);
		Assert.That((Alive(plains), s.GetCard(plains).IsTapped), Is.EqualTo((true, true)));

		var hand = HandCount(me);
		var kill = Hand(Kill, me);
		Cast(kill, [], shaper);
		Assert.That(HandCount(me), Is.EqualTo(hand + 1 - 1 + 1), "the Kill cast, one card drawn");
	}

	[Test]
	public void LyraArchangelOfDawn_LifeGainGrowsYourAngels()
	{
		Field(FraCards.Get("Lyra, Archangel of Dawn"), me);
		var angel = Field(Typed(TestGames.Creature("Angel", "2/2"), "Creature — Angel"), me);
		var bear = Field(TestGames.Creature("Bear", "2/2"), me);
		Cast(
			Hand(TestGames.Inline("Heal", "{W}", "Instant", null, new GainLife(Ref.You, 1)), me),
			[]
		);
		Assert.That(
			(s.GetCard(angel).PlusOneCounters, s.GetCard(bear).PlusOneCounters),
			Is.EqualTo((1, 0))
		);
	}

	[Test]
	public void MasterOfBarbs_NoncombatDamageToThemPumpsYourTeam()
	{
		Field(FraCards.Get("Master of Barbs"), me);
		var bear = Field(TestGames.Creature("Bear", "2/2"), me);
		Cast(
			Hand(
				TestGames.Inline(
					"Ping",
					"{R}",
					"Instant",
					null,
					new DealDamage(Ref.EachOpponent, 1)
				),
				me
			),
			[]
		);
		Assert.That(s.Power(s.GetCard(bear)), Is.EqualTo(2 + 1));
	}

	[Test]
	public void SolariumSentry_TheirCheapSpellsGainYouTwo()
	{
		Field(FraCards.Get("Solarium Sentry"), me);
		var life = Life(me);
		TheirSpellOnTheStack(TestGames.Inline("Cheap", "{1}{R}", "Instant"));
		Resolve();
		Assert.That(Life(me), Is.EqualTo(life + 2));
		TheirSpellOnTheStack(TestGames.Inline("Pricey", "{2}{R}", "Instant"));
		Resolve();
		Assert.That(Life(me), Is.EqualTo(life + 2), "mana value 3: nothing");
	}

	[Test]
	public void LilianaTheFaultless_LifeAsYourCreaturesEnter_AndHexproofForADiscard()
	{
		var liliana = Field(FraCards.Get("Liliana the Faultless"), me);
		var life = Life(me);
		Cast(Hand(TestGames.Creature("Bear", "2/2"), me), []);
		Assert.That(Life(me), Is.EqualTo(life + 1));

		var bear = Named(me, "Bear").Single().Id;
		var fodder = Hand(Trinket, me);
		s = s.Act(Activations(liliana).Single(a => a.Targets[0] == bear && a.DiscardId == fodder));
		Resolve();
		Assert.That(
			(s.Has(s.GetCard(bear), "Hexproof"), s.IsIn(fodder, me, ZoneType.Graveyard)),
			Is.EqualTo((true, true))
		);
	}

	[Test]
	public void IngrisStingerquill_TheAttackerKilledInResponseStillPings()
	{
		Field(FraCards.Get("Ingris Stingerquill"), me);
		var bear = Field(TestGames.Creature("Bear", "2/2"), me);
		s = TestGames.PassUntil(s, x => x.IsWaitingForChoice);
		while (s.GetPendingChoice() is DeclareAttackerChoice declare)
			s = s.Choose([declare.CreatureId == bear ? them : CombatOption.None]);
		Assert.That(s.StackIsEmpty(), Is.False, "Ingris's trigger");
		var life = Life(them);
		Cast(Hand(Kill, me), [], bear);
		Assert.That((Alive(bear), Life(them)), Is.EqualTo((false, life - 1)), "as it last existed");
	}

	// ===== 8b: small new pieces =====

	static CardDefinition Walker(string name) => TestGames.Planeswalker(name, 3);

	static CardDefinition JaceCard =>
		TestGames.Planeswalker("Jace", 3) with
		{
			Data = TestGames.Planeswalker("Jace", 3).Data with { TypeLine = "Planeswalker — Jace" },
		};

	static CardDefinition Instant(string name, params GameAction[] effects) =>
		TestGames.Inline(name, "{1}", "Instant", null, effects);

	int Top(int player) => s.GetChildrenIds(s.ZoneId(player, ZoneType.Library)).First();

	[Test]
	public void StingingVitriol_TwoDamage_ThenYouPickTheirNonlandDiscard()
	{
		var land = Hand(TestGames.Card("Forest"), them);
		var spell = Hand(Instant("Spell"), them);
		var life = Life(them);
		IEnumerable<int> offered = [];
		var vitriol = Hand("Stinging Vitriol", me);
		s = s.Act(
			new CastSpellAction
			{
				PlayerId = me,
				CardId = vitriol,
				Targets = [them],
			}
		);
		Resolve(choice =>
		{
			offered = choice.Options.Select(o => o.Id).ToList();
			return [spell];
		});
		Assert.That(offered, Does.Contain(spell).And.Not.Contain(land));
		Assert.That(
			(Life(them), s.IsIn(spell, them, ZoneType.Graveyard)),
			Is.EqualTo((life - 2, true))
		);
	}

	[Test]
	public void LoyalTutor_APlaneswalkerOnTopOfTheLibrary()
	{
		int walker;
		(s, walker) = TestGames.Put(s, me, Walker("Walker"), ZoneType.Library);
		var tutor = Hand("Loyal Tutor", me);
		s = s.Act(new CastSpellAction { PlayerId = me, CardId = tutor });
		Resolve(choice => choice.Options.Select(o => o.Id).Where(id => id == walker));
		Assert.That(Top(me), Is.EqualTo(walker), "shuffled first, then put on top");
	}

	[Test]
	public void EntrustTheSpark_SacrificeAPlaneswalker_ForOneFromTheLibrary()
	{
		var old = Field(Walker("Old"), me);
		s = s.UpdateObject(old, s.GetCard(old) with { Loyalty = 3 }); // TEST SETUP: Put skips entering
		int fresh;
		(s, fresh) = TestGames.Put(s, me, Walker("Fresh"), ZoneType.Library);
		var entrust = Hand("Entrust the Spark", me);
		s = s.Act(new CastSpellAction { PlayerId = me, CardId = entrust });
		Resolve(choice =>
			choice switch
			{
				_ when choice.Options.Any(o => o.Id == old) => [old],
				_ when choice.Options.Any(o => o.Id == fresh) => [fresh],
				_ => choice.Options.Take(choice.MinChoices).Select(o => o.Id),
			}
		);
		Assert.That((s.IsIn(old, me, ZoneType.Graveyard), Alive(fresh)), Is.EqualTo((true, true)));
	}

	[Test]
	public void Codie_CopiesAPreparedSpell_AndPreparesYourCreatures()
	{
		var codie = Field(FraCards.Get("Codie, Ravenous Codex"), me);
		var foreseer = Field(FraCards.Get("Semester Foreseer // Peer Review"), me);
		s = s.Act(Activations(codie).Single());
		Resolve();
		Assert.That(
			s.GetCard(foreseer).IsPrepared,
			"{W}{U}{B}{R}{G}, {T}: each creature becomes prepared"
		);

		var copy = s.CardsIn(me, ZoneType.Exile).Single(c => c.IsCopy).Id;
		s = s.Act(Casts(copy).Single());
		Resolve();
		Assert.That(
			Named(me, "Cadet").Count(),
			Is.EqualTo(2),
			"Peer Review and Codie's copy of it"
		);
	}

	static CardDefinition ColouredArtifact(string name, params string[] colors) =>
		TestGames.Inline(name, "{1}", "Artifact") with
		{
			Data = TestGames.Inline(name, "{1}", "Artifact").Data with { Colors = colors },
		};

	[Test]
	public void KarnGildedGuardian_DrawsForEachColourAmongYourOtherArtifacts()
	{
		Field(ColouredArtifact("Red", "R"), me);
		Field(ColouredArtifact("Also Red", "R"), me);
		Field(ColouredArtifact("Blue", "U"), me);
		Field(Trinket, me); // colourless
		var karn = Hand("Karn, Gilded Guardian", me);
		var hand = HandCount(me);
		Cast(karn, []);
		Assert.That(HandCount(me), Is.EqualTo(hand - 1 + 2), "red and blue");
	}

	[Test]
	public void RoilingCanopy_AForestWithFiveOthers_PumpsYourCreature()
	{
		var canopy = Hand("Roiling Canopy", me);
		s = s.Act(new PlayLandAction { PlayerId = me, CardId = canopy });
		Assert.That(s.GetCard(canopy).IsTapped);

		var bear = Field(TestGames.Creature("Bear", "2/2"), me);
		Field(TestGames.Card("Forest"), me); // five Forests on the battlefield
		var sixth = Hand(TestGames.Card("Forest"), me);
		s = s.Tally(me, x => x with { ExtraLandDrops = 1 }); // TEST SETUP: a second land this turn
		s = s.Act(new PlayLandAction { PlayerId = me, CardId = sixth });
		Resolve(choice =>
			choice switch
			{
				ChooseTargetChoice => [bear],
				_ => [],
			}
		);
		Assert.That(s.Power(s.GetCard(bear)), Is.EqualTo(2 + 3));
	}

	[Test]
	public void Samut_NobodyCastsWhileYourInstantIsOnTheStack()
	{
		Field(FraCards.Get("Samut, Tyrant of Naktamun"), me);
		var mine = Hand(Instant("Mine", new GainLife(Ref.You, 1)), me);
		var another = Hand(Instant("Another"), me);
		var theirs = Hand(Instant("Theirs"), them);
		s = s.Act(new CastSpellAction { PlayerId = me, CardId = mine });
		Assert.That(Casts(another), Is.Empty, "split second: not even you");
		s = s.Act(new PassPriorityAction { PlayerId = me });
		Assert.That(
			s.LegalActions().OfType<CastSpellAction>().Where(c => c.CardId == theirs),
			Is.Empty,
			"nor them"
		);
	}

	[Test]
	public void JacesMachinations_EmpowersEight_AndJaceAtInstantSpeed()
	{
		Cast(Hand("Jace's Machinations", me), []);
		var jace = Jace(me)!;
		Assert.That(jace.Loyalty, Is.EqualTo(8));
		var instant = Hand(Instant("Hold", new GainLife(Ref.You, 1)), me);
		s = s.Act(new CastSpellAction { PlayerId = me, CardId = instant }); // the stack isn't empty now
		Assert.That(
			s.LegalActions().OfType<ActivateLoyaltyAction>().Any(a => a.CardId == jace.Id),
			"a Jace's loyalty ability with a spell on the stack"
		);
	}

	[Test]
	public void TheoristsSanctum_UntappedOnlyIfYouBeholdAJace()
	{
		var sanctum = Hand("Theorist's Sanctum", me);
		s = s.Act(new PlayLandAction { PlayerId = me, CardId = sanctum });
		Assert.That(s.GetCard(sanctum).IsTapped, "no Jace: tapped");

		Hand(JaceCard, me);
		var second = Hand("Theorist's Sanctum", me);
		s = s.Tally(me, x => x with { ExtraLandDrops = 1 }); // TEST SETUP: a second land this turn
		s = s.Act(new PlayLandAction { PlayerId = me, CardId = second });
		Assert.That(s.GetCard(second).IsTapped, Is.False, "a Jace card in hand to behold");
	}

	[Test]
	public void TheoristsProxy_TheNextSpellCantBeCountered()
	{
		var proxy = Field(FraCards.Get("Theorist's Proxy"), me);
		s = s.Act(Activations(proxy).Single());
		Resolve();
		var gain = Hand(Instant("Gain", new GainLife(Ref.You, 3)), me);
		s = s.Act(new CastSpellAction { PlayerId = me, CardId = gain });
		var counter = Hand(
			new CardDefinition(
				TestGames.Inline("Nope", "{1}", "Instant").Data,
				[
					new SpellEffectsComponent([new Counter(Ref.Target())])
					{
						Targets = [new Targeting.TargetSpec(Targeting.TargetKind.Spell)],
					},
				]
			),
			me
		);
		var life = Life(me);
		Cast(counter, [], gain);
		Assert.That(Life(me), Is.EqualTo(life + 3));
	}

	[Test]
	public void SolitaryCell_ExilesTheirCheapPermanent_AndLootsALegend()
	{
		var cheap = Field(TestGames.CreatureCosting("{2}", "Cheap", "2/2"), them);
		var cell = Hand("Solitary Cell", me);
		s = s.Act(new CastSpellAction { PlayerId = me, CardId = cell });
		Resolve(choice =>
			choice switch
			{
				ChooseTargetChoice => [cheap],
				_ => [],
			}
		);
		Assert.That(s.IsIn(cheap, them, ZoneType.Exile));

		var legend = Hand(
			TestGames.Creature("Legend", "1/1") with
			{
				Data = TestGames.Creature("Legend", "1/1").Data with
				{
					TypeLine = "Legendary Creature — Test",
				},
			},
			me
		);
		Hand(Trinket, me);
		Assert.That(
			Activations(cell).Select(a => a.DiscardId),
			Is.EquivalentTo(new[] { legend }),
			"a legendary card only"
		);
	}

	[Test]
	public void JacesMachinations_StillNotThroughSplitSecond()
	{
		Field(FraCards.Get("Samut, Tyrant of Naktamun"), me); // split second for your own instants too
		Cast(Hand("Jace's Machinations", me), []);
		var jace = Jace(me)!;
		var instant = Hand(Instant("Hold", new GainLife(Ref.You, 1)), me);
		s = s.Act(new CastSpellAction { PlayerId = me, CardId = instant });
		Assert.That(
			s.LegalActions().OfType<ActivateLoyaltyAction>().Any(a => a.CardId == jace.Id),
			Is.False,
			"split second stops loyalty abilities too"
		);
	}

	[Test]
	public void RoilingCanopy_TheEnteringForestRemovedInResponse_StillPumps()
	{
		Field(FraCards.Get("Roiling Canopy"), me);
		var bear = Field(TestGames.Creature("Bear", "2/2"), me);
		Field(TestGames.Card("Forest"), me); // five Forests on the battlefield
		var sixth = Hand(TestGames.Card("Forest"), me);
		s = s.Act(new PlayLandAction { PlayerId = me, CardId = sixth });
		s = s.GetPendingChoice() is ChooseTargetChoice ? s.Choose([bear]) : s;
		Assert.That(s.StackIsEmpty(), Is.False, "the trigger");
		var wreck = new CardDefinition(
			TestGames.Inline("Wreck", "{1}", "Instant").Data,
			[
				new SpellEffectsComponent([new Destroy(Ref.Target())])
				{
					Targets = [new Targeting.TargetSpec(Targeting.TargetKind.Permanent)],
				},
			]
		);
		Cast(Hand(wreck, me), [], sixth);
		Assert.That(
			(Alive(sixth), s.Power(s.GetCard(bear))),
			Is.EqualTo((false, 2 + 3)),
			"five OTHER Forests, as it resolves"
		);
	}
}
