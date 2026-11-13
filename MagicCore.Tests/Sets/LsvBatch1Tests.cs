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
	static CardDefinition Lsv(string name) => CardRegistry.Get(name, "LSV");

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
