using ImmutableGameObjects;
using MagicCore.Actions;
using MagicCore.Cards;
using MagicCore.Effects;
using MagicCore.Sets;
using MagicCore.Sets.Fra;
using MagicCore.Targeting;
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
					Targets = [TargetSpec.Creature],
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
		var ingris = Field(CardRegistry.Get("Ingris Stingerquill"), me);
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
		var goyf = Field(CardRegistry.Get("Tarmogoyf"), me);
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
		Field(CardRegistry.Get("Lyra, Archangel of Dawn"), me);
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
		Field(CardRegistry.Get("Master of Barbs"), me);
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
		Field(CardRegistry.Get("Solarium Sentry"), me);
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
		var liliana = Field(CardRegistry.Get("Liliana the Faultless"), me);
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
		Field(CardRegistry.Get("Ingris Stingerquill"), me);
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
		var codie = Field(CardRegistry.Get("Codie, Ravenous Codex"), me);
		var foreseer = Field(CardRegistry.Get("Semester Foreseer // Peer Review"), me);
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
		Field(CardRegistry.Get("Samut, Tyrant of Naktamun"), me);
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
		var proxy = Field(CardRegistry.Get("Theorist's Proxy"), me);
		s = s.Act(Activations(proxy).Single());
		Resolve();
		var gain = Hand(Instant("Gain", new GainLife(Ref.You, 3)), me);
		s = s.Act(new CastSpellAction { PlayerId = me, CardId = gain });
		Assert.That(s.CardsIn(me, ZoneType.Command), Is.Empty, "used up by that spell");
		var counter = Hand(
			new CardDefinition(
				TestGames.Inline("Nope", "{1}", "Instant").Data,
				[
					new SpellEffectsComponent([new Counter(Ref.Target())])
					{
						Targets = [TargetSpec.Spell()],
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
		Field(CardRegistry.Get("Samut, Tyrant of Naktamun"), me); // split second for your own instants too
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
		Field(CardRegistry.Get("Roiling Canopy"), me);
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
					Targets = [TargetSpec.Permanent()],
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

	// ===== 8c: Treasure and artifact tokens =====

	void TapAllLands(int player)
	{
		foreach (
			var land in s.CardsIn(player, ZoneType.Battlefield)
				.Where(c => c.Is(CardType.Land))
				.ToList()
		)
			s = s.UpdateObject(land.Id, land with { IsTapped = true }); // TEST SETUP
	}

	[Test]
	public void VraskaSoulOfStone_ASculptureTreasurePerNoncreatureSpell_WithVigilance()
	{
		Field(CardRegistry.Get("Vraska, Soul of Stone"), me);
		Cast(Hand(Instant("Spell"), me), []);
		var sculpture = Named(me, "Sculpture Treasure").Single();
		Assert.That(
			(sculpture.Is(CardType.Artifact | CardType.Creature), s.Has(sculpture, "Vigilance")),
			Is.EqualTo((true, true))
		);
		var bear = Field(TestGames.Creature("Bear", "2/2"), me);
		Assert.That(s.Has(s.GetCard(bear), "Vigilance"), Is.False, "artifact creatures only");
	}

	[Test]
	public void ATreasurePaysOneManaOfAnyColour_AndIsSacrificed()
	{
		var treasure = Field(new CardDefinition(Tokens.Treasure, [Tokens.SacrificeForMana(1)]), me);
		s = s.UpdateObject(treasure, s.GetCard(treasure) with { IsToken = true }); // TEST SETUP
		TapAllLands(me);
		var spell = Hand(TestGames.Inline("Black", "{B}", "Instant"), me);
		Assert.That(Casts(spell), Is.Not.Empty);
		s = s.Act(Casts(spell).Single());
		Assert.That(s.HasObject(treasure), Is.False, "sacrificed — a token, so it is gone");
	}

	[Test]
	public void ATreasureIsSpentLast()
	{
		var treasure = Field(new CardDefinition(Tokens.Treasure, [Tokens.SacrificeForMana(1)]), me);
		var spell = Hand(TestGames.Inline("Black", "{B}", "Instant"), me);
		s = s.Act(Casts(spell).Single());
		Assert.That(Alive(treasure), "a Swamp paid; the Treasure was kept");
	}

	[Test]
	public void VraskaCuttingGlare_DestroysTheirPermanent_AndTheyGetATreasure()
	{
		var theirs = Field(TestGames.Creature("Theirs", "2/2"), them);
		var vraska = Hand("Vraska, the Cutting Glare", me);
		s = s.Act(new CastSpellAction { PlayerId = me, CardId = vraska });
		Resolve(choice =>
			choice switch
			{
				ChooseTargetChoice => [theirs],
				_ => [],
			}
		);
		Assert.That(s.IsIn(theirs, them, ZoneType.Graveyard));
		Assert.That(Named(them, "Treasure").Count(), Is.EqualTo(1), "under THEIR control");
	}

	[Test]
	public void VraskaCuttingGlare_NeedsSixLands()
	{
		foreach (
			var land in s.CardsIn(me, ZoneType.Battlefield)
				.Where(c => c.Is(CardType.Land))
				.OrderBy(c => c.Name != "Swamp")
				.Skip(5)
				.ToList()
		)
			s = s.MoveObject(land.Id, s.ZoneId(me, ZoneType.Exile)); // TEST SETUP: five lands
		Field(TestGames.Creature("Theirs", "2/2"), them);
		var asked = false;
		var vraska = Hand(
			CardRegistry.Get("Vraska, the Cutting Glare") with
			{
				Data = CardRegistry.Get("Vraska, the Cutting Glare").Data with { ManaCost = "{B}" },
			},
			me
		);
		s = s.Act(new CastSpellAction { PlayerId = me, CardId = vraska });
		Resolve(choice =>
		{
			asked |= choice is ChooseTargetChoice;
			return [];
		});
		Assert.That(asked, Is.False);
	}

	[Test]
	public void KwiaVigorbloom_ALotusOnLifeGain_OncePerTurn_ForThreeOfOneColour()
	{
		Field(CardRegistry.Get("Kwia Vigorbloom"), me);
		Cast(Hand(Instant("Heal", new GainLife(Ref.You, 1)), me), []);
		Cast(Hand(Instant("Heal", new GainLife(Ref.You, 1)), me), []);
		Assert.That(Named(me, "Lotus").Count(), Is.EqualTo(1));
		TapAllLands(me);
		Assert.That(
			Casts(Hand(TestGames.Inline("Triple", "{G}{G}{G}", "Instant"), me)),
			Is.Not.Empty
		);
		Assert.That(
			Casts(Hand(TestGames.Inline("Mixed", "{G}{U}", "Instant"), me)),
			Is.Empty,
			"one colour"
		);
	}

	[Test]
	public void DraconicVisitor_ArtifactTokensAreDragonsInstead()
	{
		Field(CardRegistry.Get("Draconic Visitor"), me);
		Cast(Hand("Aerid Konstrari", me), []); // a Heartwood token as it enters
		Assert.That(Named(me, "Heartwood"), Is.Empty);
		var dragon = Named(me, "Dragon").Single();
		Assert.That((s.Power(dragon), s.Has(dragon, "Flying")), Is.EqualTo((5, true)));
	}

	[Test]
	public void HungeringPuppetbeast_SacrificeAnArtifact_ForACounterAndYourChoiceOfKeyword()
	{
		var beast = Hand("Hungering Puppetbeast", me);
		Cast(beast, []);
		var heartwood = Named(me, "Heartwood").Single().Id;
		s = s.Act(Activations(beast).Single(a => a.SacrificeId == heartwood));
		Resolve(choice =>
			choice switch
			{
				ChooseKeywordChoice k => [k.Options.Single(o => o.DisplayText == "Hexproof").Id],
				_ => [],
			}
		);
		var card = s.GetCard(beast);
		Assert.That(
			(card.PlusOneCounters, s.Has(card, "Hexproof"), s.Has(card, "Trample")),
			Is.EqualTo((1, true, false))
		);
	}

	[Test]
	public void TenuredTethermage_SacrificeALandForTwoTappedHeartwoods_ThenTapTwoForCounters()
	{
		var mage = Hand("Tenured Tethermage", me);
		var land = s.CardsIn(me, ZoneType.Battlefield).First(c => c.Is(CardType.Land)).Id;
		s = s.Act(new CastSpellAction { PlayerId = me, CardId = mage });
		Resolve(choice =>
			choice switch
			{
				ChoosePermanentChoice => [land],
				_ => [],
			}
		);
		var heartwoods = Named(me, "Heartwood").ToList();
		Assert.That(
			(
				s.IsIn(land, me, ZoneType.Graveyard),
				heartwoods.Count,
				heartwoods.All(h => h.IsTapped)
			),
			Is.EqualTo((true, 2, true))
		);
		Assert.That(Activations(mage), Is.Empty, "both are tapped");
		foreach (var h in heartwoods)
			s = s.UpdateObject(h.Id, h with { IsTapped = false }); // TEST SETUP: next turn's untap
		s = s.Act(s.DistinctMoves(Activations(mage)).Single()); // the two Heartwoods, either clicked first
		Resolve();
		Assert.That(
			(s.GetCard(mage).PlusOneCounters, Named(me, "Heartwood").All(h => h.IsTapped)),
			Is.EqualTo((2, true))
		);
	}

	[Test]
	public void GideonsMemorial_TokensGetPlusOneAndVigilance()
	{
		Field(CardRegistry.Get("Gideon's Memorial"), me);
		Cast(
			Hand(
				TestGames.Inline(
					"Make",
					"{1}",
					"Sorcery",
					null,
					new CreateTokens(FraCards.Cadet, 1)
				),
				me
			),
			[]
		);
		var cadet = Named(me, "Cadet").Single();
		var bear = Field(TestGames.Creature("Bear", "2/2"), me);
		Assert.That((s.Power(cadet), s.Has(cadet, "Vigilance")), Is.EqualTo((2 + 1, true)));
		Assert.That(s.Power(s.GetCard(bear)), Is.EqualTo(2), "tokens only");
	}

	[Test]
	public void GideonsMemorial_ItsManaPaysOnlyForPlaneswalkerSpells()
	{
		Field(CardRegistry.Get("Gideon's Memorial"), me);
		TapAllLands(me);
		var walker = Hand(TestGames.Planeswalker("Walker", 3), me); // costs {1}
		var other = Hand(TestGames.Inline("Other", "{1}", "Sorcery"), me);
		Assert.That((Casts(walker).Any(), Casts(other).Any()), Is.EqualTo((true, false)));
	}

	[Test]
	public void GideonsMemorial_DiscardedForFourDamageToAnAttacker()
	{
		var memorial = Hand("Gideon's Memorial", me);
		var bear = Field(TestGames.Creature("Bear", "2/5"), me);
		var idle = Field(TestGames.Creature("Idle", "2/5"), me);
		s = TestGames.PassUntil(s, x => x.IsWaitingForChoice);
		while (s.GetPendingChoice() is DeclareAttackerChoice declare)
			s = s.Choose([declare.CreatureId == bear ? them : CombatOption.None]);
		Assert.That(
			Activations(memorial).Select(a => a.Targets[0]),
			Does.Contain(bear).And.Not.Contain(idle)
		);
		s = s.Act(Activations(memorial).Single(a => a.Targets[0] == bear));
		Resolve();
		Assert.That(s.GetCard(bear).Damage, Is.EqualTo(4));
	}

	CardDefinition TreasureToken => new(Tokens.Treasure, [Tokens.SacrificeForMana(1)]);

	static CardDefinition HeartwoodToken =>
		new(
			FraCards.Heartwood,
			[new ManaAbilityComponent(ManaColor.Red), new ManaAbilityComponent(ManaColor.Green)]
		);

	[Test]
	public void ASummoningSickSculptureTreasure_ConvokesWithoutBeingSacrificed()
	{
		var sculpture = Field(
			new CardDefinition(FraCards.SculptureTreasure, [Tokens.SacrificeForMana(1)]),
			me
		);
		s = s.UpdateObject(
			sculpture,
			s.GetCard(sculpture) with
			{
				ControlledSinceTurn = s.GetGame().TurnNumber,
			}
		); // TEST SETUP: just arrived
		var winter = Hand("Winter, Team Player", me); // {4}{R}, convoke
		Field(
			TestGames.Creature("Red", "1/1") with
			{
				Data = TestGames.Creature("Red", "1/1").Data with { Colors = ["R"] },
			},
			me
		);
		for (var i = 0; i < 3; i++)
			Field(TestGames.Creature("Grey", "1/1"), me);
		TapAllLands(me);
		s = s.Act(Casts(winter).Single());
		Assert.That(
			(Alive(sculpture), s.GetCard(sculpture).IsTapped),
			Is.EqualTo((true, true)),
			"tapped to convoke, not sacrificed"
		);
	}

	[Test]
	public void ATreasureCantPayBothTheSacrificeAndTheMana()
	{
		var beast = Field(CardRegistry.Get("Hungering Puppetbeast"), me); // {1}, Sacrifice another artifact
		Field(TreasureToken, me);
		TapAllLands(me);
		Assert.That(Activations(beast), Is.Empty, "the only artifact is the only mana");
	}

	[Test]
	public void TappingArtifactsForACost_TheyArentAlsoTheMana()
	{
		var engine = TestGames.Creature("Engine", "1/1");
		var engineId = Field(
			engine with
			{
				Components =
				[
					new ActivatedAbilityComponent(
						ManaCost.Parse("{1}"),
						[new AddCounters(Ref.Source, 1)]
					)
					{
						Costs = [new TapUntappedCost(2, MagicCore.Filters.Is.Artifact)],
					},
				],
			},
			me
		);
		Field(HeartwoodToken, me);
		Field(HeartwoodToken, me);
		TapAllLands(me);
		Assert.That(Activations(engineId), Is.Empty, "two to tap and one for {1}: three needed");

		Field(HeartwoodToken, me);
		s = s.Act(Activations(engineId).First()); // each pair of the three is its own choice
		Resolve();
		Assert.That(
			(s.GetCard(engineId).PlusOneCounters, Named(me, "Heartwood").Count(h => h.IsTapped)),
			Is.EqualTo((1, 3))
		);
	}

	[Test]
	public void AReadySculptureTreasure_ConvokesToo_RatherThanBeingSacrificed()
	{
		var sculpture = Field(
			new CardDefinition(FraCards.SculptureTreasure, [Tokens.SacrificeForMana(1)]),
			me
		);
		var winter = Hand("Winter, Team Player", me); // {4}{R}, convoke
		Field(
			TestGames.Creature("Red", "1/1") with
			{
				Data = TestGames.Creature("Red", "1/1").Data with { Colors = ["R"] },
			},
			me
		);
		for (var i = 0; i < 3; i++)
			Field(TestGames.Creature("Grey", "1/1"), me);
		TapAllLands(me);
		s = s.Act(Casts(winter).Single());
		Assert.That(Alive(sculpture), "convoked: tapped, still here");
	}
}
