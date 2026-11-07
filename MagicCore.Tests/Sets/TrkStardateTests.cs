using ImmutableGameObjects;
using MagicCore.Actions;
using MagicCore.Cards;
using MagicCore.Effects;
using MagicCore.Sets;
using Nu = NUnit.Framework.Is;

namespace MagicCore.Tests.Sets;

/// <summary>
/// MC-5.6: TRK's Special Guests (the Stardates) on MC-6's composed pieces — each against its card's
/// PRINTED text, through real casting.
/// </summary>
public class TrkStardateTests : FraBoardTest
{
	static CardDefinition Trk(string name) => CardRegistry.Get(name, "TRK");

	static CardDefinition Bear => TestGames.Creature("Bear", "2/2");

	void AttackWithAll()
	{
		s = TestGames.PassUntil(s, x => x.IsWaitingForChoice);
		while (s.GetPendingChoice() is DeclareAttackerChoice)
			s = s.Choose([them]);
	}

	// ===== MC-6.1: triggers an additional time =====

	[Test]
	public void Isshin_ACreatureAttacking_TriggersYourAbilitiesAnAdditionalTime()
	{
		Field(Trk("Isshin, Two Heavens as One"), me);
		Field(TestCards.WithTrigger(Bear, TriggerEvent.SelfAttacks, new GainLife(Ref.You, 1)), me);
		Field(TestCards.WithTrigger(Bear, TriggerEvent.YouAttack, new DrawCards(Ref.You, 1)), me);
		var life = s.GetPlayer(me).Life;
		var hand = HandCount(me);
		AttackWithAll();
		Resolve();
		Assert.That(
			s.GetPlayer(me).Life,
			Nu.EqualTo(life + 2),
			"whenever this attacks — triggers an additional time"
		);
		Assert.That(HandCount(me), Nu.EqualTo(hand + 2), "whenever you attack — twice too");
	}

	[Test]
	public void Isshin_AnotherCause_TriggersOnce()
	{
		Field(Trk("Isshin, Two Heavens as One"), me);
		Field(TestCards.WithTrigger(Bear, TriggerEvent.YouGainLife, new DrawCards(Ref.You, 1)), me);
		var hand = HandCount(me);
		var gain = Hand(
			new CardDefinition(
				TestGames.Inline("Gain", "{1}", "Instant").Data,
				[new SpellEffectsComponent([new GainLife(Ref.You, 1)])]
			),
			me
		);
		Cast(gain, []);
		Assert.That(HandCount(me), Nu.EqualTo(hand + 1), "only a creature attacking");
	}

	// ===== MC-6.2: numeric replacements =====

	int Graveyard(int player) => s.CardsIn(player, ZoneType.Graveyard).Count();

	[Test]
	public void Torbran_ARedSourceDealsTwoMore_ToAnOpponentAndTheirs()
	{
		Field(Trk("Torbran, Thane of Red Fell"), me);
		var theirs = Field(TestGames.Creature("Big", "4/4"), them);
		var mine = Field(TestGames.Creature("Big", "4/4"), me);
		var life = Life(them);
		var embers = TestGames.Inline(
			"Embers",
			"{R}",
			"Sorcery",
			null,
			new DealDamage(Ref.EachOpponent, 1),
			new DealDamage(Ref.AllCreatures, 1)
		);
		Cast(Hand(embers with { Data = embers.Data with { Colors = ["R"] } }, me), []);
		Assert.That(Life(them), Nu.EqualTo(life - 3), "that much damage plus 2");
		Assert.That(s.GetCard(theirs).Damage, Nu.EqualTo(3), "a permanent an opponent controls");
		Assert.That(s.GetCard(mine).Damage, Nu.EqualTo(1), "not to your own");
		life = Life(them);
		Cast(
			Hand(
				TestGames.Inline(
					"Dust",
					"{1}",
					"Sorcery",
					null,
					new DealDamage(Ref.EachOpponent, 1)
				),
				me
			),
			[]
		);
		Assert.That(Life(them), Nu.EqualTo(life - 1), "a RED source");
	}

	[Test]
	public void Bruvac_AnOpponentMillsTwiceThatMany_YouDont()
	{
		Field(Trk("Bruvac the Grandiloquent"), me);
		var mine = Graveyard(me);
		var theirs = Graveyard(them);
		Cast(
			Hand(TestGames.Inline("Mill", "{1}", "Sorcery", null, new Mill(Ref.EachPlayer, 2)), me),
			[]
		);
		Assert.That(Graveyard(them), Nu.EqualTo(theirs + 4), "they mill twice that many");
		Assert.That(Graveyard(me), Nu.EqualTo(mine + 3), "you mill 2 — and the spell");
	}

	[Test]
	public void ACounterKindReplacement_LeavesOtherKinds()
	{
		Field(CardRegistry.Get("Yoshimaru, Beloved Companion"), me);
		var bear = Field(Bear, me);
		s = s.PutCounters(bear, CounterKinds.Charge, 1);
		Assert.That(s.GetCard(bear).ChargeCounters, Nu.EqualTo(1), "+1/+1 counters only");
		s = s.PutCounters(bear, CounterKinds.PlusOne, 1);
		Assert.That(s.GetCard(bear).PlusOneCounters, Nu.EqualTo(2), "that many plus one");
	}

	// ===== MC-6.3: mana abilities =====

	[Test]
	public void PhyrexianTower_SacrificeIsAMovePerCreature_NeverAutoPaid()
	{
		var tower = Field(Trk("Phyrexian Tower"), me);
		var bear = Field(Bear, me);
		Field(Bear, me);
		var moves = s.LegalActions().OfType<TapForManaAction>().ToList();
		Assert.That(
			moves,
			Has.Count.EqualTo(2),
			"{T}, Sacrifice a creature — one move per creature"
		);
		Assert.That(
			ManaAbilities.YieldOf(s, s.GetCard(tower)).Units,
			Has.Length.EqualTo(1),
			"its plain yield is the {C}"
		);
		var pool = s.GetPlayer(me).ManaPool[ManaColor.Black];
		s = s.Act(moves.Single(m => m.SacrificeId == bear));
		Assert.That(s.GetPlayer(me).ManaPool[ManaColor.Black], Nu.EqualTo(pool + 2), "Add {B}{B}");
		Assert.That(s.IsIn(bear, me, ZoneType.Graveyard), Nu.True, "sacrificed");
		Assert.That(s.GetCard(tower).IsTapped, Nu.True);
		Assert.That(s.LegalActions().OfType<TapForManaAction>(), Nu.Empty, "tapped: no more");
	}

	[Test]
	public void PhyrexianTower_IsNoLastingSourceOfBlack_ForTheSealedBuilder()
	{
		Assert.That(
			global::MagicCore.Limited.SealedBuilder.Fixes("Phyrexian Tower"),
			Nu.Empty,
			"its {B}{B} costs a creature"
		);
	}

	[Test]
	public void Rishkar_CreaturesWithACounterTapForGreen_OthersDont()
	{
		Field(Trk("Rishkar, Peema Renegade"), me);
		var countered = Field(Bear, me);
		var bare = Field(Bear, me);
		var theirs = Field(Bear, them);
		s = s.PutCounters(countered, CounterKinds.PlusOne, 1);
		s = s.PutCounters(theirs, CounterKinds.PlusOne, 1);
		Assert.That(
			ManaAbilities.ColorsOf(s, s.GetCard(countered)),
			Nu.EquivalentTo(new[] { ManaColor.Green })
		);
		Assert.That(ManaAbilities.ColorsOf(s, s.GetCard(bare)), Nu.Empty, "with a counter on it");
		Assert.That(ManaAbilities.ColorsOf(s, s.GetCard(theirs)), Nu.Empty, "you control");
	}

	[Test]
	public void TalonGates_PutsItselfOntoTheBattlefieldFromHand_AndPhasesOutUpToOne()
	{
		var victim = Field(Bear, them);
		var gates = Hand(Trk("Talon Gates of Madara"), me);
		s = s.Act(Activations(gates).Single());
		Resolve(On<ChooseTargetChoice>(victim));
		Assert.That(
			s.IsOnBattlefield(gates),
			Nu.True,
			"Put this card from your hand onto the battlefield"
		);
		Assert.That(s.IsOnBattlefield(victim), Nu.False, "up to one target creature phases out");
		Assert.That(
			ManaAbilities.ColorsOf(s, s.GetCard(gates)),
			Nu.EquivalentTo(new[] { ManaColor.Colorless }),
			"{1}, {T} isn't a plain tap"
		);
	}

	// ===== MC-6.4: counted bounds =====

	int OnTop(CardDefinition card, int player)
	{
		int id;
		(s, id) = TestGames.Put(s, player, card, ZoneType.Library);
		s = s.MoveObjectToFront(id, s.ZoneId(player, ZoneType.Library)); // TEST SETUP: index 0 is the top
		return id;
	}

	static CardDefinition Legend(string cost, string name) =>
		TestCards.Typed(TestGames.CreatureCosting(cost, name, "1/1"), "Legendary Creature — Human");

	[Test]
	public void Sisay_SearchesForALegendWithManaValueLessThanHerPower()
	{
		var sisay = Field(Trk("Sisay, Weatherlight Captain"), me);
		var cheap = OnTop(Legend("{1}", "Cheap"), me);
		OnTop(Legend("{2}", "Dear"), me); // her power is 2: "less than"
		s = s.Act(Activations(sisay).Single());
		s = TestGames.PassUntil(s, x => x.IsWaitingForChoice);
		Assert.That(
			s.GetPendingChoice()!.Options.Select(o => o.Id),
			Nu.EqualTo(new[] { cheap }),
			"a legendary permanent card with mana value less than Sisay's power"
		);
		s = s.Choose([cheap]);
		Assert.That(s.IsOnBattlefield(cheap), Nu.True, "put that card onto the battlefield");
	}

	[Test]
	public void Sisay_GetsOneForEachColorAmongYourOtherLegends()
	{
		var sisay = Field(Trk("Sisay, Weatherlight Captain"), me);
		var gold = Legend("{1}", "Gold");
		Field(gold with { Data = gold.Data with { Colors = ["W", "U"] } }, me);
		Field(Legend("{1}", "Theirs") with { Data = gold.Data with { Colors = ["B"] } }, them);
		Assert.That(s.Power(s.GetCard(sisay)), Nu.EqualTo(4), "+1/+1 for each color");
		Assert.That(s.Toughness(s.GetCard(sisay)), Nu.EqualTo(4));
	}

	[Test]
	public void Loot_PutsACreatureWithManaValueUpToYourLands_OntoTheBattlefield()
	{
		var loot = Field(Trk("Loot, Exuberant Explorer"), me);
		var lands = s.CardsIn(me, ZoneType.Battlefield).Where(c => c.Is(CardType.Land)).ToList();
		var keep = lands
			.Where(c => c.Name == "Forest")
			.Take(2)
			.Concat(lands.Where(c => c.Name != "Forest").Take(4))
			.Select(c => c.Id)
			.ToHashSet();
		foreach (var land in lands.Where(c => !keep.Contains(c.Id)))
			s = s.MoveCard(land.Id, s.ZoneId(me, ZoneType.Graveyard)); // TEST SETUP: six lands
		var six = OnTop(TestGames.CreatureCosting("{6}", "Six", "6/6"), me);
		var seven = OnTop(TestGames.CreatureCosting("{7}", "Seven", "7/7"), me);
		s = s.Act(Activations(loot).Single());
		s = TestGames.PassUntil(s, x => x.IsWaitingForChoice);
		var options = s.GetPendingChoice()!.Options.Select(o => o.Id).ToList();
		Assert.That(options, Does.Contain(six));
		Assert.That(
			options,
			Does.Not.Contain(seven),
			"mana value less than or equal to the number of lands you control"
		);
		s = s.Choose([six]);
		Assert.That(s.IsOnBattlefield(six), Nu.True, "put it onto the battlefield");
		Assert.That(s.GetCard(six).IsTapped, Nu.False);
	}
}
