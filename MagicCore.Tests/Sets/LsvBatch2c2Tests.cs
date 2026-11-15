using ImmutableGameObjects;
using MagicCore.Actions;
using MagicCore.Cards;
using MagicCore.Effects;
using MagicCore.Sets;
using MagicCore.Targeting;
using Nu = NUnit.Framework.Is;

namespace MagicCore.Tests.Sets;

/// <summary>
/// MC-4.4c2, LSVPower — red's last 16, each the nearest piece with an option (MC-6's rule): each card's ability
/// FIRES, against its PRINTED text, through real casting and the real stack.
/// </summary>
public class LsvBatch2c2Tests : FraBoardTest
{
	static CardDefinition Lsv(string name) => CardRegistry.Get(name, "LSV");

	static CardDefinition Boon =>
		TestGames.Inline("Boon", "{1}", "Instant", null, new GainLife(Ref.You, 1));

	/// <summary>A 2/2 of a creature type, costing {1}.</summary>
	static CardDefinition Typed(string type) =>
		new(
			new CardData(
				type,
				"normal",
				"{1}",
				1,
				$"Creature — {type}",
				null,
				"2",
				"2",
				null,
				null,
				[],
				null
			),
			[]
		);

	static CardDefinition TreasureCard =>
		TestGames.Inline("Treasure", "{0}", "Artifact — Treasure");

	int Tokens(int player, string name) =>
		s.CardsIn(player, ZoneType.Battlefield).Count(c => c.IsToken && c.Name == name);

	bool InExile(int card) => s.IsIn(card, s.GetCard(card).OwnerId, ZoneType.Exile);

	int? PlayableUntil(int card) => s.GetCard(card).GetComponent<PlayableFromExile>()?.UntilTurn;

	void AttackWith(params int[] attackers)
	{
		s = TestGames.PassUntil(s, x => x.GetPendingChoice() is DeclareAttackerChoice);
		while (s.GetPendingChoice() is DeclareAttackerChoice d)
			s = s.Choose([attackers.Contains(d.CreatureId) ? them : 0]);
	}

	/// <summary>Puts a fresh card on top of a library.</summary>
	int OnTop(CardDefinition card, int player)
	{
		int id;
		(s, id) = TestGames.Put(s, player, card, ZoneType.Library);
		s = s.MoveObjectToFront(id, s.ZoneId(player, ZoneType.Library));
		return id;
	}

	// ===== MC-4.4c2a =====

	[Test]
	public void BroadsideBombardiers_Boast_TwoPlusTheSacrificedManaValue_OnlyAfterAttacking_OncePerTurn()
	{
		var bombardiers = Field(Lsv("Broadside Bombardiers"), me);
		var monolith = Field(Lsv("Basalt Monolith"), me); // {3}
		Field(Lsv("Basalt Monolith"), me);
		Assert.That(Activations(bombardiers), Nu.Empty, "only if it attacked this turn");
		s = s.UpdateObject(
			bombardiers,
			s.GetCard(bombardiers) with
			{
				AttackedOnTurn = s.GetGame().TurnNumber,
			}
		);
		var life = Life(them);
		s = s.Act(
			Activations(bombardiers).First(a => a.SacrificeId == monolith && a.Targets[0] == them)
		);
		Resolve();
		Assert.That(Life(them), Nu.EqualTo(life - (2 + 3)));
		Assert.That(Activations(bombardiers), Nu.Empty, "only once each turn");
	}

	[Test]
	public void CoriSteelCutter_Flurry_SecondSpellMakesAMonk_WhichItMayEquip()
	{
		var cutter = Field(Lsv("Cori-Steel Cutter"), me);
		Cast(Hand(Boon, me), []);
		Assert.That(Tokens(me, "Monk"), Nu.EqualTo(0), "the first spell");
		var second = Hand(Boon, me);
		s = s.Act(new CastSpellAction { PlayerId = me, CardId = second });
		Resolve(On<YesNoChoice>(1));
		var monk = s.CardsIn(me, ZoneType.Battlefield).Single(c => c.Name == "Monk");
		Assert.That(s.GetCard(cutter).AttachedToId, Nu.EqualTo(monk.Id));
		Assert.That(
			(s.Power(monk), s.Has(monk, "Trample"), s.Has(monk, "Haste")),
			Nu.EqualTo((2, true, true))
		);
		Cast(Hand(Boon, me), []);
		Assert.That(s.Power(s.GetCard(monk.Id)), Nu.EqualTo(3), "prowess");
		Assert.That(Tokens(me, "Monk"), Nu.EqualTo(1), "only the second spell");
	}

	[Test]
	public void BonehoardDracosaur_Upkeep_ExilesTwoPlayable_ADinosaurForALand_ATreasureForANonland()
	{
		Field(Lsv("Bonehoard Dracosaur"), me);
		var land = OnTop(TestGames.Card("Mountain"), me);
		var spell = OnTop(Boon, me);
		s = TestGames.PassUntil(
			s,
			x =>
				x.GetGame().ActivePlayerId == me
				&& x.GetGame().Step == Step.Upkeep
				&& !x.StackIsEmpty()
		);
		Resolve();
		Assert.That(InExile(land) && InExile(spell), Nu.True);
		Assert.That(PlayableUntil(spell), Nu.EqualTo(s.GetGame().TurnNumber), "this turn");
		Assert.That((Tokens(me, "Dinosaur"), Tokens(me, "Treasure")), Nu.EqualTo((1, 1)));
	}

	[Test]
	public void Inti_AttackDiscardCountersAnAttacker_AndADiscardExilesThePlayableTop()
	{
		var inti = Field(Lsv("Inti, Seneschal of the Sun"), me);
		var boon = Hand(Boon, me);
		var top = OnTop(Boon, me);
		AttackWith(inti);
		Resolve(c =>
			c switch
			{
				ChooseCardsFromHandChoice => [boon],
				ChooseTargetChoice => [inti],
				_ => c.Options.Take(c.MinChoices).Select(o => o.Id),
			}
		);
		Assert.That(s.GetCard(inti).PlusOneCounters, Nu.EqualTo(1));
		Assert.That(s.Has(s.GetCard(inti), "Trample"), Nu.True);
		Assert.That(InExile(top), Nu.True, "whenever you discard one or more cards");
		Assert.That(
			PlayableUntil(top),
			Nu.EqualTo(s.GetGame().TurnNumber),
			"my end step is still to come"
		);
	}

	[Test]
	public void Inti_DiscardOnTheirTurn_PlayableUntilMyNextEndStep()
	{
		Field(Lsv("Inti, Seneschal of the Sun"), me);
		var top = OnTop(Boon, me);
		s = TestGames.PassUntil(
			s,
			x => x.GetGame().ActivePlayerId == them && x.GetGame().Step == Step.PreCombatMain
		);
		var turn = s.GetGame().TurnNumber;
		s = s.Act(new PassPriorityAction { PlayerId = them });
		Hand(Boon, me);
		var toss = Hand(
			TestGames.Inline("Toss", "{0}", "Instant", null, new Discard(Ref.You, 1)),
			me
		);
		s = s.Act(new CastSpellAction { PlayerId = me, CardId = toss });
		Resolve();
		Assert.That(PlayableUntil(top), Nu.EqualTo(turn + 1), "my next turn");
	}

	[Test]
	public void Magda_FiveTreasuresSearchAnArtifact_DwarvesGetOne_TappedDwarfMakesATreasure()
	{
		var magda = Field(Lsv("Magda, Brazen Outlaw"), me);
		var treasures = Enumerable.Range(0, 4).Select(_ => Field(TreasureCard, me)).ToList();
		Assert.That(Activations(magda), Nu.Empty, "four Treasures can't pay");
		treasures.Add(Field(TreasureCard, me));
		Field(TreasureCard, me);
		var monolith = OnTop(Lsv("Basalt Monolith"), me);
		s = s.Act(Activations(magda).Single());
		Resolve(c =>
			c switch
			{
				ChoosePermanentChoice => treasures,
				ChooseFromLibraryChoice => [monolith],
				_ => c.Options.Take(c.MinChoices).Select(o => o.Id),
			}
		);
		Assert.That(treasures.Any(Alive), Nu.False, "the five chosen sacrificed");
		Assert.That(Alive(monolith), Nu.True, "an artifact card onto the battlefield");

		var dwarf = Field(Typed("Dwarf"), me);
		Assert.That(
			(s.Power(s.GetCard(dwarf)), s.Power(s.GetCard(magda))),
			Nu.EqualTo((3, 2)),
			"other Dwarves"
		);
		AttackWith(dwarf);
		Resolve();
		Assert.That(
			Tokens(me, "Treasure"),
			Nu.EqualTo(1),
			"whenever a Dwarf you control becomes tapped"
		);
	}

	[Test]
	public void Tersa_DiscardUpToTwoDrawThatMany_AttackExilesARandomGraveyardCardPlayable()
	{
		s = TestGames.EmptyHands(s);
		Hand(Boon, me);
		var toss = Hand(Boon, me);
		var tersa = Hand(Lsv("Tersa Lightshatter"), me);
		s = s.Act(new CastSpellAction { PlayerId = me, CardId = tersa });
		Resolve(On<ChooseCardsFromHandChoice>(toss));
		Assert.That(s.IsIn(toss, me, ZoneType.Graveyard), Nu.True);
		Assert.That(HandCount(me), Nu.EqualTo(2), "one discarded, one drawn");

		for (var i = 0; i < 6; i++)
			(s, _) = TestGames.Put(s, me, Boon, ZoneType.Graveyard);
		var graveyard = s.CardsIn(me, ZoneType.Graveyard).Select(c => c.Id).ToList();
		Assert.That(graveyard, Has.Count.EqualTo(7));
		AttackWith(tersa);
		Resolve();
		var exiled = graveyard.Where(InExile).ToList();
		Assert.That(exiled, Has.Count.EqualTo(1), "a card at random from your graveyard");
		Assert.That(
			exiled[0],
			Nu.Not.EqualTo(graveyard[0]),
			"at random (this seed), not the first in the zone"
		);
		Assert.That(PlayableUntil(exiled[0]), Nu.EqualTo(s.GetGame().TurnNumber), "this turn");
		Assert.That(
			s.GetCard(exiled[0]).GetComponent<PlayableFromExile>()!.Blind,
			Nu.False,
			"a graveyard card was public: the AI knows what it is"
		);
	}

	// ===== MC-4.4c2b =====

	bool InGraveyard(int card) => s.IsIn(card, s.GetCard(card).OwnerId, ZoneType.Graveyard);

	void ToEndStep()
	{
		s = TestGames.PassUntil(s, x => x.GetGame().Step == Step.End && !x.StackIsEmpty());
		Resolve();
	}

	[Test]
	public void SneakAttack_PutsACreatureFromHandWithHaste_SacrificedAtTheNextEndStep()
	{
		var sneak = Field(Lsv("Sneak Attack"), me);
		var ogre = Hand(TestGames.Creature("Ogre", "3/3"), me);
		s = s.Act(Activations(sneak).Single());
		Resolve(On<ChooseCardsFromHandChoice>(ogre));
		Assert.That(Alive(ogre), Nu.True, "onto the battlefield");
		Assert.That(s.Has(s.GetCard(ogre), "Haste"), Nu.True, "that creature gains haste");
		ToEndStep();
		Assert.That(InGraveyard(ogre), Nu.True, "sacrificed at the beginning of the next end step");
	}

	[Test]
	public void OrcishLumberjack_SacrificesAForest_ThreeManaInAnyCombinationOfRedAndGreen()
	{
		var lumberjack = Field(Lsv("Orcish Lumberjack"), me);
		var moves = s.LegalActions()
			.OfType<TapForManaAction>()
			.Where(a => a.CardId == lumberjack)
			.ToList();
		Assert.That(moves, Has.Count.EqualTo(4 * 4), "four Forests, four splits each");
		Assert.That(
			moves.All(a => s.GetCard(a.SacrificeId).Name == "Forest"),
			Nu.True,
			"sacrifice a Forest"
		);
		var move = moves.First(a => a.OfTheOther == 1);
		Assert.That(
			(move with { OfTheOther = 4 }).ValidateAdd(s).IsValid,
			Nu.False,
			"three mana, never four of the other"
		);
		s = s.Act(move);
		var pool = s.GetPlayer(me).ManaPool;
		Assert.That((pool.Red, pool.Green, pool.Total), Nu.EqualTo((2, 1, 3)));
		Assert.That(InGraveyard(move.SacrificeId), Nu.True);
		Assert.That(s.GetCard(lumberjack).IsTapped, Nu.True);
	}

	[Test]
	public void FieryConfluence_ChooseThree_TheSameModeMoreThanOnce()
	{
		for (var i = 0; i < 4; i++)
			Field(TestGames.Card("Mountain"), me);
		var ogre = Field(TestGames.Creature("Ogre", "3/3"), them);
		var first = Field(TestCards.Trinket, them);
		var second = Field(TestCards.Trinket, them);
		var confluence = Hand(Lsv("Fiery Confluence"), me);
		var casts = Casts(confluence).ToList();
		Assert.That(
			casts.Count(c => c.Modes.SequenceEqual([1, 2, 2])),
			Nu.EqualTo(1),
			"two artifacts: one cast for the pair, not one per order"
		);
		Assert.That(casts.Any(c => c.Modes.SequenceEqual([2, 2, 2])), Nu.False, "three artifacts");
		Assert.That(casts.Any(c => c.Modes.Length != 3), Nu.False, "choose three");

		Cast(confluence, [0, 0, 0]);
		Assert.That(InGraveyard(ogre), Nu.True, "1 damage to each creature, three times");

		var life = Life(them);
		Cast(Hand(Lsv("Fiery Confluence"), me), [1, 2, 2], first, second);
		Assert.That(Life(them), Nu.EqualTo(life - 2), "2 damage to each opponent");
		Assert.That(Alive(first) || Alive(second), Nu.False, "destroy target artifact, twice");
	}

	[Test]
	public void ScreamingNemesis_DealsThatMuchToAnyOtherTarget_APlayerDealtItCantGainLifeForTheRestOfTheGame()
	{
		var nemesis = Field(Lsv("Screaming Nemesis"), me);
		var bear = Field(TestGames.Creature("Bear", "2/2"), them);
		var shock = Hand(TestCards.Shock(2, TargetSpec.Creature), me);
		s = s.Act(
			new CastSpellAction
			{
				PlayerId = me,
				CardId = shock,
				Targets = [nemesis],
			}
		);
		var life = Life(them);
		Resolve(c =>
		{
			if (c is ChooseTargetChoice)
				Assert.That(
					c.Options.Select(o => o.Id),
					Nu.SupersetOf(new[] { me, them, bear }).And.None.EqualTo(nemesis),
					"any OTHER target"
				);
			return c switch
			{
				ChooseTargetChoice => [them],
				_ => c.Options.Take(c.MinChoices).Select(o => o.Id),
			};
		});
		Assert.That(Life(them), Nu.EqualTo(life - 2), "that much damage");
		s = global::MagicCore.Rules.Life.Gain(s, them, 5);
		Assert.That(Life(them), Nu.EqualTo(life - 2), "they can't gain life");
		s = global::MagicCore.Rules.Life.Lose(s, them, 1);
		Assert.That(Life(them), Nu.EqualTo(life - 3), "only gaining: life is still lost");
		var mine = Life(me);
		s = global::MagicCore.Rules.Life.Gain(s, me, 5);
		Assert.That(Life(me), Nu.EqualTo(mine + 5), "the player dealt damage, not me");

		var again = Hand(TestCards.Shock(1, TargetSpec.Creature), me);
		s = s.Act(
			new CastSpellAction
			{
				PlayerId = me,
				CardId = again,
				Targets = [nemesis],
			}
		);
		Resolve(On<ChooseTargetChoice>(them));
		Assert.That(Life(them), Nu.EqualTo(life - 4), "dealt damage again");
		Assert.That(
			s.CardsIn(them, ZoneType.Command).Count(c => c.HasComponent<RestOfTheGame>()),
			Nu.EqualTo(1),
			"one \"can't gain life\", however often"
		);

		var turn = s.GetGame().TurnNumber;
		s = TestGames.PassUntil(s, x => x.GetGame().TurnNumber == turn + 2);
		s = global::MagicCore.Rules.Life.Gain(s, them, 5);
		Assert.That(Life(them), Nu.EqualTo(life - 4), "for the rest of the game");
	}

	[Test]
	public void ScreamingNemesis_ACreatureDealtTheDamage_NobodyLosesLifeGain()
	{
		var nemesis = Field(Lsv("Screaming Nemesis"), me);
		var bear = Field(TestGames.Creature("Bear", "2/2"), them);
		var shock = Hand(TestCards.Shock(2, TargetSpec.Creature), me);
		s = s.Act(
			new CastSpellAction
			{
				PlayerId = me,
				CardId = shock,
				Targets = [nemesis],
			}
		);
		Resolve(On<ChooseTargetChoice>(bear));
		Assert.That(InGraveyard(bear), Nu.True);
		var life = Life(them);
		s = global::MagicCore.Rules.Life.Gain(s, them, 5);
		Assert.That(Life(them), Nu.EqualTo(life + 5));
	}

	[Test]
	public void GoblinWelder_SwapsAnArtifactWithOneInThatPlayersGraveyard()
	{
		var welder = Field(Lsv("Goblin Welder"), me);
		var trinket = Field(TestCards.Trinket, them);
		int theirs,
			mine;
		(s, theirs) = TestGames.Put(s, them, Lsv("Sol Ring"), ZoneType.Graveyard);
		(s, mine) = TestGames.Put(s, me, Lsv("Sol Ring"), ZoneType.Graveyard);
		var swaps = Activations(welder).ToList();
		Assert.That(
			swaps.Select(a => (a.Targets[0], a.Targets[1])),
			Nu.EquivalentTo(new[] { (trinket, theirs) }),
			"an artifact card in THAT player's graveyard"
		);
		Assert.That(
			(swaps.Single() with { Targets = [trinket, mine] }).ValidateAdd(s).IsValid,
			Nu.False,
			"refused, not only never offered"
		);
		s = s.Act(swaps.Single());
		Resolve();
		Assert.That(InGraveyard(trinket), Nu.True, "that player sacrifices the artifact");
		Assert.That(
			Alive(theirs) && s.GetCard(theirs).ControllerId == them,
			Nu.True,
			"and returns the artifact card to the battlefield"
		);
		Assert.That(InGraveyard(mine), Nu.True);
	}

	[Test]
	public void GoblinWelder_ATargetNoLongerLegal_NothingHappens()
	{
		var welder = Field(Lsv("Goblin Welder"), me);
		var trinket = Field(TestCards.Trinket, them);
		int theirs;
		(s, theirs) = TestGames.Put(s, them, Lsv("Sol Ring"), ZoneType.Graveyard);
		var swap = Activations(welder).Single();
		var gone = s.Act(swap).MoveCard(theirs, s.ZoneId(them, ZoneType.Exile));
		s = gone;
		Resolve();
		Assert.That(
			Alive(trinket),
			Nu.True,
			"the card left the graveyard: both must still be legal"
		);

		s = s.Untap(welder);
		(s, theirs) = TestGames.Put(s, them, Lsv("Sol Ring"), ZoneType.Graveyard);
		s = s.Act(Activations(welder).Single());
		s = s.GainControl(trinket, me);
		Resolve();
		Assert.That(
			Alive(trinket) && InGraveyard(theirs),
			Nu.True,
			"the artifact changed controller: the card is no longer in THAT player's graveyard"
		);
	}

	[Test]
	public void RobberOfTheRich_ExilesTheirTopCard_CastableOnlyInATurnIAttackedWithARogue()
	{
		s = TestGames.EmptyHands(s);
		var robber = Field(Lsv("Robber of the Rich"), me);
		Hand(Boon, them);
		var top = OnTop(Boon, them);
		AttackWith(robber);
		Resolve();
		Assert.That(InExile(top), Nu.True, "they have more cards in hand: their top card exiled");
		Assert.That(Casts(top), Nu.Not.Empty, "I attacked with a Rogue this turn");
		var turn = s.GetGame().TurnNumber;
		s = TestGames.PassUntil(
			s,
			x =>
				x.GetGame().TurnNumber > turn
				&& x.GetGame().ActivePlayerId == me
				&& x.GetGame().Step == Step.PreCombatMain
		);
		Assert.That(InExile(top), Nu.True);
		Assert.That(Casts(top), Nu.Empty, "a turn with no Rogue attacking");
		AttackWith(robber);
		Resolve();
		Assert.That(Casts(top), Nu.Not.Empty, "during ANY turn I attacked with a Rogue");
		s = s.Act(Casts(top).First());
		var life = Life(me);
		Resolve();
		Assert.That(Life(me), Nu.EqualTo(life + 1), "I cast their card");
	}

	[Test]
	public void RobberOfTheRich_TheyHaveNoMoreCardsThanMe_NothingExiled_AndALandIsNeverPlayed()
	{
		s = TestGames.EmptyHands(s);
		var robber = Field(Lsv("Robber of the Rich"), me);
		Hand(Boon, me);
		Hand(Boon, them);
		var top = OnTop(TestGames.Card("Mountain"), them);
		AttackWith(robber);
		Resolve();
		Assert.That(InExile(top), Nu.False, "a card each: not MORE cards in hand than me");

		var turn = s.GetGame().TurnNumber;
		s = TestGames.PassUntil(
			s,
			x =>
				x.GetGame().TurnNumber > turn
				&& x.GetGame().ActivePlayerId == me
				&& x.GetGame().Step == Step.PreCombatMain
		);
		s = TestGames.EmptyHands(s);
		Hand(Boon, them);
		top = OnTop(TestGames.Card("Mountain"), them);
		AttackWith(robber);
		Resolve();
		Assert.That(InExile(top), Nu.True);
		s = TestGames.PassUntil(s, x => x.GetGame().Step == Step.PostCombatMain);
		Hand(TestGames.Card("Mountain"), me);
		Assert.That(
			s.LegalActions().OfType<PlayLandAction>(),
			Nu.Not.Empty.And.None.Matches<PlayLandAction>(a => a.CardId == top),
			"you may CAST that card: a land in hand is playable now, theirs never"
		);
	}

	[Test]
	public void UnderworldBreach_EscapeCostsThreeOtherGraveyardCards_NotExiledAfter_SacrificedAtTheEndStep()
	{
		var breach = Field(Lsv("Underworld Breach"), me);
		int boon;
		(s, boon) = TestGames.Put(s, me, Boon, ZoneType.Graveyard);
		var fuel = new List<int>();
		for (var i = 0; i < 2; i++)
		{
			int id;
			(s, id) = TestGames.Put(s, me, TestGames.Card("Mountain"), ZoneType.Graveyard);
			fuel.Add(id);
		}
		Assert.That(Casts(boon), Nu.Empty, "two other cards can't pay the escape cost");
		int third;
		(s, third) = TestGames.Put(s, me, TestGames.Card("Mountain"), ZoneType.Graveyard);
		fuel.Add(third);
		Assert.That(
			s.LegalActions().OfType<PlayLandAction>().Any(a => fuel.Contains(a.CardId)),
			Nu.False,
			"each NONLAND card has escape"
		);
		var life = Life(me);
		s = s.Act(Casts(boon).Single());
		Resolve();
		Assert.That(Life(me), Nu.EqualTo(life + 1));
		Assert.That(fuel.All(InExile), Nu.True, "three other cards exiled from my graveyard");
		Assert.That(InGraveyard(boon), Nu.True, "escape isn't flashback: back to the graveyard");
		ToEndStep();
		Assert.That(InGraveyard(breach), Nu.True, "sacrificed at the beginning of the end step");
	}

	// ===== MC-4.4c2c =====

	static CardDefinition Free(string name, params GameAction[] effects) =>
		TestGames.Inline(name, "{0}", "Instant", null, effects);

	[Test]
	public void Laelia_AttackExilesTheTopPlayableThisTurn_AndThatExileGivesHerACounter()
	{
		var laelia = Field(Lsv("Laelia, the Blade Reforged"), me);
		var top = OnTop(Boon, me);
		AttackWith(laelia);
		Resolve();
		Assert.That(InExile(top), Nu.True);
		Assert.That(PlayableUntil(top), Nu.EqualTo(s.GetGame().TurnNumber), "this turn");
		Assert.That(
			s.GetCard(laelia).PlusOneCounters,
			Nu.EqualTo(1),
			"put into exile from my library"
		);
	}

	[Test]
	public void Laelia_OneOrMoreFromMyGraveyard_OneCounter_NoneForAnyOtherMove()
	{
		var laelia = Field(Lsv("Laelia, the Blade Reforged"), me);
		for (var i = 0; i < 2; i++)
			(s, _) = TestGames.Put(s, me, Boon, ZoneType.Graveyard);
		Cast(Hand(Free("Purge", new MoveTo(Ref.CardsInYourGraveyard, ZoneType.Exile)), me), []);
		Assert.That(
			s.GetCard(laelia).PlusOneCounters,
			Nu.EqualTo(1),
			"two cards at once: one or more"
		);

		Cast(Hand(Free("Dig", new Mill(Ref.You, 2)), me), []);
		Cast(Hand(Free("Forget", new MoveTo(Ref.CardsInYourHand, ZoneType.Exile)), me), []);
		Cast(Hand(Free("Raise", new MoveTo(Ref.CardsInYourGraveyard, ZoneType.Hand)), me), []);
		OnTop(Boon, them);
		Cast(Hand(Free("Rob", new ExileTopPlayable { Of = Ref.EachOpponent }), me), []);
		Assert.That(
			s.GetCard(laelia).PlusOneCounters,
			Nu.EqualTo(1),
			"milled, exiled from my hand, returned from my graveyard, exiled from THEIR library: none"
		);
	}

	[Test]
	public void ACardExiledFromMyLibrary_DidntLeaveMyGraveyard()
	{
		Field(CardRegistry.Get("Vash, Freelance Archaeologist", "TRK"), me);
		OnTop(Boon, me);
		var hand = HandCount(me);
		Cast(Hand(Free("Impulse", new ExileTopPlayable()), me), []);
		Assert.That(HandCount(me), Nu.EqualTo(hand), "Vash: whenever a card leaves your GRAVEYARD");
		(s, _) = TestGames.Put(s, me, Boon, ZoneType.Graveyard);
		Cast(Hand(Free("Purge", new MoveTo(Ref.CardsInYourGraveyard, ZoneType.Exile)), me), []);
		Assert.That(HandCount(me), Nu.EqualTo(hand + 1), "and one that does still draws");
	}

	// ===== MC-4.4c2d =====

	const string Goblin = "\"Name Sticker\" Goblin";

	ManaPool Pool => s.GetPlayer(me).ManaPool;

	[Test]
	public void NameStickerGoblin_CastWithEightOthers_RollsForFourToSixRed()
	{
		for (var i = 0; i < 8; i++)
			Field(Lsv(Goblin), me);
		Cast(Hand(Lsv(Goblin), me), []);
		Assert.That(
			Pool.Red,
			Nu.InRange(4, 6),
			"9 or fewer, itself counted: 1-6 four, 7-14 five, 15-20 six"
		);
		Assert.That(Pool.Total, Nu.EqualTo(Pool.Red));
	}

	[Test]
	public void NameStickerGoblin_TheTenth_RollsNothing()
	{
		for (var i = 0; i < 9; i++)
			Field(Lsv(Goblin), me);
		Cast(Hand(Lsv(Goblin), me), []);
		Assert.That(Pool.Total, Nu.EqualTo(0), "ten creatures with that name");
	}

	[Test]
	public void NameStickerGoblin_FromAGraveyardOrExile_Nothing_FromTheHandByAnEffect_Rolls()
	{
		(s, _) = TestGames.Put(s, me, Lsv(Goblin), ZoneType.Graveyard);
		Cast(
			Hand(Free("Raise", new MoveTo(Ref.CardsInYourGraveyard, ZoneType.Battlefield)), me),
			[]
		);
		Assert.That(Pool.Total, Nu.EqualTo(0), "from a graveyard");
		(s, _) = TestGames.Put(s, me, Lsv(Goblin), ZoneType.Exile);
		Cast(
			Hand(
				Free(
					"Recall",
					new MoveTo(
						Ref.All(ZoneType.Exile, MagicCore.Filters.Is.Creature, Ref.You),
						ZoneType.Battlefield
					)
				),
				me
			),
			[]
		);
		Assert.That(Pool.Total, Nu.EqualTo(0), "from exile");
		foreach (var card in s.CardsIn(me, ZoneType.Hand).ToList())
			s = s.MoveCard(card.Id, s.ZoneId(me, ZoneType.Graveyard));
		Hand(Lsv(Goblin), me);
		Cast(Hand(Free("Drop", new MoveTo(Ref.CardsInYourHand, ZoneType.Battlefield)), me), []);
		Assert.That(
			Pool.Red,
			Nu.InRange(4, 6),
			"put onto the battlefield from the hand: anywhere else"
		);
	}

	[Test]
	public void NameStickerGoblin_GoneBeforeItsTriggerResolves_Nothing()
	{
		var goblin = Hand(Lsv(Goblin), me);
		s = s.Act(new CastSpellAction { PlayerId = me, CardId = goblin });
		s = s.Act(new PassPriorityAction { PlayerId = me });
		s = s.Act(new PassPriorityAction { PlayerId = them });
		Assert.That(
			(Alive(goblin), s.StackIsEmpty()),
			Nu.EqualTo((true, false)),
			"its trigger waits"
		);
		Cast(Hand(Free("Wrath", new Destroy(Ref.CreaturesYouControl)), me), []);
		Assert.That(Alive(goblin), Nu.False);
		Assert.That(Pool.Total, Nu.EqualTo(0), "if it's on the battlefield");
	}

	[Test]
	public void ATokenJustMade_EntersFromNoZone_SoTheGoblinsCopyRolls()
	{
		var goblin = Field(Lsv(Goblin), me);
		Cast(Hand(Free("Twin", new CreateTokenCopy(Ref.CreaturesYouControl)), me), []);
		Assert.That(Alive(goblin), Nu.True);
		Assert.That(Pool.Red, Nu.InRange(4, 6));
	}

	[Test]
	public void NotFrom_FitsOnlyAnEventThatKnowsWhereFrom()
	{
		Assert.That(
			() => new Trigger(EventKind.Died) { NotFrom = [ZoneType.Exile] },
			Throws.ArgumentException
		);
		Assert.That(() => new Trigger(EventKind.Entered) { From = ZoneType.Hand }, Throws.Nothing);
	}

	[Test]
	public void RollDie_EveryRollLandsInExactlyOneRow_AndEachFaceComesUp()
	{
		var life = Life(me);
		var die = new RollDie( // the rows out of order: a row is both its bounds
			3,
			[
				new(2, 2, [new GainLife(Ref.You, 100)]),
				new(1, 1, [new GainLife(Ref.You, 1)]),
				new(3, 3, [new GainLife(Ref.You, 10000)]),
			]
		);
		for (var i = 0; i < 60; i++)
			Cast(Hand(Free("Roll", die), me), []);
		var (threes, rest) = Math.DivRem(Life(me) - life, 10000);
		var (twos, ones) = Math.DivRem(rest, 100);
		Assert.That(ones + twos + threes, Nu.EqualTo(60), "one row a roll");
		Assert.That(new[] { ones, twos, threes }, Has.All.GreaterThan(0), "each face comes up");
	}

	// ===== MC-4.4c2e =====

	void EmptyMyHand()
	{
		foreach (var card in s.CardsIn(me, ZoneType.Hand).ToList())
			s = s.MoveCard(card.Id, s.ZoneId(me, ZoneType.Graveyard));
	}

	/// <summary>A 1/5 of theirs: it shows 4 damage and lives.</summary>
	int Ox(string name) => Field(TestGames.Creature(name, "1/5"), them);

	int Marked(int card) => s.GetCard(card).Damage;

	/// <summary>Casts Pyrokinesis for its mana, answering its choices — and stops with it on the stack.</summary>
	void CastPyrokinesis(int[] targets, string? split = null)
	{
		var pyro = Hand(Lsv("Pyrokinesis"), me);
		s = s.Act(Casts(pyro).Single(c => !c.WithoutPayingManaCost));
		while (s.GetPendingChoice() is { } choice)
			s = s.Choose(
				choice switch
				{
					ChooseXTargetsChoice => targets,
					ChooseKeywordChoice ways when split is not null =>
					[
						ways.Keywords.IndexOf(split) + 1,
					],
					_ => throw new AssertionException($"not asked: {choice.Prompt}"),
				}
			);
	}

	[Test]
	public void Pyrokinesis_OneCastAgainstTwelveCreatures_TheTargetsAndTheSplitChosenAsItIsCast()
	{
		EmptyMyHand();
		var a = Ox("A");
		var b = Ox("B");
		var rest = Enumerable.Range(0, 10).Select(_ => Ox("Ox")).ToList();
		var pyro = Hand(Lsv("Pyrokinesis"), me);
		Assert.That(Casts(pyro).ToList(), Has.Count.EqualTo(1), "never a cast per set and split");
		s = s.Act(Casts(pyro).Single());
		var targets = (ChooseXTargetsChoice)s.GetPendingChoice()!;
		Assert.That(targets.Options, Has.Count.EqualTo(12));
		Assert.That((targets.MinChoices, targets.MaxChoices), Nu.EqualTo((1, 4)), "any number");
		s = s.Choose([b, a]);
		var ways = (ChooseKeywordChoice)s.GetPendingChoice()!;
		Assert.That(
			ways.Keywords,
			Nu.EqualTo(new[] { "3 to A, 1 to B", "2 to A, 2 to B", "1 to A, 3 to B" })
		);
		s = s.Choose([3]);
		Resolve();
		Assert.That((Marked(a), Marked(b)), Nu.EqualTo((1, 3)), "divided as I chose");
		Assert.That(rest.Select(Marked), Has.All.EqualTo(0));
	}

	[Test]
	public void Pyrokinesis_OneTargetTakesAllFour_FourTakeOneEach_NoSplitToChoose()
	{
		var oxen = Enumerable.Range(0, 4).Select(i => Ox($"Ox {i}")).ToList();
		var alone = Ox("Alone");
		CastPyrokinesis([alone]);
		Resolve();
		Assert.That(Marked(alone), Nu.EqualTo(4));
		CastPyrokinesis([.. oxen]);
		Resolve();
		Assert.That(oxen.Select(Marked), Nu.EqualTo(new[] { 1, 1, 1, 1 }));
	}

	[Test]
	public void Pyrokinesis_ATargetGoneBeforeItResolves_TakesItsShareWithIt()
	{
		var a = Ox("A");
		var b = Ox("B");
		CastPyrokinesis([a, b], "2 to A, 2 to B");
		Cast(
			Hand(Free("Kill B", new Destroy(Ref.Permanents(MagicCore.Filters.Is.Named("B")))), me),
			[]
		);
		Assert.That(Alive(b), Nu.False);
		Assert.That(Marked(a), Nu.EqualTo(2), "the division was announced as it was cast");
	}

	[Test]
	public void Pyrokinesis_ExileARedCardFromMyHand_RatherThanPay()
	{
		EmptyMyHand();
		var ox = Ox("Ox");
		var bolt = Hand(Lsv("Lightning Bolt"), me);
		Hand(Boon, me);
		var pyro = Hand(Lsv("Pyrokinesis"), me);
		var free = Casts(pyro).Where(c => c.WithoutPayingManaCost).ToList();
		Assert.That(
			free.Select(c => c.ExileId),
			Nu.EqualTo(new[] { bolt }),
			"a RED card, not itself"
		);
		s = s.Act(free[0]);
		Resolve(On<ChooseXTargetsChoice>(ox));
		Assert.That(InExile(bolt), Nu.True);
		Assert.That(Marked(ox), Nu.EqualTo(4));
		Assert.That(s.CardsIn(me, ZoneType.Battlefield).Count(c => c.IsTapped), Nu.EqualTo(0));
	}

	[Test]
	public void ADivision_IsOneOfTheWaysItsTargetsAllow()
	{
		var a = Ox("A");
		var b = Ox("B");
		var pyro = Hand(Lsv("Pyrokinesis"), me);
		bool Legal(int[] targets, params int[] shares) =>
			new CastSpellAction
			{
				PlayerId = me,
				CardId = pyro,
				Targets = [.. targets],
				Shares = [.. shares],
			}
				.ValidateAdd(s)
				.IsValid;
		Assert.That(Legal([a, b, 0, 0], 3, 1), Nu.True);
		Assert.That(Legal([a, a, 0, 0], 3, 1), Nu.False, "different creatures");
		Assert.That(Legal([a, b, 0, 0], 3, 2), Nu.False, "five");
		Assert.That(Legal([a, b, 0, 0], 4, 0), Nu.False, "each target at least one");
		Assert.That(Legal([a, b, 0, 0]), Nu.False, "two targets: the split must be said");
		Assert.That(Legal([a, 0, 0, 0]), Nu.True, "one target: only one way");
		Assert.That(
			Division.Splits(4, 3).Select(w => string.Join("", w)),
			Nu.EqualTo(new[] { "211", "121", "112" })
		);
		Assert.That(Division.Fits(0, [a], [1]), Nu.False, "a spell that divides nothing");
	}

	[Test]
	public void DamageToSeveral_IsCountedBeforeAnyIsDealt()
	{
		Cast(Hand(Free("Ping", new DealDamage(Ref.You, 1)), me), []);
		var mine = new DealDamage(Ref.EachPlayer, Count.LifeLostThisTurnOf(Ref.You));
		var theirs = new DealDamage(Ref.EachPlayer, Count.LifeLostThisTurnOf(Ref.EachOpponent));
		var (my, their) = (Life(me), Life(them));
		Cast(Hand(Free("Echo", mine), me), []);
		Assert.That((Life(me), Life(them)), Nu.EqualTo((my - 1, their - 1)), "the 1 I had lost");
		Cast(Hand(Free("Echo", theirs), me), []);
		Assert.That((Life(me), Life(them)), Nu.EqualTo((my - 2, their - 2)), "the 1 they had lost");
	}

	[Test]
	public void ADividedSpell_WithAWordLeftEmptyBetweenItsTargets_GivesEachItsShare()
	{
		var a = Ox("A");
		var b = Ox("B");
		var pyro = Hand(Lsv("Pyrokinesis"), me);
		s = s.Act(
			new CastSpellAction
			{
				PlayerId = me,
				CardId = pyro,
				Targets = [a, 0, b, 0],
				Shares = [3, 1],
			}
		);
		Resolve();
		Assert.That((Marked(a), Marked(b)), Nu.EqualTo((3, 1)));
	}

	[Test]
	public void PureEnergy_DividingTwo_StaysACastPerCreatureAndPerPair()
	{
		Field(TestGames.Creature("A", "1/1"), me);
		Field(TestGames.Creature("B", "1/1"), me);
		var energy = Hand(CardRegistry.Get("Pure Energy", "TRK"), me);
		var casts = Casts(energy).ToList();
		Assert.That(casts, Has.Count.EqualTo(3), "A, B, A and B");
		Assert.That(casts.Select(c => c.ChoosesTargets), Has.All.False);
	}

	[Test]
	public void TheAi_PutsPyrokinesisWhereItPlaysOutBest_NeverOnItsOwnCreature()
	{
		Field(TestGames.Creature("Mine", "4/4"), me);
		var theirs = Field(TestGames.Creature("Theirs", "4/4"), them);
		var pyro = Hand(Lsv("Pyrokinesis"), me);
		s = s.Act(Casts(pyro).Single(c => !c.WithoutPayingManaCost));
		var choice = s.GetPendingChoice()!;
		Assert.That(choice.Options, Has.Count.EqualTo(2));
		Assert.That(
			new global::MagicCore.Ai.GreedyAi(me).Answer(s, choice),
			Nu.EqualTo(new[] { theirs })
		);
	}
}
