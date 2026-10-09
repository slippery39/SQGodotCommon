using ImmutableGameObjects;
using MagicCore.Actions;
using MagicCore.Cards;
using MagicCore.Effects;
using MagicCore.Sets.Fra;
using MagicCore.Targeting;
using MagicCore.Turns;

namespace MagicCore.Tests.Sets;

/// <summary>Mythics and rares (8f), planeswalkers — each ability fires.</summary>
public class FraRarePlaneswalkerTests : FraBoardTest
{
	static CardDefinition Kill =>
		new(
			TestGames.Inline("Kill", "{1}", "Instant").Data,
			[
				new SpellEffectsComponent([new Destroy(Ref.Target())])
				{
					Targets = [new TargetSpec(TargetKind.Creature)],
				},
			]
		);

	static CardDefinition Fodder => TestGames.Inline("Fodder", "{1}", "Sorcery");

	/// <summary>A planeswalker on my side with its printed loyalty, or this many (TEST SETUP).</summary>
	int Walker(string name, int? loyalty = null)
	{
		var id = Field(FraCards.Get(name), me);
		s = s.UpdateObject(
			id,
			s.GetCard(id) with
			{
				Loyalty = loyalty ?? s.GetCard(id).PrintedLoyalty,
			}
		);
		return id;
	}

	void Activate(
		int walker,
		int index,
		int[]? targets = null,
		int x = 0,
		Func<ChoiceAction, IEnumerable<int>>? answer = null
	)
	{
		s = s.Act(
			new ActivateLoyaltyAction
			{
				PlayerId = me,
				CardId = walker,
				AbilityIndex = index,
				Targets = [.. targets ?? []],
				X = x,
			}
		);
		Resolve(answer);
	}

	void TapAllLands(int player)
	{
		foreach (
			var land in s.CardsIn(player, ZoneType.Battlefield)
				.Where(c => c.Is(CardType.Land))
				.ToList()
		)
			s = s.UpdateObject(land.Id, land with { IsTapped = true }); // TEST SETUP
	}

	void EmptyHand(int player)
	{
		foreach (var c in s.CardsIn(player, ZoneType.Hand).ToList())
			s = s.MoveObject(c.Id, s.ZoneId(player, ZoneType.Exile)); // TEST SETUP
	}

	int OnTop(CardDefinition card)
	{
		int id;
		(s, id) = TestGames.Put(s, me, card, ZoneType.Library);
		s = s.MoveObjectToFront(id, s.ZoneId(me, ZoneType.Library)); // TEST SETUP
		return id;
	}

	void ToMyNextTurn()
	{
		var turn = s.GetGame().TurnNumber;
		s = TestGames.PassUntil(
			s,
			x =>
				x.GetGame().ActivePlayerId == me
				&& x.GetGame().TurnNumber > turn
				&& x.GetGame().Step == Step.PreCombatMain
		);
	}

	void ToTheirTurn() => s = TestGames.PassUntil(s, x => x.GetGame().ActivePlayerId == them);

	// ===== Ajani Resolute =====

	[Test]
	public void AjaniResolute_LifeGainLoadsHim_AndHisZeroGainsLife()
	{
		var ajani = Walker("Ajani Resolute");
		var life = Life(me);
		Activate(ajani, 0);
		Assert.That(
			(Life(me), s.GetCard(ajani).Loyalty),
			Is.EqualTo((life + 1, 2 + 1)),
			"0, then +1 for the life"
		);
	}

	[Test]
	public void AjaniResolute_MinusFour_APridemateThatGrowsOnLifeGain()
	{
		var ajani = Walker("Ajani Resolute", 4);
		Activate(ajani, 1);
		var pridemate = s.CardsIn(me, ZoneType.Battlefield)
			.Single(c => c.Name == "Ajani's Pridemate");
		Cast(
			Hand(TestGames.Inline("Heal", "{1}", "Instant", null, new GainLife(Ref.You, 1)), me),
			[]
		);
		Assert.That(s.Power(s.GetCard(pridemate.Id)), Is.EqualTo(2 + 1));
	}

	[Test]
	public void AjaniResolute_MinusTen_AnEmblemThatOutlivesHim()
	{
		var ajani = Walker("Ajani Resolute", 10);
		var bear = Field(TestGames.Creature("Bear", "2/2"), me);
		Activate(ajani, 2);
		Assert.That(Alive(ajani), Is.False, "0 loyalty");
		Assert.That(
			(s.Power(s.GetCard(bear)), s.Toughness(s.GetCard(bear))),
			Is.EqualTo((2 + 2, 2 + 2))
		);
		var theirs = Field(TestGames.Creature("Theirs", "2/2"), them);
		Assert.That(s.Power(s.GetCard(theirs)), Is.EqualTo(2), "mine only");
	}

	// ===== Chandra, Chill of Compliance =====

	[Test]
	public void ChandraChill_PlusOne_SurveilsANoncreatureNonlandIntoHand()
	{
		var chandra = Walker("Chandra, Chill of Compliance");
		var spell = OnTop(TestGames.Inline("Spell", "{1}", "Instant"));
		Activate(
			chandra,
			0,
			answer: c =>
				c switch
				{
					ChooseFromLibraryTopChoice => [spell],
					_ => [],
				}
		);
		Assert.That(s.IsIn(spell, me, ZoneType.Hand));
		var bear = OnTop(TestGames.Creature("Bear", "2/2"));
		s = s.UpdateObject(chandra, s.GetCard(chandra) with { LoyaltyActivatedOnTurn = 0 }); // TEST SETUP: another activation
		Activate(
			chandra,
			0,
			answer: c =>
				c switch
				{
					ChooseFromLibraryTopChoice => [bear],
					_ => [],
				}
		);
		Assert.That(s.IsIn(bear, me, ZoneType.Graveyard), "a creature stays");
	}

	[Test]
	public void ChandraChill_PlusOne_BlueOnlyForNoncreatureSpells()
	{
		var chandra = Walker("Chandra, Chill of Compliance");
		Activate(chandra, 1);
		TapAllLands(me);
		var creature = Hand(TestGames.CreatureCosting("{U}", "Blue Bear", "1/1"), me);
		var instant = Hand(TestGames.Inline("Blue Spell", "{U}", "Instant"), me);
		Assert.That((Casts(creature).Any(), Casts(instant).Any()), Is.EqualTo((false, true)));
		s = s.Act(Casts(instant).Single());
		Assert.That(s.GetPlayer(me).ManaPool.Restricted, Is.Empty, "spent");
	}

	[Test]
	public void ChandraChill_MinusX_TapsAndStunsX()
	{
		var chandra = Walker("Chandra, Chill of Compliance", 3);
		var theirs = Field(TestGames.Creature("Theirs", "2/2"), them);
		var offered = s.LegalActions()
			.OfType<ActivateLoyaltyAction>()
			.Where(a => a.CardId == chandra && a.AbilityIndex == 2)
			.Select(a => a.X)
			.Distinct();
		Assert.That(offered, Is.EquivalentTo(new[] { 0, 1, 2, 3 }), "X up to her loyalty");
		Activate(chandra, 2, [theirs], x: 2);
		var card = s.GetCard(theirs);
		Assert.That(
			(card.IsTapped, card.StunCounters, s.GetCard(chandra).Loyalty),
			Is.EqualTo((true, 2, 1))
		);
	}

	[Test]
	public void ChandraChill_MinusSix_EmblemDrawsOnEachSpell()
	{
		var chandra = Walker("Chandra, Chill of Compliance", 6);
		Activate(chandra, 3);
		var spell = Hand(TestGames.Inline("Spell", "{1}", "Instant"), me);
		var hand = HandCount(me);
		Cast(spell, []);
		Assert.That(HandCount(me), Is.EqualTo(hand - 1 + 1));
	}

	[Test]
	public void ChandraChill_MinusX_IsDescribedWithItsX()
	{
		var chandra = Walker("Chandra, Chill of Compliance", 3);
		var theirs = Field(TestGames.Creature("Theirs", "2/2"), them);
		var action = new ActivateLoyaltyAction
		{
			PlayerId = me,
			CardId = chandra,
			AbilityIndex = 2,
			Targets = [theirs],
			X = 2,
		};
		Assert.That(Describe.Action(s, action), Does.Contain("[−2]"));
	}

	[Test]
	public void RestrictedFloatingMana_CountsTowardX()
	{
		TapAllLands(me);
		var pool = s.GetPlayer(me).ManaPool;
		s = s.WithPlayer(
			s.GetPlayer(me) with
			{
				ManaPool = pool with
				{
					Restricted =
					[
						new RestrictedMana(ManaColor.Blue, ManaRestriction.OnlyNoncreatureSpells),
						new RestrictedMana(ManaColor.Blue, ManaRestriction.OnlyNoncreatureSpells),
					],
				},
			}
		); // TEST SETUP: two of Chandra's {U}
		var spell = Hand(TestGames.Inline("Xspell", "{X}{U}", "Instant"), me);
		Assert.That(Casts(spell).Select(c => c.X), Does.Contain(1));
	}

	[Test]
	public void TheAi_ValuesAnUntilYourNextTurnDebuff()
	{
		var theirs = Field(TestGames.Creature("Theirs", "5/5"), them);
		var ai = new MagicCore.Ai.GreedyAi(me);
		var before = ai.Evaluate(s);
		s = s.UpdateObject(
			theirs,
			(Card)s.GetCard(theirs).WithComponent(new UntilEndOfTurn(-4, -1) { UntilTurnOf = me })
		); // TEST SETUP: Garruk's +2
		Assert.That(ai.Evaluate(s), Is.GreaterThan(before));
	}

	// ===== Chandra, Torch of Defiance =====

	[Test]
	public void ChandraTorch_PlusOne_CastItOrTwoDamage()
	{
		var chandra = Walker("Chandra, Torch of Defiance");
		var spell = OnTop(TestGames.Inline("Spell", "{1}", "Sorcery"));
		Activate(
			chandra,
			0,
			answer: c =>
				c switch
				{
					YesNoChoice => [1],
					_ => [],
				}
		);
		Assert.That(
			(s.IsIn(spell, me, ZoneType.Exile), Casts(spell).Any()),
			Is.EqualTo((true, true)),
			"castable this turn"
		);

		s = s.UpdateObject(chandra, s.GetCard(chandra) with { LoyaltyActivatedOnTurn = 0 }); // TEST SETUP
		OnTop(TestGames.Inline("Other", "{1}", "Sorcery"));
		var life = Life(them);
		Activate(chandra, 0, answer: _ => []);
		Assert.That(Life(them), Is.EqualTo(life - 2), "declined: 2 damage");

		s = s.UpdateObject(chandra, s.GetCard(chandra) with { LoyaltyActivatedOnTurn = 0 }); // TEST SETUP
		OnTop(TestGames.Card("Forest"));
		var asked = false;
		Activate(
			chandra,
			0,
			answer: c =>
			{
				asked |= c is YesNoChoice;
				return [];
			}
		);
		Assert.That((asked, Life(them)), Is.EqualTo((false, life - 4)), "a land can't be cast");

		s = s.UpdateObject(chandra, s.GetCard(chandra) with { LoyaltyActivatedOnTurn = 0 }); // TEST SETUP
		foreach (
			var c in s.CardsIn(me, ZoneType.Battlefield)
				.Concat(s.CardsIn(them, ZoneType.Battlefield))
				.Where(c => c.Is(CardType.Creature))
				.ToList()
		)
			s = s.MoveObject(c.Id, s.ZoneId(c.OwnerId, ZoneType.Exile)); // TEST SETUP: no creatures
		OnTop(
			new CardDefinition(
				TestGames.Inline("Kill", "{1}", "Instant").Data,
				[
					new SpellEffectsComponent([new Destroy(Ref.Target())])
					{
						Targets = [new TargetSpec(TargetKind.Creature)],
					},
				]
			)
		);
		Activate(
			chandra,
			0,
			answer: c =>
			{
				asked |= c is YesNoChoice;
				return [];
			}
		);
		Assert.That(
			(asked, Life(them)),
			Is.EqualTo((false, life - 6)),
			"no target for it: it can't be cast"
		);
	}

	[Test]
	public void ChandraTorch_PlusOneRR_MinusThreeFour_MinusSevenEmblem()
	{
		var chandra = Walker("Chandra, Torch of Defiance", 7);
		Activate(chandra, 1);
		Assert.That(s.GetPlayer(me).ManaPool[ManaColor.Red], Is.EqualTo(2));

		var other = Walker("Chandra, Torch of Defiance", 3);
		var theirs = Field(TestGames.Creature("Theirs", "2/5"), them);
		Activate(other, 2, [theirs]);
		Assert.That(s.GetCard(theirs).Damage, Is.EqualTo(4));

		s = s.UpdateObject(chandra, s.GetCard(chandra) with { LoyaltyActivatedOnTurn = 0 }); // TEST SETUP
		Activate(chandra, 3);
		var life = Life(them);
		var spell = Hand(TestGames.Inline("Spell", "{1}", "Instant"), me);
		s = s.Act(new CastSpellAction { PlayerId = me, CardId = spell });
		Resolve(c =>
			c switch
			{
				ChooseTargetChoice => [them],
				_ => [],
			}
		);
		Assert.That(Life(them), Is.EqualTo(life - 5), "the emblem's trigger, at their face");
	}

	// ===== Jace, Reality Sculptor =====

	[Test]
	public void JaceRealitySculptor_PlusOne_EmpowersJaceByMyIslands()
	{
		var jace = Walker("Jace, Reality Sculptor");
		var islands = s.CardsIn(me, ZoneType.Battlefield).Count(c => c.Subtypes.Contains("Island"));
		Activate(jace, 0);
		Assert.That(Jace(me)!.Loyalty, Is.EqualTo(islands));
	}

	[Test]
	public void JaceRealitySculptor_MinusThree_TheirAttackersGetMinusFiveUntilMyNextTurn()
	{
		var jace = Walker("Jace, Reality Sculptor");
		var attacker = Field(TestGames.Creature("Attacker", "6/6"), them);
		Activate(jace, 1);
		ToTheirTurn();
		s = TestGames.PassUntil(s, x => x.GetPendingChoice() is DeclareAttackerChoice);
		while (s.GetPendingChoice() is DeclareAttackerChoice declare)
			s = s.Choose([declare.CreatureId == attacker ? me : CombatOption.None]);
		Resolve();
		Assert.That(s.Power(s.GetCard(attacker)), Is.EqualTo(6 - 5));
		ToMyNextTurn();
		Assert.That(s.CardsIn(me, ZoneType.Command), Is.Empty, "the effect ended as my turn began");
	}

	[Test]
	public void JaceRealitySculptor_Zero_OnlyWithTwentyFiveLoyalty_ExilesAllButTheBottom()
	{
		var jace = Walker("Jace, Reality Sculptor", 24);
		bool Offered() =>
			s.LegalActions()
				.OfType<ActivateLoyaltyAction>()
				.Any(a => a.CardId == jace && a.AbilityIndex == 2);
		Assert.That(Offered(), Is.False);
		s = s.UpdateObject(jace, s.GetCard(jace) with { Loyalty = 25 }); // TEST SETUP
		Assert.That(Offered(), Is.True);
		var bottom = s.CardsIn(them, ZoneType.Library).Last().Id;
		Activate(jace, 2);
		Assert.That(
			s.CardsIn(them, ZoneType.Library).Select(c => c.Id),
			Is.EqualTo(new[] { bottom })
		);
	}

	// ===== Garruk, Veiled Butcher =====

	[Test]
	public void GarrukVeiled_TheirDyingCreaturesAreExiled_NotMine()
	{
		Walker("Garruk, Veiled Butcher");
		var theirs = Field(TestGames.Creature("Theirs", "2/2"), them);
		var mine = Field(TestGames.Creature("Mine", "2/2"), me);
		Cast(Hand(Kill, me), [], theirs);
		Cast(Hand(Kill, me), [], mine);
		Assert.That(
			(s.IsIn(theirs, them, ZoneType.Exile), s.IsIn(mine, me, ZoneType.Graveyard)),
			Is.EqualTo((true, true))
		);
	}

	[Test]
	public void GarrukVeiled_PlusTwo_MinusFourMinusOneUntilMyNextTurn()
	{
		var garruk = Walker("Garruk, Veiled Butcher");
		var theirs = Field(TestGames.Creature("Theirs", "5/5"), them);
		Activate(garruk, 0, [theirs]);
		ToTheirTurn();
		Assert.That(
			(s.Power(s.GetCard(theirs)), s.Toughness(s.GetCard(theirs))),
			Is.EqualTo((1, 4)),
			"still, on their turn"
		);
		ToMyNextTurn();
		Assert.That(s.Power(s.GetCard(theirs)), Is.EqualTo(5));
	}

	[Test]
	public void GarrukVeiled_MinusTwo_EachSacrifices_AndIGetABeast()
	{
		var garruk = Walker("Garruk, Veiled Butcher");
		var mine = Field(TestGames.Creature("Mine", "1/1"), me);
		var theirs = Field(TestGames.Creature("Theirs", "1/1"), them);
		Activate(garruk, 1, answer: c => c.Options.Take(c.MinChoices).Select(o => o.Id));
		var beast = s.CardsIn(me, ZoneType.Battlefield).SingleOrDefault(c => c.Name == "Beast");
		Assert.That(
			(Alive(mine), Alive(theirs), beast is not null && s.Has(beast, "Trample")),
			Is.EqualTo((false, false, true))
		);
	}

	[Test]
	public void GarrukVeiled_MinusThree_TheyDiscardTwo_IDrawIfNotTwoNonlands()
	{
		var garruk = Walker("Garruk, Veiled Butcher", 9);
		EmptyHand(them);
		Hand(Fodder, them);
		Hand(TestGames.Card("Forest"), them);
		var hand = HandCount(me);
		Activate(garruk, 2);
		Assert.That(
			(HandCount(them), HandCount(me)),
			Is.EqualTo((0, hand + 1)),
			"a land among them: I draw"
		);

		s = s.UpdateObject(garruk, s.GetCard(garruk) with { LoyaltyActivatedOnTurn = 0 }); // TEST SETUP
		Hand(Fodder, them);
		Hand(Fodder, them);
		hand = HandCount(me);
		Activate(garruk, 2);
		Assert.That(HandCount(me), Is.EqualTo(hand), "two nonlands: no draw");

		s = s.UpdateObject(garruk, s.GetCard(garruk) with { LoyaltyActivatedOnTurn = 0 }); // TEST SETUP
		hand = HandCount(me);
		Activate(garruk, 2);
		Assert.That(HandCount(me), Is.EqualTo(hand + 1), "an empty hand discarded nothing: I draw");
	}

	// ===== Garruk, Curse Breaker =====

	[Test]
	public void GarrukCurseBreaker_APowerFourCreatureEntering_DrawsACard()
	{
		Walker("Garruk, Curse Breaker");
		var big = Hand(TestGames.Creature("Big", "4/4"), me);
		var small = Hand(TestGames.Creature("Small", "3/3"), me);
		var hand = HandCount(me);
		Cast(big, []);
		Assert.That(HandCount(me), Is.EqualTo(hand - 1 + 1));
		Cast(small, []);
		Assert.That(HandCount(me), Is.EqualTo(hand - 2 + 1), "power 3: nothing");
	}

	[Test]
	public void GarrukCurseBreaker_PlusTwoUntapsTwoLands_MinusThreeABeast()
	{
		var garruk = Walker("Garruk, Curse Breaker", 3);
		TapAllLands(me);
		var lands = s.CardsIn(me, ZoneType.Battlefield)
			.Where(c => c.Is(CardType.Land))
			.Take(2)
			.Select(c => c.Id)
			.ToArray();
		Activate(garruk, 0, lands);
		Assert.That(lands.Select(l => s.GetCard(l).IsTapped), Is.EqualTo(new[] { false, false }));
		var other = Walker("Garruk, Curse Breaker", 3);
		Activate(other, 1);
		Assert.That(
			s.CardsIn(me, ZoneType.Battlefield).Count(c => c.Name == "Beast"),
			Is.EqualTo(1)
		);
	}

	[Test]
	public void GarrukCurseBreaker_MinusFour_AttackersGetPlusTwoAndTrample()
	{
		var garruk = Walker("Garruk, Curse Breaker", 4);
		var bear = Field(TestGames.Creature("Bear", "2/2"), me);
		Activate(garruk, 2);
		s = TestGames.PassUntil(s, x => x.GetPendingChoice() is DeclareAttackerChoice);
		while (s.GetPendingChoice() is DeclareAttackerChoice declare)
			s = s.Choose([declare.CreatureId == bear ? them : CombatOption.None]);
		Resolve();
		var card = s.GetCard(bear);
		Assert.That((s.Power(card), s.Has(card, "Trample")), Is.EqualTo((4, true)));
	}

	// ===== Tam, the Possibility =====

	[Test]
	public void Tam_PlaneswalkerSpellsCostOneLess()
	{
		Field(FraCards.Get("Tam, the Possibility"), me);
		TapAllLands(me);
		var walker = Hand(TestGames.Planeswalker("Walker", 3), me); // {1}
		var other = Hand(TestGames.Inline("Other", "{1}", "Sorcery"), me);
		Assert.That((Casts(walker).Any(), Casts(other).Any()), Is.EqualTo((true, false)));
	}

	[Test]
	public void Tam_ProliferatesOncePerPlaneswalkerType()
	{
		var tam = Field(FraCards.Get("Tam, the Possibility"), me);
		var ajani = Walker("Ajani Resolute", 3);
		var jace = Walker("Jace, Reality Sculptor", 3);
		var mine = Field(TestGames.Creature("Mine", "1/1"), me);
		s = s.UpdateObject(mine, s.GetCard(mine) with { PlusOneCounters = 1 }); // TEST SETUP
		var theirs = Field(TestGames.Creature("Theirs", "5/5"), them);
		s = s.UpdateObject(theirs, s.GetCard(theirs) with { MinusOneCounters = 1 }); // TEST SETUP
		s = s.Act(Activations(tam).Single());
		Resolve();
		Assert.That(
			(
				s.GetCard(ajani).Loyalty,
				s.GetCard(jace).Loyalty,
				s.GetCard(mine).PlusOneCounters,
				s.GetCard(theirs).MinusOneCounters
			),
			Is.EqualTo((3 + 2, 3 + 2, 1 + 2, 1 + 2)),
			"two types (Ajani, Jace): twice"
		);
	}
}
