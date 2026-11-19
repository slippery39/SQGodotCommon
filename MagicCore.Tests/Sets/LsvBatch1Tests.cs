using ImmutableGameObjects;
using MagicCore.Actions;
using MagicCore.Cards;
using MagicCore.Effects;
using MagicCore.Sets;
using MagicCore.Turns;
using Nu = NUnit.Framework.Is;

namespace MagicCore.Tests.Sets;

/// <summary>
/// MC-4.3a, LSVPower's batch 1 — power and mana: each card's ability FIRES, against its PRINTED text, through
/// real tapping and casting; and each new rule (reminder text, a mana ability's damage and life, the
/// composed mana restriction, the coin flip) with the case that tells it from its absence.
/// </summary>
public class LsvBatch1Tests : FraBoardTest
{
	int Pool(ManaColor color) => s.GetPlayer(me).ManaPool[color];

	void TapForMana(int card, ManaColor color) =>
		s = s.Act(
			new TapForManaAction
			{
				PlayerId = me,
				CardId = card,
				Color = color,
			}
		);

	bool CanTapFor(int card, ManaColor color) =>
		new TapForManaAction
		{
			PlayerId = me,
			CardId = card,
			Color = color,
		}
			.ValidateAdd(s)
			.IsValid;

	/// <summary>Every land of mine tapped: what pays now is only what the test puts down after.</summary>
	void TapMyLands()
	{
		foreach (
			var land in s.CardsIn(me, ZoneType.Battlefield).Where(c => c.Is(CardType.Land)).ToList()
		)
			s = s.Tap(land.Id);
	}

	int InLibrary(CardDefinition card)
	{
		int id;
		(s, id) = TestGames.Put(s, me, card, ZoneType.Library);
		return id;
	}

	// ===== The reminder-text rule: the original duals =====

	[Test]
	public void Tundra_ReminderTextOnly_IsImplemented_AndTapsForItsTypes()
	{
		Assert.That(CardRegistry.IsImplemented("Tundra"), "only reminder text: implemented");
		Assert.That(
			CardRegistry.IsImplemented("Indatha Triome"),
			Nu.False,
			"reminder text beside a real line is not enough"
		);
		var tundra = Field(Lsv("Tundra"), me);
		Assert.That(CanTapFor(tundra, ManaColor.White) && CanTapFor(tundra, ManaColor.Blue));
		TapForMana(tundra, ManaColor.Blue);
		Assert.That(Pool(ManaColor.Blue), Nu.EqualTo(1));
	}

	// ===== Plain mana: the Moxen, Sol Ring, the mana creatures =====

	[TestCase("Mox Pearl", ManaColor.White, 1)]
	[TestCase("Mox Sapphire", ManaColor.Blue, 1)]
	[TestCase("Mox Jet", ManaColor.Black, 1)]
	[TestCase("Mox Ruby", ManaColor.Red, 1)]
	[TestCase("Mox Emerald", ManaColor.Green, 1)]
	[TestCase("Sol Ring", ManaColor.Colorless, 2)]
	[TestCase("Llanowar Elves", ManaColor.Green, 1)]
	[TestCase("Avacyn's Pilgrim", ManaColor.White, 1)]
	[TestCase("Birds of Paradise", ManaColor.Red, 1)]
	[TestCase("Noble Hierarch", ManaColor.Blue, 1)]
	[TestCase("Ignoble Hierarch", ManaColor.Black, 1)]
	[TestCase("Grim Monolith", ManaColor.Colorless, 3)]
	[TestCase("Basalt Monolith", ManaColor.Colorless, 3)]
	[TestCase("Mana Vault", ManaColor.Colorless, 3)]
	[TestCase("Mana Crypt", ManaColor.Colorless, 2)]
	[TestCase("City of Traitors", ManaColor.Colorless, 2)]
	[TestCase("Strip Mine", ManaColor.Colorless, 1)]
	[TestCase("Wasteland", ManaColor.Colorless, 1)]
	public void TapsFor(string name, ManaColor color, int amount)
	{
		var card = Field(Lsv(name), me);
		TapForMana(card, color);
		Assert.That(Pool(color), Nu.EqualTo(amount));
		Assert.That(Life(me), Nu.EqualTo(20), "no damage");
	}

	[TestCase("Black Lotus", 3)]
	[TestCase("Lotus Petal", 1)]
	public void Lotus_SacrificedForMana(string name, int amount)
	{
		var lotus = Field(Lsv(name), me);
		TapForMana(lotus, ManaColor.Red);
		Assert.That(Pool(ManaColor.Red), Nu.EqualTo(amount));
		Assert.That(s.IsIn(lotus, me, ZoneType.Graveyard), "sacrificed");
	}

	[Test]
	public void GaeasCradle_AndTolarianAcademy_CountTheirPermanents()
	{
		Field(TestGames.Creature("Bear", "2/2"), me);
		Field(TestGames.Creature("Bear", "2/2"), me);
		Field(TestGames.Creature("Theirs", "2/2"), them);
		var cradle = Field(Lsv("Gaea's Cradle"), me);
		TapForMana(cradle, ManaColor.Green);
		Assert.That(Pool(ManaColor.Green), Nu.EqualTo(2), "a G for each creature you control");

		Field(Lsv("Mox Pearl"), me);
		var academy = Field(Lsv("Tolarian Academy"), me);
		TapForMana(academy, ManaColor.Blue);
		Assert.That(Pool(ManaColor.Blue), Nu.EqualTo(1), "a U for each artifact you control");
	}

	// ===== A mana ability's damage to you: the Talismans, Ancient Tomb =====

	[Test]
	public void Talisman_ItsColoursDealOneDamage_ItsColorlessNone()
	{
		var talisman = Field(Lsv("Talisman of Conviction"), me);
		TapForMana(talisman, ManaColor.Colorless);
		Assert.That(Life(me), Nu.EqualTo(20), "{T}: Add {C} — no damage");

		var other = Field(Lsv("Talisman of Progress"), me);
		TapForMana(other, ManaColor.Blue);
		Assert.That(
			(Pool(ManaColor.Blue), Life(me)),
			Nu.EqualTo((1, 19)),
			"{T}: Add {W} or {U}. This artifact deals 1 damage to you."
		);
	}

	[Test]
	public void Talisman_PayingForASpell_DealsItsDamage()
	{
		TapMyLands();
		var talisman = Field(Lsv("Talisman of Indulgence"), me);
		var spell = Hand(TestGames.CreatureCosting("{R}", "Goblin", "1/1"), me);
		Cast(spell, []);
		Assert.That(s.IsOnBattlefield(spell), "the Talisman paid its {R}");
		Assert.That(
			(s.GetCard(talisman).IsTapped, Life(me)),
			Nu.EqualTo((true, 19)),
			"and dealt 1 damage doing it"
		);
	}

	[Test]
	public void AncientTomb_TwoColorless_TwoDamage()
	{
		var tomb = Field(Lsv("Ancient Tomb"), me);
		TapForMana(tomb, ManaColor.Colorless);
		Assert.That((Pool(ManaColor.Colorless), Life(me)), Nu.EqualTo((2, 18)));
	}

	// ===== Pay life: Mana Confluence, the fetchlands =====

	[Test]
	public void ManaConfluence_PaysOneLife_AndNotWithoutIt()
	{
		var confluence = Field(Lsv("Mana Confluence"), me);
		TapForMana(confluence, ManaColor.Green);
		Assert.That((Pool(ManaColor.Green), Life(me)), Nu.EqualTo((1, 19)));

		var second = Field(Lsv("Mana Confluence"), me);
		s = s.WithPlayer(s.GetPlayer(me) with { Life = 0 });
		Assert.That(CanTapFor(second, ManaColor.Green), Nu.False, "no life to pay");
	}

	[Test]
	public void ManaConfluence_TwoPayingOneSpell_EachPaysItsLife()
	{
		TapMyLands();
		Field(Lsv("Mana Confluence"), me);
		Field(Lsv("Mana Confluence"), me);
		s = s.WithPlayer(s.GetPlayer(me) with { Life = 1 });
		var spell = Hand(TestGames.CreatureCosting("{G}{G}", "Bear", "2/2"), me);
		s = s.Act(new CastSpellAction { PlayerId = me, CardId = spell });
		Assert.That(
			Life(me),
			Nu.EqualTo(1 - 2),
			"the solver asks each against the life alone (a known ceiling) — but neither makes free mana"
		);
	}

	[Test]
	public void FloodedStrand_PaysLife_SacrificesItself_FetchesAnIsland()
	{
		var strand = Field(Lsv("Flooded Strand"), me);
		InLibrary(TestGames.Card("Mountain"));
		var island = InLibrary(TestGames.Card("Island"));
		var activation = Activations(strand).Single();
		s = s.Act(activation);
		Resolve(On<ChooseFromLibraryChoice>(island));
		Assert.That(s.IsOnBattlefield(island), "a Plains or Island card onto the battlefield");
		Assert.That(
			(s.IsIn(strand, me, ZoneType.Graveyard), Life(me)),
			Nu.EqualTo((true, 19)),
			"Pay 1 life, Sacrifice this land"
		);
	}

	[Test]
	public void PrismaticVista_OffersOnlyBasics()
	{
		var vista = Field(Lsv("Prismatic Vista"), me);
		InLibrary(Lsv("Tundra"));
		InLibrary(TestGames.Card("Forest"));
		s = s.Act(Activations(vista).Single());
		s = TestGames.PassUntil(s, x => x.IsWaitingForChoice);
		Assert.That(
			s.GetPendingChoice()!.Options.Select(o => s.GetCard(o.Id).Name), // one option per name
			Has.Member("Forest").And.No.Member("Tundra")
		);
	}

	// ===== The composed mana restriction: Mishra's Workshop =====

	[Test]
	public void MishrasWorkshop_CastsArtifactSpells_NotOtherSpellsOrAbilities()
	{
		TapMyLands();
		var workshop = Field(Lsv("Mishra's Workshop"), me);
		var monolith = Field(Lsv("Basalt Monolith"), me);
		s = s.Tap(monolith);
		var bear = Hand(TestGames.Creature("Bear", "2/2"), me);
		Assert.That(Casts(bear), Nu.Empty, "not a creature spell");
		Assert.That(
			Activations(monolith),
			Nu.Empty,
			"{3}: untap — an artifact's ability: spells only"
		);
		var trinket = Hand(TestGames.Inline("Trinket", "{3}", "Artifact"), me);
		Cast(trinket, []);
		Assert.That(
			(s.IsOnBattlefield(trinket), s.GetCard(workshop).IsTapped),
			Nu.EqualTo((true, true)),
			"an artifact spell, paid by the Workshop"
		);
	}

	// ===== Rituals =====

	[Test]
	public void DarkRitual_AddsBBB()
	{
		Cast(Hand(Lsv("Dark Ritual"), me), []);
		Assert.That(Pool(ManaColor.Black), Nu.EqualTo(3));
	}

	[Test]
	public void CabalRitual_FiveWithThreshold_ThreeWithout()
	{
		Cast(Hand(Lsv("Cabal Ritual"), me), []);
		Assert.That(Pool(ManaColor.Black), Nu.EqualTo(3));
		for (var i = 0; i < 7; i++)
			(s, _) = TestGames.Put(s, me, TestGames.Card("Swamp"), ZoneType.Graveyard);
		Cast(Hand(Lsv("Cabal Ritual"), me), []);
		Assert.That(
			Pool(ManaColor.Black),
			Nu.EqualTo(3 - 2 + 5),
			"threshold: BBBBB instead (the cast spent two of the first BBB)"
		);
	}

	// ===== Lands that destroy lands =====

	[Test]
	public void StripMine_DestroysAnyLand_WastelandOnlyANonbasic()
	{
		var plains = s.CardsIn(them, ZoneType.Battlefield).First(c => c.Name == "Plains").Id;
		var tundra = Field(Lsv("Tundra"), them);
		var wasteland = Field(Lsv("Wasteland"), me);
		Assert.That(
			Activations(wasteland).Select(a => a.Targets.Single()),
			Has.Member(tundra).And.No.Member(plains),
			"target nonbasic land"
		);
		s = s.Act(Activations(wasteland).First(a => a.Targets.Single() == tundra));
		Resolve();
		Assert.That(
			(Alive(tundra), s.IsIn(wasteland, me, ZoneType.Graveyard)),
			Nu.EqualTo((false, true))
		);

		var strip = Field(Lsv("Strip Mine"), me);
		s = s.Act(Activations(strip).First(a => a.Targets.Single() == plains));
		Resolve();
		Assert.That(Alive(plains), Nu.False, "target land — a basic too");
	}

	// ===== Untapping: the Monoliths, Mana Vault =====

	[Test]
	public void GrimMonolith_PaysFourToUntap()
	{
		var monolith = Field(Lsv("Grim Monolith"), me);
		TapForMana(monolith, ManaColor.Colorless);
		s = s.Act(Activations(monolith).Single());
		Resolve();
		Assert.That(s.GetCard(monolith).IsTapped, Nu.False, "{4}: Untap this artifact");
	}

	void ToMyNext(Step step) =>
		s = TestGames.PassUntil(
			s,
			x =>
				x.GetGame().ActivePlayerId == me && x.GetGame().Step == step
				|| x.GetPendingChoice() is YesNoChoice
		);

	[Test]
	public void ManaVault_StaysTapped_HurtsInYourDrawStep_UntapsForFour()
	{
		var vault = Field(Lsv("Mana Vault"), me);
		s = s.Tap(vault);
		s = TestGames.PassUntil(s, x => x.GetGame().ActivePlayerId == them);
		ToMyNext(Step.Draw);
		Assert.That(s.GetPendingChoice(), Nu.InstanceOf<YesNoChoice>(), "you may pay {4}");
		s = s.Choose([]); // no
		ToMyNext(Step.PreCombatMain);
		Assert.That(
			(s.GetCard(vault).IsTapped, Life(me)),
			Nu.EqualTo((true, 19)),
			"doesn't untap; tapped at your draw step: 1 damage"
		);

		s = TestGames.PassUntil(s, x => x.GetGame().ActivePlayerId == them);
		ToMyNext(Step.Draw);
		s = s.Choose([1]); // pay {4}
		ToMyNext(Step.PreCombatMain);
		Assert.That(
			(s.GetCard(vault).IsTapped, Life(me)),
			Nu.EqualTo((false, 19)),
			"untapped in the upkeep: no damage"
		);
	}

	// ===== Mana Crypt: the coin flip =====

	[Test]
	public void ManaCrypt_FlipsEachUpkeep_ThreeDamageOnALoss()
	{
		Field(Lsv("Mana Crypt"), me);
		var start = s;
		var losses = Enumerable
			.Range(1, 12)
			.Select(seed =>
			{
				s = start with { RngSeed = seed };
				s = TestGames.PassUntil(s, x => x.GetGame().ActivePlayerId == them);
				ToMyNext(Step.PreCombatMain);
				return 20 - Life(me);
			})
			.ToList();
		Assert.That(losses, Nu.All.EqualTo(0).Or.EqualTo(3));
		Assert.That(losses, Has.Member(0).And.Member(3), "won some flips, lost some");
	}

	// ===== City of Traitors =====

	[Test]
	public void CityOfTraitors_SacrificedWhenYouPlayAnotherLand()
	{
		var city = Hand(Lsv("City of Traitors"), me);
		s = s.Act(new PlayLandAction { PlayerId = me, CardId = city });
		Resolve();
		Assert.That(Alive(city), "playing itself is not another land");

		s = TestGames.PassUntil(s, x => x.GetGame().ActivePlayerId == them);
		ToMyNext(Step.PreCombatMain);
		var forest = Hand(TestGames.Card("Forest"), me);
		s = s.Act(new PlayLandAction { PlayerId = me, CardId = forest });
		Resolve();
		Assert.That(Alive(city), Nu.False);
	}

	// ===== MC-4.3b: counters =====

	static CardDefinition Boon =>
		TestGames.Inline("Boon", "{1}", "Instant", null, new GainLife(Ref.You, 1));

	[TestCase("Counterspell")]
	[TestCase("Mana Leak")]
	[TestCase("Mana Tithe")]
	[TestCase("Spell Pierce")]
	public void Counters_TheirSpell(string name)
	{
		var spell = TheirSpellOnTheStack(Boon);
		Cast(Hand(Lsv(name), me), [], spell); // "unless its controller pays": they don't
		Assert.That(s.IsIn(spell, them, ZoneType.Graveyard), "countered");
		Assert.That(Life(them), Nu.EqualTo(20));
	}

	[Test]
	public void SpellPierce_TargetsOnlyANoncreatureSpell()
	{
		TheirSpellOnTheStack(TestGames.Creature("Bear", "2/2", "Flash"));
		var pierce = Hand(Lsv("Spell Pierce"), me);
		Assert.That(Casts(pierce), Nu.Empty);
	}

	[Test]
	public void MemoryLapse_PutsItOnTopOfItsOwnersLibrary()
	{
		var spell = TheirSpellOnTheStack(Boon);
		Cast(Hand(Lsv("Memory Lapse"), me), [], spell);
		Assert.That(s.CardsIn(them, ZoneType.Library).First().Id, Nu.EqualTo(spell));
	}

	[Test]
	public void Remand_ReturnsItToHand_AndDraws()
	{
		var spell = TheirSpellOnTheStack(Boon);
		var remand = Hand(Lsv("Remand"), me);
		var hand = HandCount(me);
		Cast(remand, [], spell);
		Assert.That(s.IsIn(spell, them, ZoneType.Hand), "into its owner's hand instead");
		Assert.That(HandCount(me), Nu.EqualTo(hand - 1 + 1), "Draw a card.");
	}

	[Test]
	public void SternScolding_CountersASmallCreatureSpell_NotABigOne()
	{
		var big = TheirSpellOnTheStack(TestGames.Creature("Giant", "3/3", "Flash"));
		var scolding = Hand(Lsv("Stern Scolding"), me);
		Assert.That(Casts(scolding), Nu.Empty, "power and toughness both 3");
		Resolve();
		Assert.That(Alive(big));

		var small = TheirSpellOnTheStack(TestGames.Creature("Wall", "0/4", "Flash"));
		Cast(scolding, [], small);
		Assert.That(s.IsIn(small, them, ZoneType.Graveyard), "power 2 or less");
	}

	// ===== Burn and removal =====

	[Test]
	public void LightningBolt_ThreeToAnyTarget()
	{
		Cast(Hand(Lsv("Lightning Bolt"), me), [], them);
		Assert.That(Life(them), Nu.EqualTo(17));
	}

	[Test]
	public void BurstLightning_TwoOrKickedFour()
	{
		Cast(Hand(Lsv("Burst Lightning"), me), [], them);
		Assert.That(Life(them), Nu.EqualTo(18));
		var kicked = Hand(Lsv("Burst Lightning"), me);
		s = s.Act(Casts(kicked).First(c => c.Kicked && c.Targets.Single() == them));
		Resolve();
		Assert.That(Life(them), Nu.EqualTo(18 - 4));
	}

	[Test]
	public void SwordsToPlowshares_ExilesIt_ItsControllerGainsItsPower()
	{
		var giant = Field(TestGames.Creature("Giant", "3/3"), them);
		Cast(Hand(Lsv("Swords to Plowshares"), me), [], giant);
		Assert.That((s.IsIn(giant, them, ZoneType.Exile), Life(them)), Nu.EqualTo((true, 23)));
	}

	[TestCase("Path to Exile", ZoneType.Exile)]
	[TestCase("Erode", ZoneType.Graveyard)]
	public void PathAndErode_ItsControllerMayFetchABasicTapped(string name, ZoneType to)
	{
		var giant = Field(TestGames.Creature("Giant", "3/3"), them);
		var lands = s.CardsIn(them, ZoneType.Battlefield).Count(c => c.Is(CardType.Land));
		Cast(Hand(Lsv(name), me), [], giant);
		Assert.That(s.IsIn(giant, them, to));
		Assert.That(
			s.CardsIn(them, ZoneType.Battlefield).Count(c => c.Is(CardType.Land)),
			Nu.EqualTo(lands),
			"they may decline"
		);

		var other = Field(TestGames.Creature("Giant", "3/3"), them);
		var again = Hand(Lsv(name), me);
		s = s.Act(Casts(again).First(c => c.Targets.Single() == other));
		Resolve(c =>
			c switch
			{
				ChooseFromLibraryChoice => c.Options.Take(1).Select(o => o.Id),
				_ => c.Options.Take(c.MinChoices).Select(o => o.Id),
			}
		);
		var fetched = s.CardsIn(them, ZoneType.Battlefield)
			.Where(c => c.Is(CardType.Land))
			.ToList();
		Assert.That(fetched.Count, Nu.EqualTo(lands + 1), "a basic land card onto the battlefield");
		Assert.That(fetched.Last().IsTapped, "tapped");
	}

	[Test]
	public void PathToExile_AStolenCreature_ItsControllerFetches()
	{
		var stolen = Field(TestGames.Creature("Giant", "3/3"), me);
		s = s.GainControl(stolen, them); // TEST SETUP: mine, controlled by them
		var mine = s.CardsIn(me, ZoneType.Battlefield).Count(c => c.Is(CardType.Land));
		var theirs = s.CardsIn(them, ZoneType.Battlefield).Count(c => c.Is(CardType.Land));
		var path = Hand(Lsv("Path to Exile"), me);
		s = s.Act(Casts(path).First(c => c.Targets.Single() == stolen));
		Resolve(c =>
			c switch
			{
				ChooseFromLibraryChoice => c.Options.Take(1).Select(o => o.Id),
				_ => c.Options.Take(c.MinChoices).Select(o => o.Id),
			}
		);
		Assert.That(s.IsIn(stolen, me, ZoneType.Exile), "to its owner's exile");
		Assert.That(
			(
				s.CardsIn(me, ZoneType.Battlefield).Count(c => c.Is(CardType.Land)),
				s.CardsIn(them, ZoneType.Battlefield).Count(c => c.Is(CardType.Land))
			),
			Nu.EqualTo((mine, theirs + 1)),
			"its controller searches, not its owner"
		);
	}

	[Test]
	public void CutDown_TotalPowerAndToughnessFiveOrLess()
	{
		var small = Field(TestGames.Creature("Bear", "2/3"), them);
		var big = Field(TestGames.Creature("Giant", "3/3"), them);
		var cut = Hand(Lsv("Cut Down"), me);
		Assert.That(
			Casts(cut).Select(c => c.Targets.Single()),
			Has.Member(small).And.No.Member(big)
		);
		Cast(cut, [], small);
		Assert.That(Alive(small), Nu.False);
	}

	[Test]
	public void Vindicate_DestroysAnyPermanent()
	{
		var plains = s.CardsIn(them, ZoneType.Battlefield).First(c => c.Name == "Plains").Id;
		Cast(Hand(Lsv("Vindicate"), me), [], plains);
		Assert.That(Alive(plains), Nu.False);
	}

	[Test]
	public void WrathOfGod_DestroysEveryCreature()
	{
		var mine = Field(TestGames.Creature("Bear", "2/2"), me);
		var theirs = Field(TestGames.Creature("Giant", "3/3"), them);
		Cast(Hand(Lsv("Wrath of God"), me), []);
		Assert.That((Alive(mine), Alive(theirs)), Nu.EqualTo((false, false)));
	}

	[Test]
	public void AbruptDecay_NonlandManaValueThreeOrLess_CantBeCountered()
	{
		var cheap = Field(Lsv("Basalt Monolith"), them);
		var dear = Field(TestGames.CreatureCosting("{4}", "Ogre", "4/4"), them);
		var decay = Hand(Lsv("Abrupt Decay"), me);
		var plains = s.CardsIn(them, ZoneType.Battlefield).First(c => c.Name == "Plains").Id;
		Assert.That(
			Casts(decay).Select(c => c.Targets.Single()),
			Has.Member(cheap).And.No.Member(plains).And.No.Member(dear)
		);
		s = s.Act(Casts(decay).First(c => c.Targets.Single() == cheap));
		var counter = Hand(Lsv("Counterspell"), them);
		s = s.Act(new PassPriorityAction { PlayerId = me });
		s = s.Act(
			new CastSpellAction
			{
				PlayerId = them,
				CardId = counter,
				Targets = [decay],
			}
		);
		Resolve();
		Assert.That(Alive(cheap), Nu.False, "This spell can't be countered.");
		Assert.That(Alive(dear));
	}

	[Test]
	public void Abrade_ThreeDamageToACreature_OrDestroyAnArtifact()
	{
		var giant = Field(TestGames.Creature("Giant", "3/3"), them);
		var mox = Field(Lsv("Mox Pearl"), them);
		Cast(Hand(Lsv("Abrade"), me), [0], giant);
		Cast(Hand(Lsv("Abrade"), me), [1], mox);
		Assert.That((Alive(giant), Alive(mox)), Nu.EqualTo((false, false)));
	}

	[Test]
	public void Pyroblast_HitsOnlyWhatIsBlue()
	{
		var blue = Field(Lsv("Faerie Mastermind"), them);
		var red = Field(Lsv("Goblin Welder"), them);
		Cast(Hand(Lsv("Pyroblast"), me), [1], red);
		Assert.That(Alive(red), "a legal target, but not blue: nothing");
		Cast(Hand(Lsv("Pyroblast"), me), [1], blue);
		Assert.That(Alive(blue), Nu.False);

		var spell = TheirSpellOnTheStack(Lsv("Brainstorm"));
		Cast(Hand(Lsv("Pyroblast"), me), [0], spell);
		Assert.That(s.IsIn(spell, them, ZoneType.Graveyard), "counter target spell if it's blue");
	}

	// ===== Turns and cards =====

	[Test]
	public void TimeWalk_YouTakeAnExtraTurn_TimeWarp_TargetPlayerDoes()
	{
		Cast(Hand(Lsv("Time Walk"), me), []);
		Assert.That(s.GetGame().ExtraTurns, Nu.EqualTo(new[] { me }));
		Cast(Hand(Lsv("Time Warp"), me), [], them);
		Assert.That(s.GetGame().ExtraTurns, Nu.EqualTo(new[] { me, them }));
	}

	[Test]
	public void AncestralRecall_TargetPlayerDrawsThree()
	{
		var hand = HandCount(them);
		Cast(Hand(Lsv("Ancestral Recall"), me), [], them);
		Assert.That(HandCount(them), Nu.EqualTo(hand + 3));
	}

	[Test]
	public void Brainstorm_DrawThree_PutTwoBackOnTop_InTheOrderChosen()
	{
		var first = Hand(TestGames.Card("Plains"), me);
		var second = Hand(TestGames.Card("Island"), me);
		var brainstorm = Hand(Lsv("Brainstorm"), me);
		var hand = HandCount(me);
		var picks = new Queue<int>([first, second]);
		s = s.Act(new CastSpellAction { PlayerId = me, CardId = brainstorm });
		Resolve(c =>
			c switch
			{
				ChooseCardsFromHandChoice => [picks.Dequeue()],
				_ => c.Options.Take(c.MinChoices).Select(o => o.Id),
			}
		);
		Assert.That(HandCount(me), Nu.EqualTo(hand - 1 + 3 - 2));
		Assert.That(
			s.CardsIn(me, ZoneType.Library).Take(2).Select(c => c.Id),
			Nu.EqualTo(new[] { second, first }),
			"the second put back is on top"
		);
	}

	// ===== Exalted =====

	[Test]
	public void NobleHierarch_Exalted_PumpsALoneAttacker()
	{
		var hierarch = Field(Lsv("Noble Hierarch"), me);
		var bear = Field(TestGames.Creature("Bear", "2/2"), me);
		s = TestGames.PassUntil(s, x => x.GetPendingChoice() is DeclareAttackerChoice);
		while (s.GetPendingChoice() is DeclareAttackerChoice d)
			s = s.Choose([d.CreatureId == bear ? them : 0]); // 0: doesn't attack
		Resolve();
		Assert.That(
			(s.Power(s.GetCard(bear)), s.Toughness(s.GetCard(bear))),
			Nu.EqualTo((3, 3)),
			"attacks alone: +1/+1"
		);
		Assert.That(Alive(hierarch));
	}
}
