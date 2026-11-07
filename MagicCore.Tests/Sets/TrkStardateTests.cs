using ImmutableGameObjects;
using MagicCore.Actions;
using MagicCore.Cards;
using MagicCore.Effects;
using MagicCore.Sets;
using MagicCore.Targeting;
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

	// ===== MC-6.5: one put, one remove =====

	ActivateAbilityAction Ability(int card, int index) =>
		Activations(card).Single(a => a.AbilityIndex == index);

	[Test]
	public void Mikaeus_EntersWithX_TapsToGrow_ThenSpendsOneOnTheOthers()
	{
		var mikaeus = Hand(Trk("Mikaeus, the Lunarch"), me);
		s = s.Act(
			new CastSpellAction
			{
				PlayerId = me,
				CardId = mikaeus,
				X = 2,
			}
		);
		Resolve();
		Assert.That(
			s.GetCard(mikaeus).PlusOneCounters,
			Nu.EqualTo(2),
			"enters with X +1/+1 counters"
		);
		s = s.UpdateObject(mikaeus, s.GetCard(mikaeus) with { ControlledSinceTurn = 0 }); // TEST SETUP: not sick
		var bear = Field(Bear, me);
		var theirs = Field(Bear, them);
		s = s.Act(Ability(mikaeus, 1));
		Assert.That(s.GetPendingChoice(), Nu.Null, "his own counter: nothing to pick");
		Resolve();
		Assert.That(
			s.GetCard(mikaeus).PlusOneCounters,
			Nu.EqualTo(1),
			"remove a +1/+1 counter from Mikaeus"
		);
		Assert.That(
			s.GetCard(bear).PlusOneCounters,
			Nu.EqualTo(1),
			"each other creature you control"
		);
		Assert.That(s.GetCard(theirs).PlusOneCounters, Nu.EqualTo(0), "not theirs");
	}

	[Test]
	public void Mikaeus_WithoutACounter_CantSpendOne()
	{
		var mikaeus = Field(Trk("Mikaeus, the Lunarch"), me);
		Assert.That(
			Activations(mikaeus).Select(a => a.AbilityIndex),
			Nu.EqualTo(new[] { 0 }),
			"no +1/+1 counter to remove — only {T}: put one"
		);
		s = s.PutCounters(mikaeus, CounterKinds.Charge, 1); // TEST SETUP: another kind
		Assert.That(
			Activations(mikaeus).Count(),
			Nu.EqualTo(1),
			"a charge counter isn't a +1/+1 counter"
		);
		s = s.PutCounters(mikaeus, CounterKinds.PlusOne, 1); // TEST SETUP: a 0/0 dies without one
		s = s.Act(Ability(mikaeus, 0));
		Resolve();
		Assert.That(
			s.GetCard(mikaeus).PlusOneCounters,
			Nu.EqualTo(2),
			"{T}: Put a +1/+1 counter on Mikaeus"
		);
	}

	[Test]
	public void BristlyBill_LandfallPutsOne_ThenDoublesEachCreaturesOwn()
	{
		var bill = Field(Trk("Bristly Bill, Spine Sower"), me);
		var bear = Field(Bear, me);
		var bare = Field(Bear, me);
		var theirs = Field(Bear, them);
		s = s.PutCounters(theirs, CounterKinds.PlusOne, 1); // TEST SETUP
		var forest = Hand(TestGames.Card("Forest"), me);
		s = s.Act(new PlayLandAction { PlayerId = me, CardId = forest });
		Resolve(On<ChooseTargetChoice>(bear));
		Assert.That(
			s.GetCard(bear).PlusOneCounters,
			Nu.EqualTo(1),
			"landfall: a +1/+1 counter on target creature"
		);
		s = s.PutCounters(bear, CounterKinds.PlusOne, 2); // TEST SETUP: three
		s = s.PutCounters(bill, CounterKinds.PlusOne, 1); // TEST SETUP
		s = s.Act(Activations(bill).Single());
		Resolve();
		Assert.That(
			s.GetCard(bear).PlusOneCounters,
			Nu.EqualTo(6),
			"double the number on each creature"
		);
		Assert.That(s.GetCard(bill).PlusOneCounters, Nu.EqualTo(2), "each counted for itself");
		Assert.That(s.GetCard(bare).PlusOneCounters, Nu.EqualTo(0), "twice none is none");
		Assert.That(s.GetCard(theirs).PlusOneCounters, Nu.EqualTo(1), "creatures you control");
	}

	// ===== MC-6.6: modes =====

	[Test]
	public void Ertai_ChooseUpToOne_NoneIsAnAnswer()
	{
		var bear = Field(Bear, them);
		var ertai = Hand(Trk("Ertai Resurrected"), me);
		s = s.Act(new CastSpellAction { PlayerId = me, CardId = ertai });
		s = TestGames.PassUntil(s, x => x.GetPendingChoice() is ChooseModesChoice);
		var modes = (ChooseModesChoice)s.GetPendingChoice()!;
		Assert.That((modes.MinChoices, modes.MaxChoices), Nu.EqualTo((0, 1)), "choose up to one");
		Assert.That(modes.Prompt, Nu.EqualTo("Choose up to one"));
		var hand = HandCount(them);
		s = s.Choose([]);
		Resolve();
		Assert.That(Alive(bear), Nu.True);
		Assert.That(HandCount(them), Nu.EqualTo(hand), "no mode: nobody draws");
		Assert.That(
			s.GetPlayer(me).ThisTurn.DilemmasFaced,
			Nu.EqualTo(0),
			"no mode chosen: no dilemma faced"
		);
	}

	[Test]
	public void Ertai_Destroy_ItsControllerDraws()
	{
		var bear = Field(Bear, them);
		var hand = HandCount(them);
		CastWithTrigger(Hand(Trk("Ertai Resurrected"), me), [1], bear);
		Assert.That(Alive(bear), Nu.False, "destroy another target creature or planeswalker");
		Assert.That(HandCount(them), Nu.EqualTo(hand + 1), "its controller draws a card");
		Assert.That(
			s.GetPlayer(me).ThisTurn.DilemmasFaced,
			Nu.EqualTo(1),
			"a mode chosen: a dilemma faced"
		);
	}

	[Test]
	public void Ertai_CountersAnActivatedAbility_ItsControllerDraws()
	{
		var pump = Field(
			new CardDefinition(
				Bear.Data,
				[new ActivatedAbilityComponent(ManaCost.Parse("{1}"), [new GainLife(Ref.You, 3)])]
			),
			them
		);
		var ertai = Hand(Trk("Ertai Resurrected"), me);
		s = s.Act(new PassPriorityAction { PlayerId = me });
		s = s.Act(
			new ActivateAbilityAction
			{
				PlayerId = them,
				CardId = pump,
				AbilityIndex = 0,
			}
		);
		s = s.Act(new PassPriorityAction { PlayerId = them });
		var ability = s.GetChildren(s.GetWellKnownId(MagicKeys.Stack))
			.OfType<StackAbility>()
			.Single();
		var life = Life(them);
		var hand = HandCount(them);
		CastWithTrigger(ertai, [0], ability.Id);
		Assert.That(Life(them), Nu.EqualTo(life), "counter target activated ability");
		Assert.That(
			HandCount(them),
			Nu.EqualTo(hand + 1),
			"its controller — the ability's — draws"
		);
	}

	[Test]
	public void Ertai_CountersASpell_ItsControllerDraws()
	{
		var spell = TheirSpellOnTheStack(
			TestGames.Inline("Gain", "{1}", "Instant", null, new GainLife(Ref.You, 3))
		);
		var life = Life(them);
		var hand = HandCount(them);
		CastWithTrigger(Hand(Trk("Ertai Resurrected"), me), [0], spell);
		Assert.That(Life(them), Nu.EqualTo(life), "counter target spell");
		Assert.That(HandCount(them), Nu.EqualTo(hand + 1), "its controller draws a card");
	}

	// ===== MC-6.7: cycling =====

	[Test]
	public void DecreeOfPain_DrawsForEachCreatureDestroyedThisWay()
	{
		Field(Bear, me);
		Field(Bear, them);
		Field(Bear, them);
		var wall = Field(TestGames.Creature("Wall", "0/4", "Indestructible"), them);
		var hand = HandCount(me);
		Cast(Hand(Trk("Decree of Pain"), me), []);
		Assert.That(
			s.CardsIn(them, ZoneType.Battlefield)
				.Where(c => c.Is(CardType.Creature))
				.Select(c => c.Id),
			Nu.EqualTo(new[] { wall }),
			"destroy all creatures"
		);
		Assert.That(
			HandCount(me),
			Nu.EqualTo(hand + 3),
			"a card for each one destroyed — not the wall"
		);
	}

	[Test]
	public void DecreeOfPain_Cycled_DrawsAndShrinksEveryCreature()
	{
		var bear = Field(Bear, them);
		var big = Field(TestGames.Creature("Big", "3/3"), me);
		var decree = Hand(Trk("Decree of Pain"), me);
		var hand = HandCount(me);
		s = s.Act(Activations(decree).Single());
		Resolve();
		Assert.That(HandCount(me), Nu.EqualTo(hand), "discard this card: draw a card");
		Assert.That(Alive(bear), Nu.False, "when you cycle this card, all creatures get -2/-2");
		Assert.That(s.Power(s.GetCard(big)), Nu.EqualTo(1));
	}

	[Test]
	public void DecreeOfPain_AnotherCardCycled_DoesntTriggerIt()
	{
		var bear = Field(Bear, them);
		var decree = Hand(Trk("Decree of Pain"), me);
		s = s.MoveCard(decree, s.ZoneId(me, ZoneType.Graveyard)); // TEST SETUP: cycled before
		var whale = Hand(Trk("Humpback Whales"), me);
		s = s.Act(Activations(whale).Single());
		Resolve();
		Assert.That(Alive(bear), Nu.True, "when you cycle THIS card");
	}

	[Test]
	public void Landcycling_IsCycling()
	{
		var whale = Hand(Trk("Humpback Whales"), me);
		var watcher = Field(
			TestCards.WithTrigger(
				Bear,
				new Trigger(EventKind.AbilityActivated) { By = Whose.You, Ability = "Cycling" },
				new GainLife(Ref.You, 1)
			),
			me
		);
		var life = Life(me);
		s = s.Act(Activations(whale).Single());
		Resolve();
		Assert.That(Life(me), Nu.EqualTo(life + 1), "basic landcycling is a cycling ability");
		var channel = Hand(
			new CardDefinition(
				Bear.Data,
				[
					new ActivatedAbilityComponent(
						ManaCost.Parse("{1}"),
						[new DrawCards(Ref.You, 1)]
					)
					{
						Costs = [new DiscardSelfCost()],
						From = ZoneType.Hand,
					},
				]
			),
			me
		);
		s = s.Act(Activations(channel).Single());
		Resolve();
		Assert.That(
			Life(me),
			Nu.EqualTo(life + 1),
			"discarding it for another ability isn't cycling"
		);
		Assert.That(Alive(watcher), Nu.True);
	}

	// ===== MC-6.8: evasion; MC-6.9's "you may play that card" =====

	[Test]
	public void ThadaAdel_Islandwalk_UnblockableWhileTheyControlAnIsland()
	{
		var thada = Field(Trk("Thada Adel, Acquisitor"), me);
		var blocker = Field(Bear, them);
		Assert.That(
			global::MagicCore.Turns.Combat.CanBlock(s, s.GetCard(blocker), s.GetCard(thada)),
			Nu.False,
			"defending player controls an Island"
		);
		foreach (
			var island in s.CardsIn(them, ZoneType.Battlefield)
				.Where(c => c.Name == "Island")
				.ToList()
		)
			s = s.MoveCard(island.Id, s.ZoneId(them, ZoneType.Graveyard)); // TEST SETUP
		Assert.That(
			global::MagicCore.Turns.Combat.CanBlock(s, s.GetCard(blocker), s.GetCard(thada)),
			Nu.True,
			"no Island"
		);
	}

	[Test]
	public void ThadaAdel_CombatDamage_ExilesTheirArtifact_PlayableThisTurn()
	{
		Field(Trk("Thada Adel, Acquisitor"), me);
		var relic = TestGames.Put(
			s,
			them,
			TestGames.Inline("Relic", "{1}", "Artifact"),
			ZoneType.Library
		);
		s = relic.Item1;
		AttackWithAll();
		s = TestGames.PassUntil(s, x => x.GetPendingChoice() is ChooseFromLibraryChoice);
		var search = s.GetPendingChoice()!;
		Assert.That(s.DecidingPlayerId(), Nu.EqualTo(me), "you search that player's library");
		Assert.That(
			search.Options.Select(o => o.Id),
			Nu.EqualTo(new[] { relic.Item2 }),
			"an artifact card"
		);
		s = s.Choose([relic.Item2]);
		Assert.That(s.IsIn(relic.Item2, them, ZoneType.Exile), Nu.True, "exile it");
		var playable = s.GetCard(relic.Item2).GetComponent<PlayableFromExile>();
		Assert.That(playable, Nu.Not.Null, "you may play that card");
		Assert.That(
			(playable!.PlayerId, playable.UntilTurn),
			Nu.EqualTo((me, s.GetGame().TurnNumber)),
			"you, until end of turn"
		);
	}

	[Test]
	public void Shizo_GivesALegendFear()
	{
		var shizo = Field(Trk("Shizo, Death's Storehouse"), me);
		var legend = Field(Trk("Ertai Resurrected"), me);
		var bear = Field(Bear, them);
		var black = Field(Bear with { Data = Bear.Data with { Colors = ["B"] } }, them);
		Assert.That(Activations(shizo).Count(), Nu.EqualTo(1), "target legendary creature: Ertai");
		s = s.Act(Activations(shizo).Single());
		Resolve();
		Assert.That(
			global::MagicCore.Turns.Combat.CanBlock(s, s.GetCard(bear), s.GetCard(legend)),
			Nu.False,
			"fear: not by a green creature"
		);
		Assert.That(
			global::MagicCore.Turns.Combat.CanBlock(s, s.GetCard(black), s.GetCard(legend)),
			Nu.True,
			"except by black creatures"
		);
	}

	// ===== Stella Lee: the Nth spell this turn =====

	static CardDefinition Gain =>
		TestGames.Inline("Gain", "{1}", "Instant", null, new GainLife(Ref.You, 1));

	int PlayableInExile() =>
		s.CardsIn(me, ZoneType.Exile).Count(c => c.GetComponent<PlayableFromExile>() is not null);

	[Test]
	public void StellaLee_TheSecondSpellOnly_ExilesTheTopPlayable()
	{
		Field(Trk("Stella Lee, Wild Card"), me);
		Cast(Hand(Gain, me), []);
		Assert.That(PlayableInExile(), Nu.EqualTo(0), "the first spell");
		Cast(Hand(Gain, me), []);
		Assert.That(PlayableInExile(), Nu.EqualTo(1), "your second spell each turn");
		Cast(Hand(Gain, me), []);
		Assert.That(PlayableInExile(), Nu.EqualTo(1), "not the third");
	}

	[Test]
	public void StellaLee_CopiesOnlyOnceThreeSpellsWereCast()
	{
		var stella = Field(Trk("Stella Lee, Wild Card"), me);
		Cast(Hand(Gain, me), []);
		var second = Hand(Gain, me);
		s = s.Act(new CastSpellAction { PlayerId = me, CardId = second });
		Assert.That(Activations(stella), Nu.Empty, "two spells this turn: not yet");
		Resolve();
		var third = Hand(Gain, me);
		s = s.Act(new CastSpellAction { PlayerId = me, CardId = third });
		var life = Life(me);
		s = s.Act(Activations(stella).Single());
		Resolve();
		Assert.That(Life(me), Nu.EqualTo(life + 2), "copy target instant spell you control");
	}

	[TestCase("Teyo, Diamondblade Mage")]
	[TestCase("Shields Up!")]
	public void PutCounters_IfItsACreature_NotOnAnArtifact(string name)
	{
		var relic = Field(TestGames.Inline("Relic", "{1}", "Artifact"), me);
		var card = Hand(CardRegistry.Get(name), me);
		if (s.GetCard(card).Is(CardType.Creature))
			CastWithTrigger(card, [], relic);
		else
			Cast(card, [], relic);
		Assert.That(
			s.GetCard(relic).PlusOneCounters,
			Nu.EqualTo(0),
			"put a +1/+1 counter on it if it's a creature"
		);
	}

	[Test]
	public void PutCounters_PlusOneOnANoncreature_IsPut()
	{
		var relic = Field(TestGames.Inline("Relic", "{1}", "Artifact"), me);
		var grow = TestGames.Inline(
			"Grow",
			"{1}",
			"Sorcery",
			null,
			new PutCounters(
				Ref.Permanents(
					global::MagicCore.Filters.Is.Yours & global::MagicCore.Filters.Is.Artifact
				),
				1
			)
		);
		Cast(Hand(grow, me), []);
		Assert.That(s.GetCard(relic).PlusOneCounters, Nu.EqualTo(1), "CR 122.1: any permanent");
	}

	// ===== MC-5.6 batch 3a: Riku, Prosper, Drivnod =====

	/// <summary>"Choose one or more —" of three: gain 1, gain 2, gain 3.</summary>
	static CardDefinition Charm =>
		new(
			TestGames.Inline("Charm", "{1}", "Instant").Data,
			[
				new SpellEffectsComponent([])
				{
					Modes =
					[
						Scripting.M([], new GainLife(Ref.You, 1)),
						Scripting.M([], new GainLife(Ref.You, 2)),
						Scripting.M([], new GainLife(Ref.You, 3)),
					],
					Choose = new() { AtMost = 3 },
				},
			]
		);

	ChooseModesChoice CastCharmChoosing(params int[] modes)
	{
		var charm = Hand(Charm, me);
		s = s.Act(
			new CastSpellAction
			{
				PlayerId = me,
				CardId = charm,
				Modes = [.. modes],
			}
		);
		s = TestGames.PassUntil(s, x => x.GetPendingChoice() is ChooseModesChoice);
		return (ChooseModesChoice)s.GetPendingChoice()!;
	}

	[Test]
	public void Riku_TwoModesChosen_ChoosesUpToTwo_ExileAndBird()
	{
		Field(Trk("Riku of Many Paths"), me);
		var choice = CastCharmChoosing(0, 1);
		Assert.That(
			(choice.MinChoices, choice.MaxChoices),
			Nu.EqualTo((0, 2)),
			"choose up to X, where X is the number of times you chose a mode for that spell"
		);
		s = s.Choose([0, 2]);
		Resolve();
		Assert.That(PlayableInExile(), Nu.EqualTo(1), "exile the top card; you may play it");
		var bird = s.CardsIn(me, ZoneType.Battlefield).Single(c => c.Name == "Bird");
		Assert.That(s.Has(bird, "Flying"), Nu.True, "a 1/1 blue Bird creature token with flying");
	}

	[Test]
	public void Riku_OneModeChosen_ChoosesOne_ACounterAndTrample()
	{
		var riku = Field(Trk("Riku of Many Paths"), me);
		var choice = CastCharmChoosing(2);
		Assert.That(choice.MaxChoices, Nu.EqualTo(1), "one mode chosen: up to one");
		s = s.Choose([1]);
		Resolve();
		Assert.That(s.GetCard(riku).PlusOneCounters, Nu.EqualTo(1), "a +1/+1 counter on Riku");
		Assert.That(s.Has(s.GetCard(riku), "Trample"), Nu.True, "it gains trample");
	}

	[Test]
	public void Riku_ANonModalSpell_DoesntTrigger()
	{
		var riku = Field(Trk("Riku of Many Paths"), me);
		var gain = Hand(Gain, me);
		s = s.Act(new CastSpellAction { PlayerId = me, CardId = gain });
		Assert.That(s.GetPendingChoice(), Nu.Null, "not a modal spell");
		Resolve();
		Assert.That(s.GetCard(riku).PlusOneCounters, Nu.EqualTo(0));
	}

	[Test]
	public void IsModal_ASpellWithModes_NotOneWithout()
	{
		// Riku can't tell: a non-modal spell's X is 0, and "up to 0" does nothing — so the word is asked directly.
		var charm = Hand(Charm, me);
		var gain = Hand(Gain, me);
		var asking = new global::MagicCore.Filters.FilterContext(me, 0);
		Assert.That(
			global::MagicCore.Filters.Is.Modal.Matches(s, s.GetCard(charm), asking),
			Nu.True
		);
		Assert.That(
			global::MagicCore.Filters.Is.Modal.Matches(s, s.GetCard(gain), asking),
			Nu.False
		);
	}

	int Treasures() => s.CardsIn(me, ZoneType.Battlefield).Count(c => c.Name == "Treasure");

	int InExilePlayable(CardDefinition card)
	{
		int id;
		(s, id) = TestGames.Put(s, me, card, ZoneType.Exile);
		s = s.UpdateObject(
			id,
			(Card)s.GetCard(id).WithComponent(new PlayableFromExile(me, s.GetGame().TurnNumber)) // TEST SETUP: impulse
		);
		return id;
	}

	[Test]
	public void Prosper_ASpellCastFromExile_MakesATreasure_FromHandDoesnt()
	{
		Field(Trk("Prosper, Tome-Bound"), me);
		Cast(Hand(Gain, me), []);
		Assert.That(Treasures(), Nu.EqualTo(0), "cast from your hand");
		var exiled = InExilePlayable(Gain);
		Cast(exiled, []);
		Assert.That(Treasures(), Nu.EqualTo(1), "whenever you play a card from exile");
	}

	[Test]
	public void Prosper_ALandPlayedFromExile_MakesATreasure()
	{
		Field(Trk("Prosper, Tome-Bound"), me);
		var forest = InExilePlayable(TestGames.Card("Forest"));
		s = s.Act(new PlayLandAction { PlayerId = me, CardId = forest });
		Resolve();
		Assert.That(Treasures(), Nu.EqualTo(1), "playing a land from exile is playing a card");
	}

	[Test]
	public void Prosper_ALandPlayedFromHand_NoTreasure()
	{
		Field(Trk("Prosper, Tome-Bound"), me);
		var forest = Hand(TestGames.Card("Forest"), me);
		s = s.Act(new PlayLandAction { PlayerId = me, CardId = forest });
		Resolve();
		Assert.That(Treasures(), Nu.EqualTo(0));
	}

	[Test]
	public void Drivnod_ACreatureDying_TriggersYourAbilitiesAnAdditionalTime()
	{
		Field(Trk("Drivnod, Carnage Dominus"), me);
		Field(
			TestCards.WithTrigger(
				Bear,
				TriggerEvent.CreatureYouControlDies,
				new GainLife(Ref.You, 1)
			),
			me
		);
		var dying = Field(
			TestCards.WithTrigger(Bear, TriggerEvent.SelfDies, new DrawCards(Ref.You, 1)),
			me
		);
		var theirs = Field(
			TestCards.WithTrigger(Bear, TriggerEvent.SelfDies, new GainLife(Ref.You, 1)),
			them
		);
		var life = Life(me);
		var hand = HandCount(me);
		var theirLife = Life(them);
		Cast(Hand(TestCards.Kill(), me), [], dying);
		Assert.That(
			Life(me),
			Nu.EqualTo(life + 2),
			"a creature dying: triggers an additional time"
		);
		Assert.That(HandCount(me), Nu.EqualTo(hand + 2), "its own 'when this dies' too");
		Cast(Hand(TestCards.Kill(), me), [], theirs);
		Assert.That(Life(them), Nu.EqualTo(theirLife + 1), "a permanent THEY control: once");
	}

	[Test]
	public void Drivnod_PayLifeForPhyrexianMana_ExileThree_IndestructibleCounter()
	{
		var drivnod = Field(Trk("Drivnod, Carnage Dominus"), me);
		var dead = new int[4];
		for (var i = 0; i < 4; i++)
			(s, dead[i]) = TestGames.Put(s, me, Bear, ZoneType.Graveyard);
		Assert.That(
			Activations(drivnod).Select(a => a.LifeForPhyrexian),
			Nu.EquivalentTo(new[] { 0, 1, 2 }),
			"{B/P}{B/P}: each paid with {B} or 2 life — one move per split"
		);
		var life = Life(me);
		var untapped = s.CardsIn(me, ZoneType.Battlefield).Count(c => !c.IsTapped);
		s = s.Act(Activations(drivnod).Single(a => a.LifeForPhyrexian == 2));
		Assert.That(Life(me), Nu.EqualTo(life - 4), "2 life each");
		Assert.That(
			s.CardsIn(me, ZoneType.Battlefield).Count(c => !c.IsTapped),
			Nu.EqualTo(untapped),
			"no mana"
		);
		var first = (ChooseAmongChoice)s.GetPendingChoice()!;
		Assert.That(first.Options.Count, Nu.EqualTo(4), "any creature card in your graveyard");
		s = s.Choose([dead[0]]);
		Assert.That(
			s.GetPendingChoice()!.Options.Select(o => o.Id),
			Nu.EquivalentTo(dead[1..]),
			"the one exiled isn't offered again"
		);
		Resolve();
		Assert.That(s.CardsIn(me, ZoneType.Exile).Count(), Nu.EqualTo(3), "exile three");
		Assert.That(s.CardsIn(me, ZoneType.Graveyard).Count(), Nu.EqualTo(1));
		Assert.That(
			s.GetCard(drivnod).CountersOf(CounterKinds.Indestructible),
			Nu.EqualTo(1),
			"put an indestructible counter on Drivnod"
		);
		Cast(Hand(TestCards.Kill(), me), [], drivnod);
		Assert.That(Alive(drivnod), Nu.True, "an indestructible counter: it has indestructible");
	}

	[Test]
	public void BelowZeroLife_AnAbilityPayingNoLife_CanStillBeActivated()
	{
		// Found by the per-card replay of Lore (Georgiou kept P1 at -6): the Phyrexian life check read
		// "life < 2 × 0" and refused every activation below 0.
		var pump = Field(
			new CardDefinition(
				Bear.Data,
				[new ActivatedAbilityComponent(ManaCost.Parse("{1}"), [new GainLife(Ref.You, 1)])]
			),
			me
		);
		s = s.WithPlayer(s.GetPlayer(me) with { Life = -3 }); // TEST SETUP: Georgiou's "don't lose"
		Assert.That(Activations(pump), Nu.Not.Empty, "no life paid: any life total will do");
	}

	[Test]
	public void Drivnod_PaidWithBlack_NoLife()
	{
		var drivnod = Field(Trk("Drivnod, Carnage Dominus"), me);
		for (var i = 0; i < 3; i++)
			(s, _) = TestGames.Put(s, me, Bear, ZoneType.Graveyard);
		var life = Life(me);
		s = s.Act(Activations(drivnod).Single(a => a.LifeForPhyrexian == 0));
		Resolve();
		Assert.That(Life(me), Nu.EqualTo(life), "{B}{B} paid with mana");
		Assert.That(
			ManaCost.Parse("{2}{B/P}").ToString(),
			Nu.EqualTo("{2}{B/P}"),
			"the printed form: what Parse reads (Describe shows it)"
		);
		Assert.That(s.GetCard(drivnod).CountersOf(CounterKinds.Indestructible), Nu.EqualTo(1));
	}

	[Test]
	public void Drivnod_TwoCreatureCards_CantActivate_LowLife_FewerSplits()
	{
		var drivnod = Field(Trk("Drivnod, Carnage Dominus"), me);
		for (var i = 0; i < 2; i++)
			(s, _) = TestGames.Put(s, me, Bear, ZoneType.Graveyard);
		(s, _) = TestGames.Put(s, me, TestGames.Card("Forest"), ZoneType.Graveyard);
		Assert.That(Activations(drivnod), Nu.Empty, "three CREATURE cards");
		(s, _) = TestGames.Put(s, me, Bear, ZoneType.Graveyard);
		s = s.WithPlayer(s.GetPlayer(me) with { Life = 3 }); // TEST SETUP
		Assert.That(
			Activations(drivnod).Select(a => a.LifeForPhyrexian),
			Nu.EquivalentTo(new[] { 0, 1 }),
			"3 life pays for one, not two (CR 119.4)"
		);
	}
}
