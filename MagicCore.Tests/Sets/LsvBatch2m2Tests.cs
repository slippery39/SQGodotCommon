using System.Collections.Immutable;
using ImmutableGameObjects;
using MagicCore.Actions;
using MagicCore.Cards;
using MagicCore.Effects;
using MagicCore.Rules;
using MagicCore.Sets;
using MagicCore.Targeting;
using MagicCore.Turns;
using MIs = MagicCore.Filters.Is;
using Nu = NUnit.Framework.Is;

namespace MagicCore.Tests.Sets;

/// <summary>
/// MC-4.4m2b + m2c, LSVPower — multicolour's combat, trigger, permanent, copy and mana options, each against its
/// printed text: ninjutsu (Fallen Shinobi), a creature planeswalker (Kaito), entering attacking (Raph &amp; Mikey),
/// the monarch's holder (Okoye, Aragorn), combat damage to a planeswalker (Psychic Frog), a player becoming a target
/// (Leovold), "twice each turn" (Nadu), a spell's targets (Dack Fayden), monstrosity (Alpha Deathclaw), airbend and
/// waterbend (Aang), a die's rows (Comet); a copy for the other player (Fractured Identity), a graveyard card copied
/// (Superior Spider-Man), a copy that is an artifact too (Saheeli), abilities lost for good and an exchange's targets
/// (Oko), types and base P/T for good (the Figures), an opponent's permanent chosen (Chaos Defiler), a hand card or a
/// creature (Cloak and Dagger), a card per type (Atraxa), and three mana moves (Vivi, Squandered Resources, Loot).
/// </summary>
public class LsvBatch2m2Tests : FraBoardTest
{
	static IEnumerable<int> Min(ChoiceAction c) => c.Options.Take(c.MinChoices).Select(o => o.Id);

	static IEnumerable<int> First(ChoiceAction c) => c.Options.Take(1).Select(o => o.Id);

	int Walker(string name, int loyalty)
	{
		var walker = Field(Lsv(name), me);
		s = s.PutCounters(walker, CounterKinds.Loyalty, loyalty);
		return walker;
	}

	void Loyalty(int walker, int cost, Func<ChoiceAction, IEnumerable<int>>? answer = null)
	{
		var index = global::MagicCore
			.Actions.Loyalty.AbilitiesOf(s, s.GetCard(walker))
			.FindIndex(a => a.Cost == cost);
		s = s.Act(
			s.LegalActions()
				.OfType<ActivateLoyaltyAction>()
				.First(a => a.CardId == walker && a.AbilityIndex == index)
		);
		Resolve(answer);
	}

	/// <summary>These attack what is named (a player or a planeswalker); then on to my priority once blockers are declared.</summary>
	void AttackTo(int defender, params int[] attackers)
	{
		s = TestGames.PassUntil(s, x => x.GetPendingChoice() is DeclareAttackerChoice);
		while (s.GetPendingChoice() is DeclareAttackerChoice d)
			s = s.Choose([attackers.Contains(d.CreatureId) ? defender : 0]);
	}

	void ToMyPriorityIn(Step step) =>
		s = TestGames.PassUntil(
			s,
			x => x.GetGame().Step == step && x.GetGame().PriorityPlayerId == me && x.StackIsEmpty()
		);

	void TheyCast(CardDefinition card, params int[] targets)
	{
		var spell = Hand(card, them);
		s = s.Act(new PassPriorityAction { PlayerId = me });
		s = s.Act(
			new CastSpellAction
			{
				PlayerId = them,
				CardId = spell,
				Targets = [.. targets],
			}
		);
	}

	IEnumerable<TapForManaAction> ManaMoves(int card) =>
		s.LegalActions().OfType<TapForManaAction>().Where(a => a.CardId == card);

	static CardDefinition Pump =>
		TestCards.Targeted(
			"Pump",
			TargetSpec.Creature,
			new ModifyUntilEndOfTurn(Ref.Target(), 1, 1)
		);

	// ===== m2b =====

	[Test]
	public void FallenShinobi_Ninjutsu_OnlyOnceBlockersAreDeclared_EntersTappedAndAttacking()
	{
		var bear = Field(TestGames.Creature("Bear", "2/2"), me);
		var shinobi = Hand(Lsv("Fallen Shinobi"), me);
		var second = OnTop(TestGames.Creature("Ogre", "3/3"), them);
		var first = OnTop(TestGames.Creature("Giant", "4/4"), them);
		var life = Life(them);

		AttackTo(them, bear);
		ToMyPriorityIn(Step.DeclareAttackers);
		Assert.That(Activations(shinobi), Nu.Empty, "no attacker is unblocked before blockers");
		ToMyPriorityIn(Step.DeclareBlockers);
		s = s.Act(Activations(shinobi).Single(a => a.ReturnId == bear));
		Resolve();

		Assert.That(s.IsIn(bear, me, ZoneType.Hand), Nu.True, "the unblocked attacker returned");
		var ninja = s.GetCard(shinobi);
		Assert.That(
			(Alive(shinobi), ninja.IsTapped, ninja.AttackingId),
			Nu.EqualTo((true, true, them)),
			"tapped and attacking"
		);

		ToMyPriorityIn(Step.PostCombatMain);
		Assert.That(Life(them), Nu.EqualTo(life - 5), "the Ninja's damage, not the Bear's");
		foreach (var card in new[] { first, second })
			Assert.That(
				InExile(card)
				&& s.GetCard(card).GetComponent<PlayableFromExile>() is { Free: true } p
					? p.PlayerId
					: 0,
				Nu.EqualTo(me),
				"their top two exiled, mine to play free"
			);
	}

	[Test]
	public void Kaito_DuringYourTurn_IsA34NinjaWithHexproof_DamageIsMarkedAndRemovesLoyalty()
	{
		var kaito = Hand(Lsv("Kaito, Bane of Nightmares"), me);
		CastAsking(kaito, Min);
		var card = s.GetCard(kaito);
		Assert.That(
			(card.Is(CardType.Creature), s.Power(card), s.Toughness(card), s.Has(card, "Hexproof")),
			Nu.EqualTo((true, 3, 4, true))
		);
		Assert.That(s.HasSubtype(card, "Ninja"), Nu.True);

		var bear = Field(TestGames.Creature("Bear", "2/2"), them);
		s = Damage.Deal(s, s.GetCard(bear), kaito, 2);
		Assert.That(
			(s.GetCard(kaito).Damage, s.GetCard(kaito).Loyalty),
			Nu.EqualTo((2, 2)),
			"a creature planeswalker: marked, and that many loyalty counters removed"
		);

		Loyalty(kaito, 1);
		Assert.That(s.Power(s.GetCard(kaito)), Nu.EqualTo(4), "Ninjas you control get +1/+1");

		s = TestGames.PassUntil(
			s,
			x => x.GetGame().ActivePlayerId == them && x.GetGame().PriorityPlayerId != 0
		);
		Assert.That(Alive(kaito), Nu.True);
		Assert.That(s.GetCard(kaito).Is(CardType.Creature), Nu.False, "only during your turn");
	}

	[Test]
	public void Kaito_Zero_DrawsOnlyIfAnOpponentLostLifeThisTurn()
	{
		var kaito = Walker("Kaito, Bane of Nightmares", 4);
		var hand = HandCount(me);
		s = global::MagicCore.Rules.Life.Lose(s, them, 1);

		Loyalty(kaito, 0);

		Assert.That(HandCount(me), Nu.EqualTo(hand + 1));
	}

	[Test]
	public void RaphAndMikey_Attacking_PutTheFirstCreatureRevealedOntoTheBattlefieldAttacking()
	{
		var turtles = Field(Lsv("Raph & Mikey, Troublemakers"), me);
		var giant = OnTop(TestGames.Creature("Giant", "4/4"));
		var land = OnTop(TestGames.Card("Forest"));
		var life = Life(them);

		AttackTo(them, turtles);
		ToMyPriorityIn(Step.DeclareBlockers);

		Assert.That(
			(Alive(giant), s.GetCard(giant).IsTapped, s.GetCard(giant).AttackingId),
			Nu.EqualTo((true, true, them)),
			"tapped and attacking"
		);
		Assert.That(Library(me)[^1], Nu.EqualTo(land), "the rest on the bottom");
		ToMyPriorityIn(Step.PostCombatMain);
		Assert.That(Life(them), Nu.EqualTo(life - 7 - 4));
	}

	[TestCase(true, 6)]
	[TestCase(false, 3)]
	public void Okoye_ThatCreatureAttackingTheMonarch_GainsDoubleStrike(
		bool theyAreTheMonarch,
		int damage
	)
	{
		Field(Lsv("Okoye, Mighty and Adored"), me);
		var bear = Field(TestGames.Creature("Bear", "2/2"), me);
		s = DesignationComponent.Take(
			s,
			theyAreTheMonarch ? them : me,
			DesignationComponent.Monarch
		);
		var life = Life(them);

		s = TestGames.PassUntil(s, x => x.GetPendingChoice() is ChooseTargetChoice);
		s = s.Choose([bear]);
		AttackTo(them, bear);
		ToMyPriorityIn(Step.PostCombatMain);

		Assert.That(
			Life(them),
			Nu.EqualTo(life - damage),
			"a 3/3 with its counter — twice, against the monarch"
		);
	}

	[TestCase(true)]
	[TestCase(false)]
	public void Aragorn_Attacking_IfYouAreTheMonarch_CreaturesCantBlock(bool monarch)
	{
		var aragorn = Field(Lsv("Aragorn, King of Gondor"), me);
		Field(TestGames.Creature("Wall", "0/6"), them);
		if (monarch)
			s = DesignationComponent.Take(s, me, DesignationComponent.Monarch);

		AttackTo(them, aragorn);
		var blocksAsked = false;
		for (var i = 0; i < 200 && s.GetGame().Step != Step.PostCombatMain; i++)
		{
			blocksAsked |= s.GetPendingChoice() is DeclareBlockerChoice;
			s = TestGames.PassOrChoose(s);
		}

		Assert.That(blocksAsked, Nu.EqualTo(!monarch));
	}

	[Test]
	public void PsychicFrog_CombatDamageToAPlaneswalker_DrawsACard()
	{
		var frog = Field(Lsv("Psychic Frog"), me);
		var walker = Field(TestGames.Planeswalker("Walker", 3), them);
		s = s.PutCounters(walker, CounterKinds.Loyalty, 3);
		var hand = HandCount(me);

		AttackTo(walker, frog);
		ToMyPriorityIn(Step.PostCombatMain);

		Assert.That(s.GetCard(walker).Loyalty, Nu.EqualTo(2));
		Assert.That(HandCount(me), Nu.EqualTo(hand + 1));
	}

	[Test]
	public void PsychicFrog_NoncombatDamage_DrawsNothing()
	{
		var frog = Field(Lsv("Psychic Frog"), me);
		var hand = HandCount(me);

		s = Damage.Deal(s, s.GetCard(frog), them, 1);
		Cast(Hand(Pump, me), [], frog); // priority is given: the damage's triggers are collected

		Assert.That(HandCount(me), Nu.EqualTo(hand));
	}

	[Test]
	public void PsychicFrog_CombatDamageToAPlayer_DrawsACard()
	{
		var frog = Field(Lsv("Psychic Frog"), me);
		var hand = HandCount(me);

		AttackTo(them, frog);
		ToMyPriorityIn(Step.PostCombatMain);

		Assert.That(HandCount(me), Nu.EqualTo(hand + 1));
	}

	[Test]
	public void Leovold_YouBecomingTheTargetOfAnOpponentsSpell_MayDraw()
	{
		Field(Lsv("Leovold, Emissary of Trest"), me);
		var hand = HandCount(me);

		TheyCast(TestCards.Shock(1, TargetSpec.AnyTarget()), me);
		Resolve(c =>
			c switch
			{
				YesNoChoice => [1],
				_ => Min(c),
			}
		);

		Assert.That(HandCount(me), Nu.EqualTo(hand + 1));
	}

	[Test]
	public void Nadu_ACreatureBecomingATarget_RevealsTheTop_ALandEnters_OnlyTwiceEachTurn()
	{
		Field(Lsv("Nadu, Winged Wisdom"), me);
		var bear = Field(TestGames.Creature("Bear", "2/2"), me);
		var third = OnTop(TestGames.Creature("Ogre", "3/3"));
		var spell = OnTop(TestCards.Fodder);
		var land = OnTop(TestGames.Card("Forest"));

		Cast(Hand(Pump, me), [], bear);
		Assert.That(Alive(land), Nu.True, "a land card: onto the battlefield");
		Cast(Hand(Pump, me), [], bear);
		Assert.That(s.IsIn(spell, me, ZoneType.Hand), Nu.True, "otherwise into your hand");
		Cast(Hand(Pump, me), [], bear);
		Assert.That(Library(me)[0], Nu.EqualTo(third), "only twice each turn");
	}

	[Test]
	public void Nadu_ItsTwiceEachTurn_NeverUsesUpTheCreaturesOwnOnceEachTurn()
	{
		Field(Lsv("Nadu, Winged Wisdom"), me);
		var innocence = Field(Lsv("Enduring Innocence"), me);
		OnTop(TestCards.Fodder);
		OnTop(TestCards.Fodder);
		var first = Hand(Pump, me);
		var second = Hand(Pump, me);
		Cast(first, [], innocence);
		Cast(second, [], innocence);
		var bear = Hand(TestGames.Creature("Bear", "2/2"), me);
		var hand = HandCount(me);

		Cast(bear, []);

		Assert.That(
			HandCount(me),
			Nu.EqualTo(hand),
			"the Bear cast, a card drawn: its own ability still triggers"
		);
	}

	[Test]
	public void DackFayden_Emblem_ASpellTargetingAPermanent_GainsControlOfIt()
	{
		var dack = Walker("Dack Fayden", 6);
		Loyalty(dack, -6);
		var ogre = Field(TestGames.Creature("Ogre", "3/3"), them);

		Cast(Hand(Pump, me), [], ogre);

		Assert.That(s.GetCard(ogre).ControllerId, Nu.EqualTo(me));
	}

	[Test]
	public void AlphaDeathclaw_Monstrosity_FourCountersOnce_AndDestroysATarget()
	{
		var claw = Field(Lsv("Alpha Deathclaw"), me);
		var ogre = Field(TestGames.Creature("Ogre", "3/3"), them);

		s = s.Act(Activations(claw).First());
		Resolve(c =>
			c switch
			{
				ChooseTargetChoice => [ogre],
				_ => Min(c),
			}
		);

		Assert.That(s.GetCard(claw).PlusOneCounters, Nu.EqualTo(4));
		Assert.That(Alive(ogre), Nu.False, "when it becomes monstrous, destroy target permanent");

		var giant = Field(TestGames.Creature("Giant", "4/4"), them);
		s = s.Act(Activations(claw).First());
		Resolve(c =>
			c switch
			{
				ChooseTargetChoice => [giant],
				_ => Min(c),
			}
		);

		Assert.That(s.GetCard(claw).PlusOneCounters, Nu.EqualTo(4), "already monstrous");
		Assert.That(Alive(giant), Nu.True);
	}

	[Test]
	public void Aang_Airbend_ExilesIt_ItsOwnerMayCastItForTwo()
	{
		var giant = Field(TestGames.CreatureCosting("{4}{G}", "Giant", "4/4"), them);
		var aang = Hand(Lsv("Aang, Swift Savior // Aang and La, Ocean's Fury"), me);

		CastAsking(
			aang,
			c =>
				c switch
				{
					ChooseTargetChoice => [giant],
					_ => Min(c),
				}
		);

		Assert.That(InExile(giant), Nu.True);
		Assert.That(
			Costs.Of(s, them, s.GetCard(giant)).ToString(),
			Nu.EqualTo("{2}"),
			"for {2} rather than its mana cost"
		);
		Assert.That(Playable.IsImpulsePlayableBy(s, s.GetCard(giant), them), Nu.True);
		Assert.That(Playable.IsImpulsePlayableBy(s, s.GetCard(giant), me), Nu.False, "its owner");
	}

	[Test]
	public void Aang_Airbend_AnXSpellIsCastForTwoWithXZero()
	{
		var hydra = Field(TestGames.CreatureCosting("{X}{G}", "Hydra", "0/0"), me);
		s = s.PutCounters(hydra, CounterKinds.PlusOne, 2);
		var aang = Hand(Lsv("Aang, Swift Savior // Aang and La, Ocean's Fury"), me);

		CastAsking(
			aang,
			c =>
				c switch
				{
					ChooseTargetChoice => [hydra],
					_ => Min(c),
				}
		);

		Assert.That(InExile(hydra), Nu.True);
		Assert.That(
			Casts(hydra).Select(c => c.X).Distinct(),
			Nu.EqualTo(new[] { 0 }),
			"no X in {2}"
		);
	}

	[Test]
	public void Aang_Waterbend_ArtifactsAndCreaturesPayForIt_TransformsHim()
	{
		var aang = Field(Lsv("Aang, Swift Savior // Aang and La, Ocean's Fury"), me);
		TapMyLands();
		for (var i = 0; i < 3; i++)
			Field(TestCards.Trinket, me);
		for (var i = 0; i < 3; i++)
			Field(TestGames.Creature("Bear", "2/2"), me);
		Assert.That(Activations(aang), Nu.Empty, "seven to tap, Aang among them: not {8}");
		Field(TestGames.Creature("Bear", "2/2"), me);

		s = s.Act(Activations(aang).Single());
		Resolve();

		Assert.That(s.GetCard(aang).Name, Nu.EqualTo("Aang and La, Ocean's Fury"));
		Assert.That(
			s.CardsIn(me, ZoneType.Battlefield).Count(c => !c.Is(CardType.Land) && c.IsTapped),
			Nu.EqualTo(8)
		);
	}

	[Test]
	public void Comet_Zero_EachRowOfTheDie()
	{
		var seen = new HashSet<int>();
		for (var seed = 1; seed <= 60 && seen.Count < 4; seed++)
		{
			s = TestGames.InFirstMain(seed);
			me = s.GetGame().StartingPlayerId;
			them = s.OpponentOf(me);
			var comet = Walker("Comet, Stellar Pup", 5);
			var cheap = Put(TestGames.Creature("Bear", "2/2"), me, ZoneType.Graveyard);
			var life = Life(them);

			Loyalty(
				comet,
				0,
				c =>
					c switch
					{
						ChoosePermanentChoice => [them],
						_ => Min(c),
					}
			);

			var again = s.LegalActions()
				.OfType<ActivateLoyaltyAction>()
				.Any(a => a.CardId == comet);
			var squirrels = s.CardsIn(me, ZoneType.Battlefield)
				.Where(c => c.Name == "Squirrel")
				.ToList();
			var loyalty = s.GetCard(comet).Loyalty;
			seen.Add(loyalty);
			switch (loyalty)
			{
				case 7: // 1 or 2 — [+2], two Squirrels with haste
					Assert.That(squirrels.Count(c => s.Has(c, "Haste")), Nu.EqualTo(2));
					break;
				case 4: // 3 — [−1], a card with mana value 2 or less back to hand
					Assert.That(s.IsIn(cheap, me, ZoneType.Hand), Nu.True);
					break;
				case 3: // 4 or 5 — damage equal to his loyalty to a creature or PLAYER, then [−2]
					Assert.That(Life(them), Nu.EqualTo(life - 5));
					break;
				case 6: // 6 — [+1], and two more activations this turn
					Assert.That(again, Nu.True, "may activate again");
					s = s.UpdateObject(
						comet,
						s.GetCard(comet) with
						{
							ExtraLoyaltyActivations = 1,
						}
					);
					Loyalty(
						comet,
						0,
						c =>
							c switch
							{
								ChoosePermanentChoice => [them],
								_ => Min(c),
							}
					);
					Assert.That(
						s.GetCard(comet).ExtraLoyaltyActivations is 0 or 2,
						Nu.True,
						"an activation after the first uses one (a 6 adds two)"
					);
					break;
				default:
					Assert.Fail($"no row leaves Comet at {loyalty}");
					break;
			}
			if (loyalty != 6)
				Assert.That(again, Nu.False, "once a turn, but for a 6");
		}
		Assert.That(seen, Nu.EquivalentTo(new[] { 7, 4, 3, 6 }), "every row rolled over the seeds");
	}

	// ===== m2c =====

	[TestCase(true)]
	[TestCase(false)]
	public void FracturedIdentity_ExilesIt_ThePlayerWhoDoesntControlItGetsTheCopy(bool mine)
	{
		var ogre = Field(TestGames.Creature("Ogre", "3/3"), mine ? me : them);
		var identity = Hand(Lsv("Fractured Identity"), me);

		Cast(identity, [], ogre);

		Assert.That(InExile(ogre), Nu.True);
		Assert.That(
			s.CardsIn(mine ? them : me, ZoneType.Battlefield)
				.Count(c => c.IsToken && c.Name == "Ogre"),
			Nu.EqualTo(1),
			"each player other than its controller"
		);
		Assert.That(
			s.CardsIn(mine ? me : them, ZoneType.Battlefield).Any(c => c.Name == "Ogre"),
			Nu.False
		);
	}

	[Test]
	public void SuperiorSpiderMan_EntersAsACopyOfACreatureCardInAGraveyard_KeepsHisNameAndIs44()
	{
		var giant = Put(TestGames.Creature("Giant", "6/6", "Flying"), them, ZoneType.Graveyard);
		var spider = Hand(Lsv("Superior Spider-Man"), me);

		CastAsking(
			spider,
			c =>
				c switch
				{
					ChooseCreatureToCopyChoice => [giant],
					_ => Min(c),
				}
		);

		var card = s.GetCard(spider);
		Assert.That(
			(card.Name, s.Power(card), s.Toughness(card), s.Has(card, "Flying")),
			Nu.EqualTo(("Superior Spider-Man", 4, 4, true))
		);
		Assert.That(
			s.HasSubtype(card, "Spider") && s.HasSubtype(card, "Test"),
			Nu.True,
			"in addition"
		);
		Assert.That(InExile(giant), Nu.True, "when you do, exile that card");
	}

	[Test]
	public void Saheeli_MinusTwo_AnArtifactBecomesACopy_AndIsStillAnArtifact()
	{
		var saheeli = Walker("Saheeli, Sublime Artificer", 5);
		var trinket = Field(TestCards.Trinket, me);
		var ogre = Field(TestGames.Creature("Ogre", "3/3"), me);

		s = s.Act(
			s.LegalActions()
				.OfType<ActivateLoyaltyAction>()
				.Single(a => a.CardId == saheeli && a.Targets.SequenceEqual([trinket, ogre]))
		);
		Resolve();

		var copy = s.GetCard(trinket);
		Assert.That(
			(copy.Name, copy.Is(CardType.Creature), copy.Is(CardType.Artifact)),
			Nu.EqualTo(("Ogre", true, true))
		);
	}

	[Test]
	public void Saheeli_MinusTwo_CopiesTheModelsCopiableTypes_NeverATypeStampOnIt()
	{
		var saheeli = Walker("Saheeli, Sublime Artificer", 5);
		var trinket = Field(TestCards.Trinket, me);
		var animated = Field(TestCards.Trinket, me);
		var model = s.GetCard(animated);
		s = s.UpdateObject(animated, model.WithTypesUntilItLeaves(model.Types | CardType.Creature));
		s = s.PutCounters(animated, CounterKinds.PlusOne, 2);

		s = s.Act(
			s.LegalActions()
				.OfType<ActivateLoyaltyAction>()
				.Single(a => a.CardId == saheeli && a.Targets.SequenceEqual([trinket, animated]))
		);
		Resolve();

		Assert.That(Alive(trinket), Nu.True, "no 0/0 creature: the stamp isn't copiable");
		Assert.That(s.GetCard(trinket).Is(CardType.Creature), Nu.False);
	}

	[Test]
	public void Oko_PlusOne_LosesAllAbilitiesAndIsAGreenElk33_ForGood()
	{
		var oko = Walker("Oko, Thief of Crowns", 4);
		var angel = Field(TestGames.Creature("Angel", "5/5", "Flying"), them);
		var trinket = Field(TestCards.Trinket, them);

		Loyalty(
			oko,
			1,
			c =>
				c switch
				{
					ChooseTargetChoice => [angel],
					_ => Min(c),
				}
		);
		s = s.Act(
			s.LegalActions().OfType<ActivateLoyaltyAction>().FirstOrDefault(a => a.CardId == oko)
				?? (GameAction)new PassPriorityAction { PlayerId = me }
		);
		s = TestGames.PassUntil(
			s,
			x => x.GetGame().ActivePlayerId == them && x.GetGame().PriorityPlayerId != 0
		);

		var elk = s.GetCard(angel);
		Assert.That(
			(s.Power(elk), s.Toughness(elk), s.Has(elk, "Flying"), s.HasSubtype(elk, "Elk")),
			Nu.EqualTo((3, 3, false, true)),
			"still, a turn later"
		);
		Assert.That(s.HasSubtype(elk, "Test"), Nu.False, "it loses its other creature types");
		Assert.That(s.ColorsOf(elk), Nu.EqualTo(new[] { ManaColor.Green }));
		Assert.That(Alive(trinket), Nu.True);
	}

	[Test]
	public void Oko_PlusOne_AnArtifactBecomesAnArtifactCreature()
	{
		var oko = Walker("Oko, Thief of Crowns", 4);
		var trinket = Field(TestCards.Trinket, them);

		s = s.Act(
			s.LegalActions()
				.OfType<ActivateLoyaltyAction>()
				.Single(a => a.CardId == oko && a.Targets.SequenceEqual([trinket]))
		);
		Resolve();

		var elk = s.GetCard(trinket);
		Assert.That(
			(elk.Is(CardType.Artifact), elk.Is(CardType.Creature), s.Power(elk)),
			Nu.EqualTo((true, true, 3))
		);
	}

	[Test]
	public void Oko_MinusFive_OneActivation_ItsTargetsChosenAfter_ExchangesControl()
	{
		var oko = Walker("Oko, Thief of Crowns", 5);
		Field(TestGames.Creature("Bear", "2/2"), me);
		var mine = Field(TestCards.Trinket, me);
		Field(TestGames.Creature("Wolf", "2/2"), them);
		var theirs = Field(TestGames.Creature("Ogre", "3/3"), them);
		Field(TestGames.Creature("Giant", "4/4"), them);

		var minusFive = s.LegalActions()
			.OfType<ActivateLoyaltyAction>()
			.Where(a => a.CardId == oko && a.AbilityIndex == 2)
			.ToList();
		Assert.That(minusFive, Has.Count.EqualTo(1), "never one per pair of targets");

		s = s.Act(minusFive[0]);
		var asked = ResolveAsking(c =>
			c switch
			{
				ChooseTargetChoice t => [t.Index == 0 ? mine : theirs],
				_ => Min(c),
			}
		);

		Assert.That(
			asked.OfType<ChooseTargetChoice>().Last().Options,
			Has.Count.EqualTo(2),
			"power 3 or less"
		);
		Assert.That(
			(s.GetCard(mine).ControllerId, s.GetCard(theirs).ControllerId, s.GetCard(oko).Loyalty),
			Nu.EqualTo((them, me, 0))
		);
	}

	[Test]
	public void FigureOfDestiny_EachLevelOnlyFromTheOneBefore_ForGood()
	{
		var figure = Field(Lsv("Figure of Destiny"), me);
		void Activate(int index)
		{
			foreach (
				var land in s.CardsIn(me, ZoneType.Battlefield)
					.Where(c => c.Is(CardType.Land))
					.ToList()
			)
				s = s.Untap(land.Id); // eight lands make {R/W}; the levels cost fourteen
			s = s.Act(Activations(figure).Single(a => a.AbilityIndex == index));
			Resolve();
		}
		(int, int) Size() => (s.Power(s.GetCard(figure)), s.Toughness(s.GetCard(figure)));

		Activate(1);
		Assert.That(Size(), Nu.EqualTo((1, 1)), "not a Spirit yet");
		Activate(0);
		Assert.That(Size(), Nu.EqualTo((2, 2)));
		Assert.That(s.HasSubtype(s.GetCard(figure), "Spirit"), Nu.True);
		Activate(2);
		Assert.That(Size(), Nu.EqualTo((2, 2)), "not a Warrior yet");
		Activate(1);
		Assert.That(Size(), Nu.EqualTo((4, 4)));
		Activate(2);
		var avatar = s.GetCard(figure);
		Assert.That(
			(Size(), s.Has(avatar, "Flying"), s.Has(avatar, "First strike")),
			Nu.EqualTo(((8, 8), true, true))
		);

		s = TestGames.PassUntil(
			s,
			x => x.GetGame().ActivePlayerId == them && x.GetGame().PriorityPlayerId != 0
		);
		avatar = s.GetCard(figure);
		Assert.That((Size(), s.Has(avatar, "Flying")), Nu.EqualTo(((8, 8), true)), "no duration");
	}

	[Test]
	public void FigureOfFable_TheAvatar_HasProtectionFromEachOfYourOpponents()
	{
		var figure = Field(Lsv("Figure of Fable"), me);
		foreach (var index in new[] { 0, 1, 2 })
		{
			s = s.Act(Activations(figure).Single(a => a.AbilityIndex == index));
			Resolve();
		}

		var avatar = s.GetCard(figure);
		Assert.That((s.Power(avatar), s.Toughness(avatar)), Nu.EqualTo((7, 8)));
		var pump = Hand(Pump, me);
		Assert.That(
			s.LegalActions()
				.OfType<CastSpellAction>()
				.Any(c => c.CardId == pump && c.Targets.Contains(figure)),
			Nu.True,
			"your own spell targets it"
		);
		var kill = Hand(TestCards.Kill(), them);
		s = s.Act(new PassPriorityAction { PlayerId = me });
		Assert.That(
			new CastSpellAction
			{
				PlayerId = them,
				CardId = kill,
				Targets = [figure],
			}
				.ValidateAdd(s)
				.IsValid,
			Nu.False,
			"protection from each of your opponents"
		);
	}

	[Test]
	public void ChaosDefiler_Entering_ANonlandPermanentOfTheirsIsChosenAndDestroyed()
	{
		Field(TestGames.Creature("Bear", "2/2"), me);
		var ogre = Field(TestGames.Creature("Ogre", "3/3"), them);
		var defiler = Hand(Lsv("Chaos Defiler"), me);

		var asked = CastAsking(
			defiler,
			c =>
				c switch
				{
					ChoosePermanentChoice => [ogre],
					_ => Min(c),
				}
		);

		Assert.That(
			Offered<ChoosePermanentChoice>(asked),
			Nu.EquivalentTo(new[] { ogre }),
			"that player's nonland permanents only"
		);
		Assert.That(Alive(ogre), Nu.False);
	}

	[Test]
	public void CloakAndDagger_TheChosenCreatureIsOfferedBesideTheirHand_AndComesBack()
	{
		var ogre = Field(TestGames.Creature("Ogre", "3/3"), them);
		var held = Hand(TestCards.Fodder, them);
		var forest = Hand(TestGames.Card("Forest"), them);
		var cloak = Hand(Lsv("Cloak and Dagger, Entwined"), me);

		var asked = CastAsking(
			cloak,
			c =>
				c switch
				{
					ChooseTargetChoice t => [t.Index == 0 ? them : ogre],
					ChooseCardsFromHandChoice => [ogre],
					_ => Min(c),
				}
		);

		var offered = Offered<ChooseCardsFromHandChoice>(asked).ToList();
		Assert.That(
			new[] { held, ogre },
			Nu.SubsetOf(offered),
			"their hand or the chosen creature"
		);
		Assert.That(offered, Does.Not.Contain(forest), "a nonland card");
		Assert.That(InExile(ogre), Nu.True);

		s = s.Destroy([cloak]);
		Resolve();
		Assert.That(Alive(ogre), Nu.True, "until Cloak and Dagger leave the battlefield");
	}

	[Test]
	public void Atraxa_Entering_ACardOfEachTypeAmongTheTopTen()
	{
		var secondCreature = OnTop(TestGames.Creature("Ogre", "3/3"));
		var kindred = OnTop(TestCards.Typed(TestCards.Fodder, "Kindred Enchantment — Faerie"));
		var enchantment = OnTop(TestCards.Typed(TestCards.Fodder, "Enchantment"));
		var instant = OnTop(TestCards.Kill());
		var land = OnTop(TestGames.Card("Forest"));
		var creature = OnTop(TestGames.Creature("Bear", "2/2"));
		var golem = OnTop(
			TestCards.Typed(TestGames.Creature("Golem", "3/3"), "Artifact Creature — Golem")
		);
		var atraxa = Hand(Lsv("Atraxa, Grand Unifier"), me);

		var asked = CastAsking(atraxa, First);

		var hand = s.CardsIn(me, ZoneType.Hand).Select(c => c.Id).ToList();
		Assert.That(new[] { land, instant }, Nu.SubsetOf(hand));
		Assert.That(
			new[] { golem, creature },
			Nu.SubsetOf(hand),
			"the artifact creature taken as an artifact isn't offered again as the creature"
		);
		Assert.That(
			new[] { enchantment, kindred },
			Nu.SubsetOf(hand),
			"kindred is a card type too"
		);
		Assert.That(hand, Does.Not.Contain(secondCreature), "one card of each type");
		Assert.That(Library(me)[..4], Does.Not.Contain(secondCreature), "the rest on the bottom");
		Assert.That(asked.OfType<ChooseAmongChoice>().All(c => c.MaxChoices == 1), Nu.True);
	}

	[Test]
	public void Vivi_ManaByItsPower_InAnyCombination_OnceEachTurn()
	{
		var vivi = Field(Lsv("Vivi Ornitier"), me);
		s = s.PutCounters(vivi, CounterKinds.PlusOne, 2);

		var moves = ManaMoves(vivi).ToList();
		Assert.That(
			moves.Select(m => m.OfTheOther),
			Nu.EquivalentTo(new[] { 0, 1, 2 }),
			"a move per split"
		);
		s = s.Act(moves.Single(m => m.OfTheOther == 1));

		var pool = s.GetPlayer(me).ManaPool;
		Assert.That((pool.Blue, pool.Red), Nu.EqualTo((1, 1)), "X is Vivi's power");
		Assert.That(s.GetCard(vivi).IsTapped, Nu.False, "{0}: no tap");
		Assert.That(ManaMoves(vivi), Nu.Empty, "only once each turn");
	}

	[Test]
	public void SquanderedResources_SacrificingALand_MakesWhatThatLandCould()
	{
		var resources = Field(Lsv("Squandered Resources"), me);
		var forest = s.CardsIn(me, ZoneType.Battlefield).First(c => c.Name == "Forest").Id;

		var moves = ManaMoves(resources).Where(m => m.SacrificeId == forest).ToList();
		Assert.That(moves.Select(m => m.Color), Nu.EqualTo(new[] { ManaColor.Green }));
		Assert.That(Describe.Action(s, moves[0]), Does.Not.StartWith("Tap"), "no {T} in its cost");
		s = s.Act(moves[0]);

		Assert.That(InGraveyard(forest), Nu.True);
		Assert.That(s.GetPlayer(me).ManaPool.Green, Nu.EqualTo(1));
	}

	[Test]
	public void Loot_ExhaustOnAManaAbility_ThreeOfOneColour_Once_EachExhaustItsOwn()
	{
		var loot = Field(Lsv("Loot, the Pathfinder"), me);
		var lands = UntappedLands();

		var moves = ManaMoves(loot).ToList();
		Assert.That(moves, Has.Count.EqualTo(5), "a move per colour, never the payment's");
		s = s.Act(moves.Single(m => m.Color == ManaColor.White));

		Assert.That(s.GetPlayer(me).ManaPool.White, Nu.EqualTo(3));
		Assert.That(
			(s.GetCard(loot).IsTapped, UntappedLands()),
			Nu.EqualTo((true, lands - 1)),
			"{G}, {T}"
		);

		s = s.Untap(loot);
		Assert.That(ManaMoves(loot), Nu.Empty, "activate each exhaust ability only once");
		Assert.That(
			Activations(loot).Select(a => a.AbilityIndex).Distinct(),
			Nu.EquivalentTo(new[] { 0, 1 }),
			"the other two are their own"
		);

		var hand = HandCount(me);
		s = s.Act(Activations(loot).First(a => a.AbilityIndex == 0));
		Resolve();
		s = s.Untap(loot);
		Assert.That(HandCount(me), Nu.EqualTo(hand + 3));
		Assert.That(
			Activations(loot).Select(a => a.AbilityIndex).Distinct(),
			Nu.EqualTo(new[] { 1 })
		);
	}
}
