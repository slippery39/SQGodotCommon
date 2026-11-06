using ImmutableGameObjects;
using MagicCore.Actions;
using MagicCore.Cards;
using MagicCore.Effects;
using MagicCore.Rules;
using MagicCore.Sets;
using MagicCore.Targeting;
using MagicCore.Turns;
using static MagicCore.Sets.Scripting;
using Nu = NUnit.Framework.Is;

namespace MagicCore.Tests.Sets;

/// <summary>
/// MC-5.3 batch 7b: TRK's one-offs — modes not chosen before, can't sacrifice Treasures, enters attacking,
/// a granted mana ability, explore, a player's hexproof and damage prevented, counters removed from among,
/// a token attached as it is made, any number of lands sacrificed. Each new piece against its card's
/// PRINTED text, through real casting.
/// </summary>
public class TrkBatch7bTests : FraBoardTest
{
	static CardDefinition Trk(string name) => CardRegistry.Get(name, "TRK");

	static CardDefinition Bear => TestGames.Creature("Bear", "2/2");

	static CardDefinition GainThree =>
		new(
			TestGames.Inline("Gain", "{1}", "Instant").Data,
			[new SpellEffectsComponent([new GainLife(Ref.You, 3)])]
		);

	int OnTop(CardDefinition card, int player)
	{
		int id;
		(s, id) = TestGames.Put(s, player, card, ZoneType.Library);
		s = s.MoveObjectToFront(id, s.ZoneId(player, ZoneType.Library)); // TEST SETUP: index 0 is the top
		return id;
	}

	Card Named(int player, string name) =>
		s.CardsIn(player, ZoneType.Battlefield).Single(c => c.Name == name);

	/// <summary>Answers a choice of type T with these ids; any other with its minimum.</summary>
	static Func<ChoiceAction, IEnumerable<int>> On<T>(params int[] ids)
		where T : ChoiceAction =>
		c =>
			c switch
			{
				T => ids,
				_ => c.Options.Take(c.MinChoices).Select(o => o.Id),
			};

	// ===== A token attached as it is made; Equipment tokens become a copy =====

	[Test]
	public void Gwyndala_TheHeirloomEntersAttached_AndEquipmentTokensBecomeACopy()
	{
		var gwyndala = Hand(Trk("Gwyndala, the Progeny"), me);
		Cast(gwyndala, []);
		var heirloom = Named(me, "Neuroflux Heirloom");
		Assert.That(heirloom.IsToken, Nu.True);
		Assert.That(heirloom.AttachedToId, Nu.EqualTo(gwyndala), "Attach it to Gwyndala");
		Assert.That(
			s.Power(s.GetCard(gwyndala)),
			Nu.EqualTo(2),
			"Equipped creature gets +1/+1 — a 1/1"
		);
		var blade = Field(
			new CardDefinition(
				TestGames.Inline("Blade", "{1}", "Artifact — Equipment").Data,
				[AttachedGets(3, 0)]
			),
			me
		);
		s = s.Act(Activations(gwyndala).Single(a => a.Targets.SequenceEqual([blade])));
		Resolve();
		Assert.That(s.GetCard(heirloom.Id).Name, Nu.EqualTo("Blade"), "becomes a copy");
		Assert.That(
			s.Power(s.GetCard(gwyndala)),
			Nu.EqualTo(4),
			"still attached, now the Blade's +3/+0"
		);
	}

	// ===== A basic or a Planet; any number of lands sacrificed, that many found =====

	[Test]
	public void GenesisDevice_FindsAPlanet_ThenSacrificesAnyNumberOfLands_AndFindsThatMany()
	{
		var planet = OnTop(Trk("Bajor"), me);
		var device = Hand(Trk("Genesis Device"), me);
		s = s.Act(new CastSpellAction { PlayerId = me, CardId = device });
		Resolve(On<ChooseFromLibraryChoice>(planet));
		Assert.That(s.IsOnBattlefield(planet), Nu.True, "a basic land card or Planet card");
		Assert.That(s.GetCard(planet).IsTapped, Nu.True, "onto the battlefield tapped");

		var first = OnTop(TestGames.Card("Plains"), me);
		var second = OnTop(TestGames.Card("Island"), me);
		OnTop(TestGames.Card("Swamp"), me); // a third: only "that many" are found
		var mine = s.CardsIn(me, ZoneType.Battlefield)
			.Where(c => c.Is(CardType.Land) && c.Id != planet)
			.Select(c => c.Id)
			.Take(2)
			.ToArray();
		s = s.Act(Activations(device).Single());
		s = TestGames.PassUntil(s, x => x.GetPendingChoice() is ChoosePermanentChoice);
		Assert.That(
			s.GetPendingChoice()!.MaxChoices,
			Nu.GreaterThan(2),
			"Sacrifice any number of lands"
		);
		s = s.Choose(mine);
		s = TestGames.PassUntil(s, x => x.GetPendingChoice() is ChooseFromLibraryChoice);
		Assert.That(s.GetPendingChoice()!.MaxChoices, Nu.EqualTo(2), "up to that many land cards");
		s = s.Choose([first, second]);
		Resolve();
		Assert.That(mine.All(id => !s.IsOnBattlefield(id)), Nu.True, "the two sacrificed");
		Assert.That(
			new[] { first, second }.All(id => s.IsOnBattlefield(id) && s.GetCard(id).IsTapped),
			Nu.True,
			"put them onto the battlefield tapped"
		);
		Assert.That(s.IsOnBattlefield(device), Nu.False, "Sacrifice Genesis Device");
	}

	// ===== Modes not chosen before; you lose the game =====

	[Test]
	public void KobayashiMaru_EachUpkeepChoosesAModeNotChosenBefore_AndAbandonShipLoses()
	{
		Field(Trk("Kobayashi Maru Scenario"), me);
		ChooseModesChoice NextUpkeep()
		{
			s = TestGames.PassUntil(
				s,
				x => x.GetPendingChoice() is ChooseModesChoice || x.IsOver()
			);
			return (ChooseModesChoice)s.GetPendingChoice()!;
		}
		Assert.That(NextUpkeep().Options.Select(o => o.Id), Nu.EquivalentTo(new[] { 0, 1, 2, 3 }));
		var hand = HandCount(me);
		s = s.Choose([1]); // Sensors
		Resolve();
		Assert.That(HandCount(me), Nu.EqualTo(hand + 2), "Sensors — Draw two cards");
		Assert.That(
			NextUpkeep().Options.Select(o => o.Id),
			Nu.EquivalentTo(new[] { 0, 2, 3 }),
			"choose one that hasn't been chosen"
		);
		s = s.Choose([2]); // Transport
		Resolve();
		Assert.That(
			s.CardsIn(me, ZoneType.Battlefield).Count(c => c.Name == "Officer"),
			Nu.EqualTo(2)
		);
		Assert.That(NextUpkeep().Options.Select(o => o.Id), Nu.EquivalentTo(new[] { 0, 3 }));
		s = s.Choose([3]); // Abandon Ship
		Resolve();
		Assert.That(s.IsOver(), Nu.True, "Abandon Ship — You lose the game.");
		Assert.That(s.WinnerId(), Nu.EqualTo(them));
	}

	// ===== Can't sacrifice Treasure tokens =====

	[Test]
	public void Quark_ATreasureEachUpkeep_ThatCantBeSacrificed()
	{
		var quark = Field(Trk("Quark, Humble Proprietor"), me);
		var turn = s.GetGame().TurnNumber;
		s = TestGames.PassUntil(
			s,
			x => x.GetGame().TurnNumber == turn + 1 && x.GetGame().Step == Step.Draw
		);
		var treasure = Named(me, "Treasure");
		Assert.That(treasure.IsToken, Nu.True, "At the beginning of each upkeep");
		Assert.That(
			ManaAbilities.CanTap(s, treasure),
			Nu.False,
			"its mana is a sacrifice — You can't sacrifice Treasure tokens"
		);
		Assert.That(
			new SacrificeCost(global::MagicCore.Filters.Is.Artifact).Candidates(s, me, quark),
			Does.Not.Contain(treasure.Id)
		);
		Assert.That(s.Sacrifice(treasure.Id).IsOnBattlefield(treasure.Id), Nu.True);
		s = s.Sacrifice(quark); // gone, the Treasure is a Treasure again
		Assert.That(ManaAbilities.CanTap(s, s.GetCard(treasure.Id)), Nu.True);
	}

	CardDefinition TreasureWith(params GameComponent[] abilities) =>
		new(Tokens.Treasure, [.. abilities]);

	int TokenOnField(CardDefinition card)
	{
		var id = Field(card, me);
		s = s.UpdateObject(id, s.GetCard(id) with { IsToken = true }); // TEST SETUP: a token
		return id;
	}

	[Test]
	public void Quark_AnEdictTakesAnArtifactYouCanSacrifice_NotTheTreasure()
	{
		Field(Trk("Quark, Humble Proprietor"), me);
		var treasure = TokenOnField(TreasureWith(Tokens.SacrificeForMana(1)));
		var rock = Field(TestGames.Inline("Rock", "{0}", "Artifact"), me); // ties the Treasure at 0
		var edict = Hand(
			new CardDefinition(
				TestGames.Inline("Edict", "{1}", "Instant").Data,
				[
					new SpellEffectsComponent(
						[new SacrificeGreatestManaValue(Ref.You, CardType.Artifact)]
					),
				]
			),
			me
		);
		Cast(edict, []);
		Assert.That(s.IsOnBattlefield(treasure), Nu.True, "You can't sacrifice Treasure tokens");
		Assert.That(
			s.IsOnBattlefield(rock),
			Nu.False,
			"the artifact you can sacrifice goes instead"
		);
	}

	[Test]
	public void Quark_ATreasuresOwnSacrificeThisAbility_CantBeActivated()
	{
		var quark = Field(Trk("Quark, Humble Proprietor"), me);
		var treasure = TokenOnField(
			TreasureWith(
				new ActivatedAbilityComponent(ManaCost.None, [new DrawCards(Ref.You, 1)])
				{
					Costs = [new SacrificeSelfCost()],
				}
			)
		);
		Assert.That(Activations(treasure), Nu.Empty, "its cost is a sacrifice it can't make");
		s = s.Sacrifice(quark);
		Assert.That(Activations(treasure), Nu.Not.Empty);
	}

	// ===== Enters tapped and attacking =====

	[Test]
	public void Kira_ACreatureEnteringWhileYouAttack_EntersTappedAndAttacking()
	{
		Field(Trk("Kira Nerys, Firebrand"), me);
		var calm = Hand(Bear, me);
		s = s.Act(new CastSpellAction { PlayerId = me, CardId = calm });
		Resolve();
		Assert.That(
			s.GetCard(calm).AttackingId,
			Nu.EqualTo(0),
			"while you control an attacking creature"
		);
		Assert.That(
			s.Power(s.GetCard(calm)),
			Nu.EqualTo(3),
			"Whenever another creature you control enters, it gets +1/+1"
		);
		s = s.UpdateObject(calm, s.GetCard(calm) with { AttackingId = them }); // TEST SETUP: declared
		var late = Hand(Bear, me);
		s = s.EnterBattlefield(late, me);
		Assert.That(s.GetCard(late).AttackingId, Nu.EqualTo(them), "enters … attacking");
		Assert.That(s.GetCard(late).IsTapped, Nu.True, "enters tapped");
		var theirs = Hand(Bear, them);
		s = s.EnterBattlefield(theirs, them);
		Assert.That(s.GetCard(theirs).AttackingId, Nu.EqualTo(0), "a creature YOU control");
	}

	// ===== Exiled with this Saga, sharing a card type =====

	[Test]
	public void AllGoodThings_ExilesACardEachChapter_AndGainsTenWhenTheyShareAType()
	{
		s = TestGames.EmptyHands(s);
		var saga = Hand(Trk("All Good Things..."), me);
		var exiled = new List<int>();
		var bear = Hand(Bear, me);
		exiled.Add(bear);
		s = s.Act(new CastSpellAction { PlayerId = me, CardId = saga });
		Resolve(On<ChooseCardsFromHandChoice>(bear));
		Assert.That(s.IsIn(bear, me, ZoneType.Exile), Nu.True, "then exile a card from your hand");
		for (var chapter = 2; chapter <= 3; chapter++)
		{
			var next = Hand(Bear, me);
			exiled.Add(next);
			s = TestGames.PassUntil(
				s,
				x =>
					x.GetPendingChoice()
						is ChooseCardsFromHandChoice { Prompt: "Exile a card from your hand" }
			);
			Assert.That(s.GetPendingChoice()!.MinChoices, Nu.EqualTo(1), "not a may");
			s = s.Choose([next]);
		}
		var life = Life(me);
		var turn = s.GetGame().TurnNumber;
		s = TestGames.PassUntil(
			s,
			x => x.GetPlayer(me).Life >= life + 10 || x.GetGame().TurnNumber > turn + 3
		);
		Assert.That(
			Life(me),
			Nu.EqualTo(life + 10),
			"IV — If all cards exiled … share a card type"
		);
		Assert.That(s.IsOnBattlefield(saga), Nu.False, "Sacrifice after IV");
	}

	[Test]
	public void AllGoodThings_ExiledCardsWithNoTypeInCommon_GainNothing()
	{
		var saga = Field(Trk("All Good Things..."), me);
		var context = System
			.Collections.Immutable.ImmutableDictionary<string, object>.Empty.Add(
				ContextKeys.Controller,
				me
			)
			.Add(ContextKeys.Source, saga);
		void ExileLinked(CardDefinition card)
		{
			int id;
			(s, id) = TestGames.Put(s, me, card, ZoneType.Exile);
			var by = new ExiledBy(saga, s.GetCard(saga).Incarnation);
			s = s.UpdateObject(id, (Card)s.GetCard(id).WithComponent(by)); // TEST SETUP: linked
		}
		var share = new ExiledWithSourceShareACardType();
		Assert.That(share.Holds(s, context), Nu.False, "none exiled");
		ExileLinked(Bear);
		ExileLinked(TestCards.Typed(Bear, "Artifact Creature — Golem"));
		Assert.That(
			share.Holds(s, context),
			Nu.True,
			"creature and artifact creature share creature"
		);
		ExileLinked(TestCards.Fodder);
		Assert.That(share.Holds(s, context), Nu.False, "a sorcery shares nothing with them");
	}

	// ===== That player may draw; can't attack this turn; you win =====

	[Test]
	public void Peace_TheActivePlayerMayDraw_ThenCantAttack_AndAPeaceCounterGoesOn()
	{
		var peace = Field(Trk("Peace in Our Time"), me);
		var bear = Field(Bear, me);
		var theirBear = Field(Bear, them);
		s = TestGames.PassUntil(s, x => x.GetPendingChoice() is YesNoChoice);
		Assert.That(s.DecidingPlayerId(), Nu.EqualTo(me), "on each player's turn, that player");
		var hand = HandCount(me);
		s = s.Choose([1]);
		Assert.That(HandCount(me), Nu.EqualTo(hand + 1), "may draw a card");
		Assert.That(Combat.CanAttack(s, s.GetCard(bear)), Nu.False, "they can't attack this turn");
		Assert.That(
			s.GetCard(peace).CountersOf(CounterKinds.Peace),
			Nu.EqualTo(1),
			"you put a peace counter on this enchantment"
		);
		s = TestGames.PassUntil(
			s,
			x => x.GetPendingChoice() is YesNoChoice && x.DecidingPlayerId() == them
		);
		var theirHand = HandCount(them);
		s = s.Choose([1]);
		Assert.That(HandCount(them), Nu.EqualTo(theirHand + 1), "THAT player draws");
		Assert.That(Combat.CanAttack(s, s.GetCard(theirBear)), Nu.False);
		Assert.That(s.GetCard(peace).CountersOf(CounterKinds.Peace), Nu.EqualTo(2), "you put");
	}

	[Test]
	public void Peace_TwelvePeaceCounters_YouWinAtYourUpkeep()
	{
		var peace = Field(Trk("Peace in Our Time"), me);
		s = s.PutCounters(peace, CounterKinds.Peace, 11); // TEST SETUP
		var turn = s.GetGame().TurnNumber;
		s = TestGames.PassUntil(s, x => x.IsOver() || x.GetGame().TurnNumber > turn + 2);
		Assert.That(s.IsOver(), Nu.False, "eleven is not twelve");
		s = s.PutCounters(peace, CounterKinds.Peace, 1);
		s = TestGames.PassUntil(s, x => x.IsOver() || x.GetGame().TurnNumber > turn + 4);
		Assert.That(s.IsOver() && s.WinnerId() == me, Nu.True, "twelve or more: you win the game");
	}

	// ===== You gain hexproof; damage to you prevented =====

	[Test]
	public void NeedsOfTheMany_SacrificeACreature_ProtectsYoursAndYou_ThenExilesItself()
	{
		var fodder = Field(Bear, me);
		var kept = Field(Bear, me);
		var attacker = Field(Bear, them);
		var needs = Hand(Trk("The Needs of the Many"), me);
		s = s.Act(Casts(needs).Single(c => c.SacrificeId == fodder));
		Resolve();
		Assert.That(s.IsOnBattlefield(fodder), Nu.False, "sacrifice a creature");
		Assert.That(s.Has(s.GetCard(kept), "Hexproof"), Nu.True);
		Assert.That(s.Has(s.GetCard(kept), "Indestructible"), Nu.True);
		Assert.That(s.IsIn(needs, me, ZoneType.Exile), Nu.True, "Exile The Needs of the Many");
		var shock = Hand(TestCards.Shock(3, TargetSpec.Player()), them);
		var targets = TargetSpec.Player().Candidates(s, them, shock).ToList();
		Assert.That(targets, Does.Not.Contain(me), "You gain hexproof until end of turn");
		Assert.That(targets, Does.Contain(them), "your hexproof, not theirs");
		var life = Life(me);
		s = Damage.Deal(s, s.GetCard(attacker), me, 2, combat: true);
		Assert.That(Life(me), Nu.EqualTo(life), "Prevent all damage that would be dealt to you");
		s = Damage.Deal(s, s.GetCard(kept), them, 2);
		Assert.That(Life(them), Nu.EqualTo(20 - 2), "to you only");
	}

	[Test]
	public void NeedsOfTheMany_PreventedCombatDamage_IsNoCombatDamageDealtToThatPlayer()
	{
		var hitter = Field(
			TestCards.WithTrigger(
				Bear,
				TriggerEvent.OpponentsDealtCombatDamageDuringYourTurn,
				new DrawCards(Ref.You, 1)
			),
			me
		);
		s = s.UpdateObject(hitter, s.GetCard(hitter) with { AttackingId = them }); // TEST SETUP: unblocked
		s = GetEmblem.ToCommandZone(
			s,
			them,
			"The Needs of the Many (this turn)",
			[new PreventAllDamageToYou()]
		);
		var hand = HandCount(me);
		s = Combat.DealDamage(s, firstStrikeStep: false);
		s = TestGames.PassUntil(s, x => x.GetGame().Step != Step.PreCombatMain); // its trigger, if any, resolves
		Assert.That(Life(them), Nu.EqualTo(20), "Prevent all damage that would be dealt to you");
		Assert.That(
			HandCount(me),
			Nu.EqualTo(hand),
			"none was dealt, so nothing \"deals combat damage\""
		);
	}

	// ===== Whenever a player casts; counters removed from among =====

	[Test]
	public void Data_GrowsOnAnyPlayersNoncreatureSpell_AndRemovesThreeCountersFromAmongToDraw()
	{
		var data = Field(Trk("Lieutenant Commander Data"), me);
		var bear = Field(Bear, me);
		s = s.PutCounters(bear, CounterKinds.PlusOne, 2); // TEST SETUP
		TheirSpellOnTheStack(GainThree);
		Resolve();
		Assert.That(
			s.GetCard(data).PlusOneCounters,
			Nu.EqualTo(1),
			"Whenever a player casts a noncreature spell"
		);
		var hand = HandCount(me);
		s = s.Act(Activations(data).Single());
		var picks = new Queue<int>(new[] { bear, bear, data });
		var asked = 0;
		int Pick()
		{
			asked++;
			return picks.Dequeue();
		}
		Resolve(c =>
			c switch
			{
				ChoosePermanentChoice => [Pick()],
				_ => c.Options.Take(c.MinChoices).Select(o => o.Id),
			}
		);
		Assert.That(asked, Nu.EqualTo(3), "Remove three counters — each one picked");
		Assert.That(s.GetCard(bear).PlusOneCounters, Nu.EqualTo(0));
		Assert.That(
			s.GetCard(data).PlusOneCounters,
			Nu.EqualTo(0),
			"from among creatures you control"
		);
		Assert.That(HandCount(me), Nu.EqualTo(hand + 1), "Draw a card");
		Assert.That(Activations(data), Nu.Empty, "no counters left to remove");
	}

	// ===== Creatures you control have "{T}: Add {G}{G}" =====

	[Test]
	public void RokTahk_YourCreaturesTapForTwoGreen_NotWhileSick_AndMurf()
	{
		var rok = Hand(Trk("Rok-Tahk, Budding Scientist"), me);
		Cast(rok, []);
		var murf = Named(me, "Murf");
		Assert.That(
			murf.IsToken && s.Has(murf, "Indestructible"),
			Nu.True,
			"Murf … with indestructible"
		);
		var bear = Field(Bear, me);
		var yield = ManaAbilities.YieldOf(s, s.GetCard(bear));
		Assert.That(
			yield.Units.Select(u => u.Single()),
			Nu.EqualTo(new[] { ManaColor.Green, ManaColor.Green }),
			"{T}: Add {G}{G}"
		);
		Assert.That(ManaAbilities.CanTap(s, s.GetCard(bear)), Nu.True);
		Assert.That(
			ManaAbilities.CanTap(s, s.GetCard(rok)),
			Nu.False,
			"a {T} ability: not while sick"
		);
		var theirs = Field(Bear, them);
		Assert.That(ManaAbilities.CanTap(s, s.GetCard(theirs)), Nu.False, "creatures YOU control");
	}

	// ===== An additional land each turn; explore =====

	[Test]
	public void Janeway_ExploresALandIntoHand_ANonlandForACounter_AndPlaysTwoLands()
	{
		var land = OnTop(TestGames.Card("Forest"), me);
		var janeway = Hand(Trk("Captain Kathryn Janeway"), me);
		Cast(janeway, []);
		Assert.That(
			s.IsIn(land, me, ZoneType.Hand),
			Nu.True,
			"Put that card into your hand if it's a land"
		);
		Assert.That(s.GetCard(janeway).PlusOneCounters, Nu.EqualTo(0));

		var spell = OnTop(TestCards.Fodder, me);
		var bear = Hand(Bear, me);
		s = s.Act(new CastSpellAction { PlayerId = me, CardId = bear });
		Resolve(On<YesNoChoice>(1));
		Assert.That(
			s.GetCard(bear).PlusOneCounters,
			Nu.EqualTo(1),
			"Otherwise, put a +1/+1 counter"
		);
		Assert.That(
			s.IsIn(spell, me, ZoneType.Graveyard),
			Nu.True,
			"or put it into your graveyard"
		);

		var second = Hand(TestGames.Card("Plains"), me);
		Hand(TestGames.Card("Island"), me);
		s = s.Act(new PlayLandAction { PlayerId = me, CardId = land });
		s = s.Act(new PlayLandAction { PlayerId = me, CardId = second });
		Assert.That(s.IsOnBattlefield(second), Nu.True, "an additional land on each of your turns");
		Assert.That(s.LegalActions().OfType<PlayLandAction>(), Nu.Empty, "one additional");
	}
}
