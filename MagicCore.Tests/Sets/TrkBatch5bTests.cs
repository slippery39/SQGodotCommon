using ImmutableGameObjects;
using MagicCore.Actions;
using MagicCore.Cards;
using MagicCore.Effects;
using MagicCore.Sets;
using MagicCore.Targeting;
using MagicCore.Turns;
using Is = MagicCore.Filters.Is;
using Nu = NUnit.Framework.Is;

namespace MagicCore.Tests.Sets;

/// <summary>
/// MC-5.3 batch 5b: TRK's tier-3 rares and mythics through real casting — a delayed return on the
/// card that died (Dr. McCoy), charge counters doubled (Geordi La Forge), a mana ability that costs
/// mana (Starfleet Academy), tap three or seven chosen as a set (Collation Trimatrix, Captain Picard),
/// the life lost this turn (Gul Dukat), base P/T for good (Seven of Nine) and reveal the top X
/// (Branching Timeline).
/// </summary>
public class TrkBatch5bTests : FraBoardTest
{
	static CardDefinition Trk(string name) => CardRegistry.Get(name, "TRK");

	static CardDefinition Bear => TestGames.Creature("Bear", "2/2");

	static CardDefinition Officer => TestCards.Typed(Bear, "Creature — Human Officer");

	static CardDefinition Drone => TestCards.Typed(Bear, "Artifact Creature — Golem");

	static CardDefinition Spacecraft => TestCards.Typed(TestCards.Trinket, "Artifact — Spacecraft");

	void CastAndResolve(int card, Func<ChoiceAction, IEnumerable<int>>? answer = null)
	{
		s = s.Act(Casts(card).First());
		Resolve(answer);
	}

	void Activate(
		ActivateAbilityAction activation,
		Func<ChoiceAction, IEnumerable<int>>? answer = null
	)
	{
		s = s.Act(activation);
		Resolve(answer);
	}

	void ToTurn(int turn) => s = TestGames.PassUntil(s, x => x.GetGame().TurnNumber == turn);

	int OnTop(CardDefinition card, int player)
	{
		var id = Hand(card, player);
		s = s.MoveCard(id, s.ZoneId(player, ZoneType.Library), toTop: true);
		return id;
	}

	void NoLands(int player)
	{
		foreach (
			var land in s.CardsIn(player, ZoneType.Battlefield)
				.Where(c => c.Is(CardType.Land))
				.ToList()
		)
			s = s.MoveCard(land.Id, s.ZoneId(player, ZoneType.Library));
	}

	// ===== Dr. McCoy: a delayed trigger on the card that died =====

	[Test]
	public void McCoy_TheCreatureThatDiesThisTurn_ReturnsAtTheEndStep_WithACounter()
	{
		var bear = Field(Bear, them);
		var mccoy = Hand(Trk("Dr. Leonard \"Bones\" McCoy"), me);
		CastAndResolve(
			mccoy,
			c =>
				c switch
				{
					ChooseModesChoice => [1],
					ChooseTargetChoice => [bear],
					_ => c.Options.Take(c.MinChoices).Select(o => o.Id),
				}
		);
		var kill = Hand(TestCards.Kill(), me);
		Cast(kill, [], bear);
		Assert.That(s.IsIn(bear, them, ZoneType.Graveyard), Nu.True);
		ToTurn(s.GetGame().TurnNumber + 1);
		Assert.That(s.IsIn(bear, them, ZoneType.Battlefield), Nu.True, "under its OWNER's control");
		Assert.That(s.GetCard(bear).PlusOneCounters, Nu.EqualTo(1));
	}

	[Test]
	public void McCoy_FirstMode_ReturnsAPermanentCard_NeverADoctor()
	{
		var doctor = Hand(TestCards.Typed(Bear, "Creature — Human Doctor"), me);
		s = s.MoveCard(doctor, s.ZoneId(me, ZoneType.Graveyard));
		var land = Hand(TestGames.Card("Plains"), me);
		s = s.MoveCard(land, s.ZoneId(me, ZoneType.Graveyard));
		var offered = new List<int>();
		CastAndResolve(
			Hand(Trk("Dr. Leonard \"Bones\" McCoy"), me),
			c =>
				c switch
				{
					ChooseModesChoice => [0],
					ChooseTargetChoice t => offered.Also(t.Options.Select(o => o.Id)).Take(1),
					_ => c.Options.Take(c.MinChoices).Select(o => o.Id),
				}
		);
		Assert.That(offered, Nu.EquivalentTo(new[] { land }));
		Assert.That(s.IsIn(land, me, ZoneType.Hand), Nu.True);
	}

	// ===== Geordi La Forge: charge counters doubled, entering too =====

	[Test]
	public void Geordi_DoublesChargeCountersOnYourArtifact_NotTheirs()
	{
		Field(Trk("Lt. Cmdr. Geordi La Forge"), me);
		var mine = Field(Spacecraft, me);
		var theirs = Field(Spacecraft, them);
		s = s.PutCounters(mine, CounterKinds.Charge, 3);
		s = s.PutCounters(theirs, CounterKinds.Charge, 3);
		Assert.That(s.GetCard(mine).ChargeCounters, Nu.EqualTo(6));
		Assert.That(s.GetCard(theirs).ChargeCounters, Nu.EqualTo(3));
	}

	// ===== Captain Picard: tap seven Officers, a Spacecraft with seven charge counters =====

	[Test]
	public void Picard_TapsSevenOfficers_ForASpacecraftWithSevenCounters_FourteenWithGeordi()
	{
		var picard = Field(Trk("Captain Jean-Luc Picard"), me);
		var officers = Enumerable.Range(0, 6).Select(_ => Field(Officer, me)).ToList();
		Field(Trk("Lt. Cmdr. Geordi La Forge"), me);
		var ship = OnTop(Spacecraft, me);
		var activation = Activations(picard).Single(a => !a.TapIds.IsEmpty);
		Assert.That(
			activation.TapIds,
			Nu.EquivalentTo(officers.Append(picard)),
			"Picard is an Officer too"
		);
		Activate(
			activation,
			c =>
				c switch
				{
					ChooseFromLibraryChoice => [ship],
					_ => [],
				}
		);
		Assert.That(s.IsIn(ship, me, ZoneType.Battlefield), Nu.True);
		Assert.That(s.GetCard(ship).ChargeCounters, Nu.EqualTo(14));
		Assert.That(officers.All(o => s.GetCard(o).IsTapped), Nu.True);
	}

	[Test]
	public void Picard_ACreatureOrSpacecraftSpell_MakesAnOfficer()
	{
		Field(Trk("Captain Jean-Luc Picard"), me);
		CastAndResolve(Hand(Bear, me));
		CastAndResolve(Hand(Spacecraft, me));
		CastAndResolve(Hand(TestCards.Trinket, me));
		Assert.That(s.CardsIn(me, ZoneType.Battlefield).Count(c => c.IsToken), Nu.EqualTo(2));
	}

	// ===== Collation Trimatrix: tap three, the set the player's choice =====

	[Test]
	public void Trimatrix_OneActionPerSetOfThree_DrawsAndLosesOne()
	{
		var trimatrix = Field(Trk("Collation Trimatrix"), me);
		var drones = Enumerable.Range(0, 3).Select(_ => Field(Drone, me)).ToList();
		var sets = Activations(trimatrix).Select(a => a.TapIds).ToList();
		Assert.That(sets, Has.Count.EqualTo(4), "four choose three");
		Assert.That(
			sets.Select(t => string.Join(",", t.Order())).Distinct().Count(),
			Nu.EqualTo(4)
		);
		var hand = HandCount(me);
		var life = Life(me);
		var chosen = sets.Single(t => !t.Contains(trimatrix));
		Activate(Activations(trimatrix).Single(a => a.TapIds.SequenceEqual(chosen)));
		Assert.That(drones.All(d => s.GetCard(d).IsTapped), Nu.True);
		Assert.That(s.GetCard(trimatrix).IsTapped, Nu.False);
		Assert.That((HandCount(me), Life(me)), Nu.EqualTo((hand + 1, life - 1)));
	}

	[Test]
	public void Trimatrix_ASetWithTheSameCreatureTwice_IsRefused()
	{
		var trimatrix = Field(Trk("Collation Trimatrix"), me);
		var drone = Field(Drone, me);
		Field(Drone, me);
		var twice = Activations(trimatrix).First() with { TapIds = [trimatrix, drone, drone] };
		Assert.That(twice.ValidateAdd(s).IsValid, Nu.False);
	}

	// ===== Starfleet Academy: a mana ability that costs mana =====

	[Test]
	public void StarfleetAcademy_PaysFiveForSixOfAColour_NeverWithItsOwnMana()
	{
		NoLands(me);
		var academy = Field(Trk("Starfleet Academy"), me);
		var plains = Enumerable
			.Range(0, 4)
			.Select(_ => Field(TestGames.Card("Plains"), me))
			.ToList();
		Field(
			TestCards.Typed(Bear, "Creature — Human Vulcan Scientist Officer Doctor Android"),
			me
		); // Federation 6: with its own mana, four Plains would do
		var green = Hand(TestGames.CreatureCosting("{G}{G}{G}", "Green", "3/3"), me);
		Assert.That(Casts(green), Nu.Empty, "four Plains can't pay its {5}");
		plains.Add(Field(TestGames.Card("Plains"), me));
		CastAndResolve(green);
		Assert.That(s.IsOnBattlefield(green), Nu.True);
		Assert.That(plains.Append(academy).All(id => s.GetCard(id).IsTapped), Nu.True);
	}

	[Test]
	public void StarfleetAcademy_ItsManaCountsForX()
	{
		NoLands(me);
		Field(Trk("Starfleet Academy"), me);
		for (var i = 0; i < 5; i++)
			Field(TestGames.Card("Plains"), me);
		Field(
			TestCards.Typed(
				Bear,
				"Creature — Human Vulcan Scientist Officer Doctor Android Klingon Romulan Bajoran Cardassian"
			),
			me
		); // Federation 10: five Plains pay {5}, ten green
		var hydra = Hand(TestGames.CreatureCosting("{X}{G}", "Hydra", "0/0"), me);
		Assert.That(Casts(hydra).Max(c => c.X), Nu.EqualTo(9));
	}

	[Test]
	public void StarfleetAcademy_IsNoFixerForTheSealedBuilder() =>
		Assert.That(
			global::MagicCore.Limited.SealedBuilder.Fixes("Starfleet Academy"),
			Has.Count.LessThan(5)
		);

	// ===== Gul Dukat: the life lost this turn =====

	[Test]
	public void GulDukat_AtTheEndStep_TheyLoseWhatTheyLost_AndIGainIt()
	{
		Field(Trk("Gul Dukat, Colonial Prefect"), me);
		var opponent = TargetSpec.Player(Is.Opponents);
		Cast(Hand(TestCards.Shock(3, opponent), me), [], them); // damage
		Cast(
			Hand(TestCards.Targeted("Drain", opponent, new LoseLife(Ref.Target(), 2)), me),
			[],
			them
		);
		Assert.That(s.GetPlayer(them).ThisTurn.LifeLost, Nu.EqualTo(5));
		var mine = Life(me);
		ToTurn(s.GetGame().TurnNumber + 1);
		Assert.That((Life(them), Life(me)), Nu.EqualTo((10, mine + 5)));
	}

	// ===== Seven of Nine, Unassimilated: base P/T 7/9 for good =====

	[Test]
	public void SevenOfNine_SevenNamedLands_BecomesASevenNine_ForGood()
	{
		var seven = Field(Trk("Seven of Nine, Unassimilated"), me);
		Assert.That(Activations(seven), Nu.Empty, "five differently named lands");
		Field(Trk("Andoria"), me);
		Field(Trk("Bajor"), me);
		Activate(Activations(seven).Single());
		var turn = s.GetGame().TurnNumber;
		ToTurn(turn + 2);
		var card = s.GetCard(seven);
		Assert.That((s.Power(card), s.Toughness(card)), Nu.EqualTo((7, 9)));
	}

	// ===== Branching Timeline: reveal the top X =====

	[Test]
	public void BranchingTimeline_EveryPermanentAmongTheTopFive_EntersTapped()
	{
		var kill = OnTop(TestCards.Kill(), me);
		var plains = OnTop(TestGames.Card("Plains"), me);
		var trinket = OnTop(TestCards.Trinket, me);
		var fodder = OnTop(TestCards.Fodder, me);
		var bear = OnTop(Bear, me);
		var sixth = s.CardsIn(me, ZoneType.Library).ElementAt(5).Id;
		CastAndResolve(Hand(Trk("Branching Timeline"), me)); // five basic names: X = 5
		Assert.That(
			new[] { bear, trinket, plains }.All(id =>
				s.IsOnBattlefield(id) && s.GetCard(id).IsTapped
			),
			Nu.True
		);
		Assert.That(new[] { kill, fodder }.All(id => s.IsIn(id, me, ZoneType.Library)), Nu.True);
		Assert.That(
			s.CardsIn(me, ZoneType.Library).First().Id,
			Nu.EqualTo(sixth),
			"the rest went to the bottom"
		);
	}
}

static class ListAlso
{
	/// <summary>Records what a choice offered, and passes it on.</summary>
	public static IEnumerable<int> Also(this List<int> seen, IEnumerable<int> ids)
	{
		seen.AddRange(ids);
		return ids;
	}
}
