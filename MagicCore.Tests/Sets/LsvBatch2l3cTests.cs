using ImmutableGameObjects;
using MagicCore.Actions;
using MagicCore.Cards;
using MagicCore.Effects;
using MagicCore.Sets;
using MagicCore.Targeting;
using Nu = NUnit.Framework.Is;

namespace MagicCore.Tests.Sets;

/// <summary>
/// MC-4.4l3c, LSVPower — colourless' trigger, combat, equipment and casting options, each against its printed text: a
/// cast trigger reading its own kick (Sowing Mycospawn), annihilator and the whole graveyard shuffled (Emrakul), X
/// targets on an activated ability (Candelabra of Tawnos), the Pilot's crew power (Reckoner Bankbuster), a graveyard
/// trigger that attaches (Sword of the Meek), the fourth noncreature spell (The Fantasticar), protection until your
/// next turn (The One Ring), attackers that weren't blocked (Coveted Jewel), combat damage to a creature by the
/// equipped one (Kaldra Compleat), channel (Boseiju, Otawara), a Saga land and a printed mana cost (Urza's Saga).
/// </summary>
public class LsvBatch2l3cTests : FraBoardTest
{
	static IEnumerable<int> Min(ChoiceAction c) => c.Options.Take(c.MinChoices).Select(o => o.Id);

	/// <summary>Every "you may" answered yes; any other choice with its minimum.</summary>
	static IEnumerable<int> Yes(ChoiceAction c) =>
		c switch
		{
			YesNoChoice => [1],
			_ => Min(c),
		};

	/// <summary>The search takes the first card offered; any other choice its minimum.</summary>
	static IEnumerable<int> TakingTheFirst(ChoiceAction c) =>
		c switch
		{
			ChooseFromLibraryChoice => c.Options.Take(1).Select(o => o.Id),
			_ => Min(c),
		};

	int Lands(int player) =>
		s.CardsIn(player, ZoneType.Battlefield).Count(c => c.Is(CardType.Land));

	IEnumerable<Card> Tokens() => s.CardsIn(me, ZoneType.Battlefield).Where(c => c.IsToken);

	/// <summary>TEST SETUP: the permanent untapped again, as though a turn had passed.</summary>
	void Untap(int id) => s = s.UpdateObject(id, s.GetCard(id) with { IsTapped = false });

	/// <summary>That step of MY next turn, a player holding priority — what triggers there is on the stack, unresolved.</summary>
	void MyNext(Step step)
	{
		var turn = s.GetGame().TurnNumber;
		s = TestGames.PassUntil(
			s,
			x =>
				x.GetGame().TurnNumber > turn
				&& x.GetGame().ActivePlayerId == me
				&& x.GetGame().Step == step
				&& x.GetGame().PriorityPlayerId != 0
		);
	}

	/// <summary>Their next turn: that creature of theirs attacks me, blocked by this one of mine (0: unblocked) — to their second main phase.</summary>
	void TheyAttack(int attacker, int blocker = 0)
	{
		for (
			var i = 0;
			i < 400
				&& !(s.GetGame().ActivePlayerId == them && s.GetGame().Step == Step.PostCombatMain);
			i++
		)
			s = s.GetPendingChoice() switch
			{
				DeclareAttackerChoice d => s.Choose([d.CreatureId == attacker ? me : 0]),
				DeclareBlockerChoice b => s.Choose([b.CreatureId == blocker ? attacker : 0]),
				{ } c => s.Choose(Min(c)),
				null => s.Act(new PassPriorityAction { PlayerId = s.GetGame().PriorityPlayerId }),
			};
	}

	[TestCase(false)]
	[TestCase(true)]
	public void SowingMycospawn_FetchesALand_AndOnlyKickedExilesTargetLand(bool kicked)
	{
		Field(Lsv("Wasteland"), me); // the kicker's {C}
		var mycospawn = Hand(Lsv("Sowing Mycospawn"), me);
		var theirs = s.CardsIn(them, ZoneType.Battlefield).First(c => c.Is(CardType.Land)).Id;
		var lands = Lands(me);

		s = s.Act(
			new CastSpellAction
			{
				PlayerId = me,
				CardId = mycospawn,
				Kicked = kicked,
			}
		);
		var asked = ResolveAsking(c =>
			c switch
			{
				ChooseTargetChoice => [theirs],
				_ => TakingTheFirst(c),
			}
		);

		Assert.That(Lands(me), Nu.EqualTo(lands + 1), "search your library for a land card");
		Assert.That(
			asked.OfType<ChooseTargetChoice>().Any(),
			Nu.EqualTo(kicked),
			"the second trigger only if it was kicked"
		);
		Assert.That(InExile(theirs), Nu.EqualTo(kicked), "exile target land");
		Assert.That(Alive(mycospawn));
	}

	[Test]
	public void Emrakul_CastingItTakesAnExtraTurn()
	{
		var emrakul = Hand(Lsv("Emrakul, the Aeons Torn"), me);

		Cast(emrakul, []);

		Assert.That(Alive(emrakul));
		Assert.That(s.GetGame().ExtraTurns, Does.Contain(me), "take an extra turn after this one");
	}

	[Test]
	public void Emrakul_ProtectionFromSpellsThatAreOneOrMoreColours()
	{
		var emrakul = Field(Lsv("Emrakul, the Aeons Torn"), me);
		var red = new CardDefinition(
			new CardData(
				"Bolt",
				"normal",
				"{R}",
				0,
				"Instant",
				null,
				null,
				null,
				null,
				["R"],
				[],
				null
			),
			[
				new SpellEffectsComponent([new DealDamage(Ref.Target(), 3)])
				{
					Targets = [TargetSpec.Creature],
				},
			]
		);
		var bolt = Hand(red, me);
		var kill = Hand(TestCards.Kill(), me);

		Assert.That(Casts(bolt).Any(c => c.Targets.Contains(emrakul)), Nu.False, "a red spell");
		Assert.That(Casts(kill).Any(c => c.Targets.Contains(emrakul)), "a colourless spell");
	}

	[Test]
	public void Emrakul_Annihilator6_AndItsOwnersGraveyardIsShuffledIntoTheirLibrary()
	{
		var emrakul = Field(Lsv("Emrakul, the Aeons Torn"), me);
		var theirs = s.CardsIn(them, ZoneType.Battlefield).Count();
		var life = Life(them);

		AttackWith(Min, emrakul);

		Assert.That(
			s.CardsIn(them, ZoneType.Battlefield).Count(),
			Nu.EqualTo(theirs - 6),
			"annihilator 6: the defending player sacrifices six permanents"
		);
		Assert.That(Life(them), Nu.EqualTo(life - 15));

		var bear = Put(TestGames.Creature("Bear", "2/2"), me, ZoneType.Graveyard);
		Cast(Hand(TestCards.Kill(), me), [], emrakul);

		Assert.That(
			s.CardsIn(me, ZoneType.Graveyard),
			Nu.Empty,
			"put into a graveyard from anywhere"
		);
		Assert.That(Library(me), Has.Member(emrakul));
		Assert.That(
			Library(me),
			Has.Member(bear),
			"its owner shuffles their graveyard into their library"
		);
	}

	[Test]
	public void CandelabraOfTawnos_UntapsXTargetLands_OneActivationForEachX()
	{
		var candelabra = Field(Lsv("Candelabra of Tawnos"), me);
		var lands = s.CardsIn(me, ZoneType.Battlefield)
			.Where(c => c.Is(CardType.Land))
			.Select(c => c.Id)
			.Take(3)
			.ToList();
		foreach (var land in lands)
			TapIt(land);
		var untapped = UntappedLands();
		Assert.That(
			Activations(candelabra).Count(a => a.X == 2),
			Nu.EqualTo(1),
			"one activation for each X, never one for each set of lands"
		);
		Assert.That(Activations(candelabra).Any(a => a.X == 0), Nu.False);

		s = s.Act(Activations(candelabra).Single(a => a.X == 2));
		var asked = ResolveAsking(c =>
			c switch
			{
				ChooseXTargetsChoice => lands.Take(2),
				_ => Min(c),
			}
		);

		Assert.That(asked.OfType<ChooseXTargetsChoice>().Single().MinChoices, Nu.EqualTo(2));
		Assert.That(
			lands.Select(land => s.GetCard(land).IsTapped),
			Nu.EqualTo(new[] { false, false, true }),
			"untap X target lands"
		);
		Assert.That(UntappedLands(), Nu.EqualTo(untapped), "{X}: two tapped to pay, two untapped");
		Assert.That(s.GetCard(candelabra).IsTapped, "{T}");
	}

	[Test]
	public void CandelabraOfTawnos_TheAiUntapsItsOwnTappedLands()
	{
		var candelabra = Field(Lsv("Candelabra of Tawnos"), me);
		var tapped = s.CardsIn(me, ZoneType.Battlefield)
			.Where(c => c.Is(CardType.Land))
			.Select(c => c.Id)
			.TakeLast(2)
			.ToList();
		foreach (var land in tapped)
			TapIt(land);
		TapIt(s.CardsIn(them, ZoneType.Battlefield).First(c => c.Is(CardType.Land)).Id);

		s = s.Act(Activations(candelabra).Single(a => a.X == 3));
		var answer = new global::MagicCore.Ai.GreedyAi(me).Decide(s).Answer!;

		Assert.That(answer, Has.Count.EqualTo(3));
		Assert.That(answer, Nu.SupersetOf(tapped), "its own tapped lands first");
		Assert.That(
			answer.All(id => s.GetCard(id).ControllerId == me),
			"then its own others — never their tapped land"
		);
	}

	[Test]
	public void CandelabraOfTawnos_TheAiUntapsTheLandMakingTheMost()
	{
		var candelabra = Field(Lsv("Candelabra of Tawnos"), me);
		var tomb = Field(Lsv("Ancient Tomb"), me); // after the twenty basics: the pick once took the first land

		s = s.Act(Activations(candelabra).Single(a => a.X == 1));
		var answer = new global::MagicCore.Ai.GreedyAi(me).Decide(s).Answer!;

		Assert.That(answer, Nu.EqualTo(new[] { tomb }));
	}

	[Test]
	public void CandelabraOfTawnos_TheSearchIsOfferedIt_OnlyForLandsMakingMoreThanOneMana()
	{
		var candelabra = Field(Lsv("Candelabra of Tawnos"), me);
		IEnumerable<int> Searched() =>
			global::MagicCore
				.Ai.Evaluator.SearchMoves(s)
				.OfType<ActivateAbilityAction>()
				.Where(a => a.CardId == candelabra)
				.Select(a => a.X);

		Assert.That(Activations(candelabra).Count(), Nu.GreaterThan(1), "the rules offer every X");
		Assert.That(Searched(), Nu.Empty, "basics only: X mana spent for X mana untapped");

		Field(Lsv("Ancient Tomb"), me);

		Assert.That(Searched(), Nu.EqualTo(new[] { 1 }), "one land making two: X = 1 alone");
	}

	[Test]
	public void ReckonerBankbuster_ThreeDraws_ThenATreasureAndAPilotThatCrewsItAlone()
	{
		var bear = Field(TestGames.Creature("Bear", "2/2"), me);
		var bankbuster = Hand(Lsv("Reckoner Bankbuster"), me);
		Cast(bankbuster, []);
		Assert.That(s.GetCard(bankbuster).ChargeCounters, Nu.EqualTo(3));
		Assert.That(
			Activations(bankbuster).Any(a => a.AbilityIndex == 1),
			Nu.False,
			"crew 3: a 2/2 alone can't"
		);

		for (var i = 1; i <= 3; i++)
		{
			var hand = HandCount(me);
			s = s.Act(Activations(bankbuster).Single(a => a.AbilityIndex == 0));
			Resolve();
			Assert.That(HandCount(me), Nu.EqualTo(hand + 1), "draw a card");
			Assert.That(
				Tokens().Select(c => c.Name).Order(),
				i < 3 ? Nu.Empty : Nu.EqualTo(new[] { "Pilot", "Treasure" }),
				"then if there are no charge counters: a Treasure and a Pilot"
			);
			Untap(bankbuster);
		}

		var pilot = Tokens().Single(c => c.Name == "Pilot");
		Assert.That(s.Power(pilot), Nu.EqualTo(1), "a 1/1");
		s = s.Act(Activations(bankbuster).Single(a => a.AbilityIndex == 1));
		Resolve(c =>
			c switch
			{
				ChoosePermanentChoice => [pilot.Id],
				_ => Min(c),
			}
		);

		Assert.That(
			s.GetCard(bankbuster).Is(CardType.Creature),
			"crews Vehicles as though its power were 2 greater: crew 3 alone"
		);
		Assert.That(s.GetCard(pilot.Id).IsTapped);
		Assert.That(s.GetCard(bear).IsTapped, Nu.False, "the Pilot's 3 is the whole crew");
	}

	[Test]
	public void SwordOfTheMeek_ReturnsFromTheGraveyardAttachedToA1_1ThatEnters()
	{
		var sword = Put(Lsv("Sword of the Meek"), me, ZoneType.Graveyard);
		foreach (var size in new[] { "2/1", "1/2" })
		{
			var other = Hand(TestGames.Creature("Scout", size), me);
			Assert.That(
				CastAsking(other, Yes).OfType<YesNoChoice>(),
				Nu.Empty,
				$"a {size} is no 1/1"
			);
		}
		Assert.That(InGraveyard(sword));

		var squire = Hand(TestGames.Creature("Squire", "1/1"), me);
		CastAsking(squire, Yes);

		Assert.That(Alive(sword), "you may return this card from your graveyard");
		Assert.That(
			s.GetCard(sword).AttachedToId,
			Nu.EqualTo(squire),
			"then attach it to that creature"
		);
		var equipped = s.GetCard(squire);
		Assert.That((s.Power(equipped), s.Toughness(equipped)), Nu.EqualTo((2, 3)), "+1/+2");
	}

	[Test]
	public void TheFantasticar_ACreatureForANoncreatureSpell_AndTheFourthMakesFourConstructs()
	{
		var car = Field(Lsv("The Fantasticar"), me);
		IEnumerable<int> Answer(ChoiceAction c) =>
			c switch
			{
				YesNoChoice => [1],
				ChoosePermanentChoice => [car],
				_ => Min(c),
			};
		Assert.That(s.GetCard(car).Is(CardType.Creature), Nu.False, "a Vehicle with no crew");

		for (var i = 1; i <= 4; i++)
		{
			if (i == 2) // a creature spell is not one of the four
				Cast(Hand(TestGames.Creature("Bear", "2/2"), me), []);
			CastAsking(Hand(TestCards.Fodder, me), Answer);
			if (i < 4)
			{
				Assert.That(
					s.GetCard(car).Is(CardType.Creature),
					"an artifact creature until end of turn"
				);
				Assert.That(Tokens(), Nu.Empty, $"noncreature spell {i} is not the fourth");
			}
		}

		Assert.That(InGraveyard(car), "you may sacrifice The Fantasticar");
		var constructs = Tokens().ToList();
		Assert.That(constructs, Has.Count.EqualTo(4));
		Assert.That(
			constructs.All(c =>
				c.Name == "Construct" && s.Power(c) == 4 && s.Has(c, "Flying") && s.Has(c, "Haste")
			),
			"four 4/4 Constructs with flying and haste"
		);
	}

	[Test]
	public void TheOneRing_ProtectionFromEverythingUntilYourNextTurn()
	{
		var ogre = Field(TestGames.Creature("Ogre", "3/3"), them);
		var ring = Hand(Lsv("The One Ring"), me);
		Cast(ring, []);
		var life = Life(me);

		TheyAttack(ogre);

		Assert.That(s.GetCard(ogre).IsTapped, "it attacked");
		Assert.That(
			Life(me),
			Nu.EqualTo(life),
			"protection from everything: the damage is prevented"
		);
		var shock = Hand(TestCards.Shock(2, TargetSpec.AnyTarget()), them);
		Assert.That(Casts(shock).Any(c => c.Targets.Contains(ogre)), "their spell has a target …");
		Assert.That(Casts(shock).Any(c => c.Targets.Contains(me)), Nu.False, "… never you");

		MyNext(Step.Upkeep);

		Assert.That(s.ThisTurnEffects<PreventAllDamageToYou>(me), Nu.Empty, "until your next turn");
		Assert.That(s.ThisTurnEffects<YouHaveHexproof>(me), Nu.Empty);
	}

	[Test]
	public void TheOneRing_DrawsACardForEachBurdenCounter_AndLosesThatMuchLifeEachUpkeep()
	{
		var ring = Field(Lsv("The One Ring"), me);
		var hand = HandCount(me);

		s = s.Act(Activations(ring).Single());
		Resolve();
		Untap(ring);
		s = s.Act(Activations(ring).Single());
		Resolve();

		Assert.That(
			HandCount(me),
			Nu.EqualTo(hand + 1 + 2),
			"a burden counter, then a card for each"
		);
		var life = Life(me);
		MyNext(Step.Upkeep);
		Resolve();
		Assert.That(Life(me), Nu.EqualTo(life - 2), "you lose 1 life for each burden counter");
	}

	[Test]
	public void CovetedJewel_DrawsThree_TapsForThree_AndAnUnblockedAttackersPlayerTakesIt()
	{
		var ogre = Field(TestGames.Creature("Ogre", "3/3"), them);
		var jewel = Hand(Lsv("Coveted Jewel"), me);
		var hand = HandCount(me);
		Cast(jewel, []);
		Assert.That(HandCount(me), Nu.EqualTo(hand - 1 + 3), "when this enters, draw three cards");
		s = s.Act(
			new TapForManaAction
			{
				PlayerId = me,
				CardId = jewel,
				Color = ManaColor.Red,
			}
		);
		Assert.That(
			s.GetPlayer(me).ManaPool[ManaColor.Red],
			Nu.EqualTo(3),
			"three mana of any one color"
		);
		var theirs = HandCount(them);

		TheyAttack(ogre);

		Assert.That(
			s.GetCard(jewel).ControllerId,
			Nu.EqualTo(them),
			"that player gains control of it"
		);
		Assert.That(s.GetCard(jewel).IsTapped, Nu.False, "untap it");
		Assert.That(HandCount(them), Nu.EqualTo(theirs + 1 + 3), "their turn's card, and three");
	}

	[TestCase(true)]
	[TestCase(false)]
	public void CovetedJewel_StaysOnlyWhenTheAttackerIsBlocked(bool blocked)
	{
		var ogre = Field(TestGames.Creature("Ogre", "3/3"), them);
		var wall = Field(TestGames.Creature("Wall", "0/8"), me);
		var jewel = Field(Lsv("Coveted Jewel"), me);

		TheyAttack(ogre, blocked ? wall : 0);

		Assert.That(s.GetCard(wall).Damage, Nu.EqualTo(blocked ? 3 : 0), "it attacked");
		Assert.That(s.GetCard(jewel).ControllerId, Nu.EqualTo(blocked ? me : them));
	}

	[Test]
	public void KaldraCompleat_LivingWeapon_AndItsCreatureExilesWhatItDealsCombatDamageTo()
	{
		var wall = Field(TestGames.Creature("Wall", "0/8"), them);
		var kaldra = Hand(Lsv("Kaldra Compleat"), me);

		Cast(kaldra, []);

		var germ = Tokens().Single();
		Assert.That(s.GetCard(kaldra).AttachedToId, Nu.EqualTo(germ.Id), "living weapon");
		Assert.That(
			(s.Power(germ), s.Toughness(germ)),
			Nu.EqualTo((5, 5)),
			"a 0/0 Germ with +5/+5"
		);
		Assert.That(
			new[] { "First strike", "Trample", "Indestructible", "Haste" }.All(k => s.Has(germ, k))
		);

		AttackWith(
			c =>
				c switch
				{
					DeclareBlockerChoice => [germ.Id],
					_ => Min(c),
				},
			germ.Id
		);

		Assert.That(
			InExile(wall),
			"whenever this creature deals combat damage to a creature, exile it"
		);
	}

	[Test]
	public void KaldraCompleat_ACreatureThatDiedOfTheDamageStaysInTheGraveyard()
	{
		var bear = Field(TestGames.Creature("Bear", "2/2"), them);
		Cast(Hand(Lsv("Kaldra Compleat"), me), []);
		var germ = Tokens().Single().Id;

		AttackWith(
			c =>
				c switch
				{
					DeclareBlockerChoice => [germ],
					_ => Min(c),
				},
			germ
		);

		Assert.That(
			InGraveyard(bear),
			"dead before the ability resolves: a new object, never exiled"
		);
	}

	[Test]
	public void KaldraCompleat_TheExileIsTheCreaturesOwnAbility_GoneWithItsOthers()
	{
		var wall = Field(TestGames.Creature("Wall", "0/20"), them);
		var ogre = Field(TestGames.Creature("Ogre", "3/3"), me);
		var kaldra = Field(Lsv("Kaldra Compleat"), me);
		s = s.Act(Activations(kaldra).Single(a => a.Targets.Contains(ogre))); // equip {7}
		Resolve();
		var strip = TestCards.Targeted(
			"Strip",
			TargetSpec.Creature,
			new ModifyUntilEndOfTurn(Ref.Target(), 0, 0) { LosesAllAbilities = true }
		);
		Cast(Hand(strip, me), [], ogre);
		Assert.That(s.Has(s.GetCard(ogre), "First strike"), Nu.False, "it lost all abilities");

		AttackWith(
			c =>
				c switch
				{
					DeclareBlockerChoice => [ogre],
					_ => Min(c),
				},
			ogre
		);

		Assert.That(
			s.GetCard(wall).Damage,
			Nu.EqualTo(3 + 5),
			"still +5/+5: that is the Equipment's"
		);
		Assert.That(Alive(wall), "no exile: that ability was the creature's");
	}

	[Test]
	public void Boseiju_ChannelDestroysTheirArtifact_AndTheyMayFetchALandWithABasicLandType()
	{
		var ring = Field(Lsv("Sol Ring"), them);
		var boseiju = Hand(Lsv("Boseiju, Who Endures"), me);
		var basic = s.CardsIn(them, ZoneType.Battlefield).First(c => c.Is(CardType.Land)).Id;
		Assert.That(Activations(boseiju).Any(a => a.Targets.Contains(basic)), Nu.False, "nonbasic");
		var (lands, untapped) = (Lands(them), UntappedLands());

		s = s.Act(Activations(boseiju).Single(a => a.Targets.Contains(ring)));
		var asked = ResolveAsking(TakingTheFirst);

		Assert.That(InGraveyard(boseiju), "discard this card");
		Assert.That(untapped - UntappedLands(), Nu.EqualTo(2), "{1}{G}");
		Assert.That(InGraveyard(ring), "destroy target artifact an opponent controls");
		Assert.That(asked.OfType<ChooseFromLibraryChoice>().ToList(), Has.Count.EqualTo(1));
		Assert.That(Lands(them), Nu.EqualTo(lands + 1), "that player may search for a land");
		Assert.That(
			s.CardsIn(them, ZoneType.Battlefield).Any(c => c.Is(CardType.Land) && c.IsTapped),
			Nu.False,
			"untapped"
		);
	}

	[Test]
	public void Otawara_ChannelReturnsATarget_OneLessForEachLegendaryCreatureYouControl()
	{
		var ogre = Field(TestGames.Creature("Ogre", "3/3"), them);
		var otawara = Hand(Lsv("Otawara, Soaring City"), me);
		var second = Hand(Lsv("Otawara, Soaring City"), me);
		var untapped = UntappedLands();

		s = s.Act(Activations(otawara).Single(a => a.Targets.Contains(ogre)));
		Resolve();

		Assert.That(
			s.IsIn(ogre, them, ZoneType.Hand),
			"return target creature to its owner's hand"
		);
		Assert.That(untapped - UntappedLands(), Nu.EqualTo(4), "{3}{U}");

		var emrakul = Field(Lsv("Emrakul, the Aeons Torn"), me);
		untapped = UntappedLands();
		s = s.Act(Activations(second).Single(a => a.Targets.Contains(emrakul)));
		Resolve();

		Assert.That(untapped - UntappedLands(), Nu.EqualTo(3), "{1} less: a legendary creature");
		var land = Play("Otawara, Soaring City");
		Assert.That(CanTapFor(land, ManaColor.Blue), "{T}: Add {U}");
	}

	[Test]
	public void UrzasSaga_ALandSagaGainsItsAbilities_ThenFetchesAnArtifactWithManaCostZeroOrOne()
	{
		EmptyBoard();
		Field(TestGames.Card("Island"), me);
		Field(TestGames.Card("Island"), me);
		var ring = Put(Lsv("Sol Ring"), me, ZoneType.Library); // {1}
		var ballista = Put(Lsv("Walking Ballista"), me, ZoneType.Library); // {X}{X}: mana value 0
		var hole = Put(Lsv("Portable Hole"), me, ZoneType.Library); // {W}: mana value 1

		var saga = Play("Urza's Saga");
		Resolve();
		Assert.That(CanTapFor(saga, ManaColor.Colorless), "I — this Saga gains \"{T}: Add {C}.\"");
		Assert.That(Activations(saga), Nu.Empty, "II not yet");

		MyNext(Step.PreCombatMain);
		Resolve();
		s = s.Act(Activations(saga).Single());
		Resolve();
		Assert.That(
			s.Power(Tokens().Single()),
			Nu.EqualTo(1),
			"II — {2}, {T}: a 0/0 Construct with +1/+1 for each artifact you control"
		);

		MyNext(Step.PreCombatMain);
		var asked = ResolveAsking(c =>
			c switch
			{
				ChooseFromLibraryChoice => [ring],
				_ => Min(c),
			}
		);

		var offered = Offered<ChooseFromLibraryChoice>(asked).ToList();
		Assert.That(offered, Has.Member(ring), "mana cost {1}");
		Assert.That(offered, Has.No.Member(ballista), "{X}{X} is not {0}");
		Assert.That(offered, Has.No.Member(hole), "{W} is not {1}");
		Assert.That(Alive(ring), "III — put it onto the battlefield");
		Assert.That(InGraveyard(saga), "sacrificed after III");
	}
}
