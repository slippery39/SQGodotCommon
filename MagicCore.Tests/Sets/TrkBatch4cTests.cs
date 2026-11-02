using ImmutableGameObjects;
using MagicCore.Actions;
using MagicCore.Ai;
using MagicCore.Cards;
using MagicCore.Effects;
using MagicCore.Rules;
using MagicCore.Sets;
using MagicCore.Targeting;
using MagicCore.Turns;
using Is = MagicCore.Filters.Is;
using Nu = NUnit.Framework.Is;

namespace MagicCore.Tests.Sets;

/// <summary>
/// MC-5.3 batch 4c: TRK's last common and uncommon one-offs through real casting — play from exile (an
/// opponent's library, face down, a tax, any type of mana), a destroy replacement, a draw trigger, "can't
/// attack alone", lands entering untapped, a mana restriction for artifacts, the first spell this game, a
/// this-turn trigger on a target, enter as a copy with counters, and Zefram Cochrane's three abilities.
/// </summary>
public class TrkBatch4cTests : FraBoardTest
{
	static CardDefinition Trk(string name) => CardRegistry.Get(name, "TRK");

	static CardDefinition Bear => TestGames.Creature("Bear", "2/2");

	static CardDefinition Sorcery(string cost) => TestGames.Inline("Spell", cost, "Sorcery");

	/// <summary>Answers "you may" yes, and any choice offering one of these ids with it; the rest with their minimum.</summary>
	static Func<ChoiceAction, IEnumerable<int>> Picking(params int[] ids) =>
		c =>
			c switch
			{
				YesNoChoice yes => [yes.Options[0].Id],
				_ => Offered(c, ids),
			};

	static IEnumerable<int> Offered(ChoiceAction c, int[] ids)
	{
		var picked = c.Options.Select(o => o.Id).Where(ids.Contains).Take(c.MaxChoices).ToList();
		return picked.Count > 0 ? picked : c.Options.Take(c.MinChoices).Select(o => o.Id);
	}

	void CastAndResolve(int card, Func<ChoiceAction, IEnumerable<int>>? answer = null)
	{
		s = s.Act(Casts(card).First());
		Resolve(answer);
	}

	/// <summary>Puts a card on top of a player's library.</summary>
	int OnTop(CardDefinition card, int player)
	{
		var id = Hand(card, player);
		s = s.MoveCard(id, s.ZoneId(player, ZoneType.Library), toTop: true);
		return id;
	}

	bool InExile(int card) => s.IsIn(card, s.GetCard(card).OwnerId, ZoneType.Exile);

	bool CanPlay(int card, int player) =>
		s.LegalActions()
			.Any(a =>
				a is CastSpellAction c && c.CardId == card && c.PlayerId == player
				|| a is PlayLandAction l && l.CardId == card && l.PlayerId == player
			);

	// ===== Data, New Life: "Whenever you draw a card, put a +1/+1 counter on Data." =====

	[Test]
	public void Data_ACounterForEachCardYouDraw_NotTheirs()
	{
		var data = Field(Trk("Data, New Life"), me);
		var draw = Hand(
			TestGames.Inline("Divination", "{1}", "Sorcery", null, new DrawCards(Ref.You, 2)),
			me
		);
		Cast(draw, []);
		Assert.That(s.GetCard(data).PlusOneCounters, Nu.EqualTo(2), "one per card drawn");
		var theirs = Hand(
			TestGames.Inline("Peek", "{1}", "Sorcery", null, new DrawCards(Ref.EachOpponent, 1)),
			me
		);
		Cast(theirs, []);
		Assert.That(s.GetCard(data).PlusOneCounters, Nu.EqualTo(2), "their draw isn't yours");
	}

	// ===== Dr. McCoy: "If another creature you control with a +1/+1 counter on it would be destroyed, instead …" =====

	[Test]
	public void McCoy_EntersWithACounterOnAnother_ThatOneSurvivesDestroyLosingItsCounters()
	{
		var bear = Field(Bear, me);
		var mccoy = Hand(Trk("Dr. McCoy, Surly Surgeon"), me);
		CastAndResolve(mccoy, Picking(bear));
		Assert.That(s.GetCard(bear).PlusOneCounters, Nu.EqualTo(1), "a +1/+1 counter as he enters");
		Cast(Hand(TestCards.Kill(), me), [], bear);
		Assert.That(Alive(bear), Nu.True, "destroyed — instead its counters are removed");
		Assert.That(s.GetCard(bear).PlusOneCounters, Nu.EqualTo(0));
		Cast(Hand(TestCards.Kill(), me), [], bear);
		Assert.That(Alive(bear), Nu.False, "no counter left: destroyed");
	}

	[Test]
	public void McCoy_LethalDamageIsHealed_ButNotForHimselfOrAnOpponentsCreature()
	{
		var mccoy = Field(Trk("Dr. McCoy, Surly Surgeon"), me);
		var bear = Field(Bear, me);
		var theirs = Field(Bear, them);
		foreach (var id in new[] { mccoy, bear, theirs })
			s = s.AddPlusOneCounters(id, 1);
		Cast(Hand(TestCards.Shock(3), me), [], bear);
		Assert.That(Alive(bear), Nu.True, "lethal damage destroys — replaced");
		Assert.That(s.GetCard(bear).Damage, Nu.EqualTo(0), "all damage healed");
		Assert.That(s.GetCard(bear).PlusOneCounters, Nu.EqualTo(0));
		Cast(Hand(TestCards.Shock(3), me), [], theirs);
		Assert.That(Alive(theirs), Nu.False, "a creature you control");
		Cast(Hand(TestCards.Shock(3), me), [], mccoy);
		Assert.That(Alive(mccoy), Nu.False, "another creature");
	}

	static CardDefinition Wrath =>
		TestGames.Inline("Wrath", "{1}", "Sorcery", null, new Destroy(Ref.AllCreatures));

	[Test]
	public void DestroyAll_IsSimultaneous_AnIndestructibleGrantDoesntEndHalfway()
	{
		var guardian = Field(
			Bear with
			{
				Components =
				[
					new StaticEffectComponent(
						Is.Creature & Is.Yours & !Is.Source,
						"Indestructible"
					),
				],
			},
			me
		);
		var bear = Field(Bear, me);
		Cast(Hand(Wrath, me), []);
		Assert.That(Alive(guardian), Nu.False);
		Assert.That(Alive(bear), Nu.True, "indestructible as the wrath destroyed them all at once");
	}

	[Test]
	public void McCoy_DestroyedBesideTheCreatureHeSaves_StillSavesIt()
	{
		var mccoy = Field(Trk("Dr. McCoy, Surly Surgeon"), me); // first in the battlefield's order
		var bear = Field(Bear, me);
		s = s.AddPlusOneCounters(bear, 1);
		Cast(Hand(Wrath, me), []);
		Assert.That(Alive(mccoy), Nu.False);
		Assert.That(Alive(bear), Nu.True, "he was there as they were destroyed together");
	}

	// ===== Pet Sehlat: "This creature can't attack alone." =====

	void Declare(params int[] attackers)
	{
		s = TestGames.PassUntil(s, x => x.GetPendingChoice() is DeclareAttackerChoice);
		while (s.GetPendingChoice() is DeclareAttackerChoice declare)
			s = s.Choose([attackers.Contains(declare.CreatureId) ? them : CombatOption.None]);
	}

	[Test]
	public void PetSehlat_DeclaredAlone_DoesntAttack()
	{
		var sehlat = Field(Trk("Pet Sehlat"), me);
		Field(Bear, me);
		Declare(sehlat);
		Assert.That(s.GetCard(sehlat).AttackingId, Nu.EqualTo(0));
		Assert.That(s.GetCard(sehlat).IsTapped, Nu.False);
	}

	[Test]
	public void PetSehlat_AttacksBesideAnother_OrAnotherSehlat()
	{
		var sehlat = Field(Trk("Pet Sehlat"), me);
		var other = Field(Trk("Pet Sehlat"), me);
		Declare(sehlat, other);
		Assert.That(s.GetCard(sehlat).AttackingId, Nu.EqualTo(them));
		Assert.That(s.GetCard(other).AttackingId, Nu.EqualTo(them), "neither is alone");
	}

	// ===== Warp Speed: "Lands you control enter untapped." =====

	[Test]
	public void WarpSpeed_FindsAPlanet_AndItsLandsEnterUntapped()
	{
		var planet = OnTop(Trk("Andoria"), me);
		var warp = Hand(Trk("Warp Speed"), me);
		CastAndResolve(warp, Picking(planet));
		Assert.That(s.IsIn(planet, me, ZoneType.Hand), Nu.True, "a Planet card, searched for");
		Assert.That(s.GetCard(planet).KnownTo, Nu.EqualTo(Knowers.Everyone), "revealed");
		s = s.Act(new PlayLandAction { PlayerId = me, CardId = planet });
		Resolve(); // its "you gain 1 life"
		Assert.That(s.GetCard(planet).IsTapped, Nu.False, "its own 'enters tapped' too");
		var genesis = Hand(
			TestGames.Inline(
				"Genesis",
				"{1}",
				"Sorcery",
				null,
				global::MagicCore.Sets.Trk.TrkCards.CreateNewPlanet
			),
			me
		);
		Cast(genesis, []);
		var token = s.CardsIn(me, ZoneType.Battlefield).Single(c => c.Name == "New Planet");
		Assert.That(token.IsTapped, Nu.False, "a token created tapped");
	}

	[Test]
	public void WarpSpeed_TheirLandsStillEnterTapped()
	{
		Field(Trk("Warp Speed"), me);
		var genesis = Hand(
			TestGames.Inline(
				"Their Genesis",
				"{1}",
				"Sorcery",
				null,
				new CreateTokens(global::MagicCore.Sets.Trk.TrkCards.NewPlanet, 1)
				{
					Tapped = true,
					ControlledBy = Ref.EachOpponent,
				}
			),
			me
		);
		Cast(genesis, []);
		Assert.That(
			s.CardsIn(them, ZoneType.Battlefield).Single(c => c.Name == "New Planet").IsTapped,
			Nu.True
		);
	}

	// ===== Isolinear Processor: "Spend this mana only to cast an artifact spell or activate an ability of an artifact source." =====

	void OnlyTheProcessorsMana()
	{
		foreach (
			var land in s.CardsIn(me, ZoneType.Battlefield).Where(c => c.Is(CardType.Land)).ToList()
		)
			s = s.MoveCard(land.Id, s.ZoneId(me, ZoneType.Graveyard));
		Field(Trk("Isolinear Processor"), me);
	}

	static CardDefinition WithOneManaAbility(CardDefinition card) =>
		card with
		{
			Components =
			[
				.. card.Components,
				new ActivatedAbilityComponent(ManaCost.Parse("{1}"), [new GainLife(Ref.You, 1)]),
			],
		};

	[Test]
	public void IsolinearProcessor_PaysForAnArtifactSpell_NotAnother()
	{
		OnlyTheProcessorsMana();
		var trinket = Hand(TestCards.Trinket, me);
		var sorcery = Hand(Sorcery("{1}"), me);
		Assert.That(Casts(trinket), Nu.Not.Empty, "an artifact spell");
		Assert.That(Casts(sorcery), Nu.Empty, "not a sorcery");
	}

	[Test]
	public void IsolinearProcessor_PaysForAnArtifactsAbility_NotACreatures()
	{
		OnlyTheProcessorsMana();
		var relic = Field(WithOneManaAbility(TestCards.Trinket), me);
		var bear = Field(WithOneManaAbility(Bear), me);
		Assert.That(Activations(relic), Nu.Not.Empty, "an ability of an artifact source");
		Assert.That(Activations(bear), Nu.Empty, "not a creature's");
	}

	// ===== First Contact: "If this spell is the first spell you've cast this game, you gain 2 life." =====

	[Test]
	public void FirstContact_GainsTwoAsTheFirstSpellThisGame_NotTheSecond()
	{
		var life = Life(me);
		Cast(Hand(Trk("First Contact"), me), []);
		Assert.That(Life(me), Nu.EqualTo(life + 2), "the first spell this game");
		Cast(Hand(Trk("First Contact"), me), []);
		Assert.That(Life(me), Nu.EqualTo(life + 2), "the second isn't");
	}

	[Test]
	public void FirstContact_AfterAnotherSpell_NoLife()
	{
		Cast(Hand(Sorcery("{1}"), me), []);
		var life = Life(me);
		Cast(Hand(Trk("First Contact"), me), []);
		Assert.That(Life(me), Nu.EqualTo(life));
	}

	// ===== A Good Day to Die: "When that creature dies this turn, you draw a card." =====

	[Test]
	public void AGoodDayToDie_TheirCreatureDies_IDraw()
	{
		var bear = Field(Bear, them);
		var other = Field(Bear, them);
		Cast(Hand(Trk("A Good Day to Die"), me), [], bear);
		Assert.That(s.Power(s.GetCard(bear)), Nu.EqualTo(4), "+2/+0");
		var (mine, theirs) = (HandCount(me), HandCount(them));
		Cast(Hand(TestCards.Kill(), me), [], other);
		Assert.That(HandCount(me), Nu.EqualTo(mine), "another creature: nothing");
		Cast(Hand(TestCards.Kill(), me), [], bear);
		Assert.That(HandCount(me), Nu.EqualTo(mine + 1), "that creature: I draw");
		Assert.That(HandCount(them), Nu.EqualTo(theirs), "the caster draws, not its controller");
	}

	[Test]
	public void AGoodDayToDie_OnlyThisTurn()
	{
		var bear = Field(Bear, me);
		Cast(Hand(Trk("A Good Day to Die"), me), [], bear);
		var turn = s.GetGame().TurnNumber;
		s = TestGames.PassUntil(
			s,
			x => x.GetGame().TurnNumber == turn + 1 && x.GetGame().Step == Step.PreCombatMain
		);
		var hand = HandCount(me);
		s = s.Act(new PassPriorityAction { PlayerId = s.GetGame().PriorityPlayerId });
		s = s.Destroy([bear]);
		s = TurnStructure.GivePriority(s, s.GetGame().ActivePlayerId);
		Resolve();
		Assert.That(HandCount(me), Nu.EqualTo(hand), "the turn it was cast is over");
	}

	// ===== Sylvia Tilly: "you may pay {1} and exile a card from your graveyard. When you do, put a +1/+1 counter on target creature." =====

	[Test]
	public void SylviaTilly_PaysOneAndExilesAGraveyardCard_ACounterOnTheTarget()
	{
		Field(Trk("Sylvia Tilly, Joy of Discovery"), me);
		var bear = Field(Bear, me);
		int dead;
		(s, dead) = TestGames.Put(s, me, Bear, ZoneType.Graveyard);
		s = TestGames.PassUntil(s, x => x.GetPendingChoice() is YesNoChoice);
		Resolve(Picking(dead, bear));
		Assert.That(InExile(dead), Nu.True, "exiled from your graveyard");
		Assert.That(s.GetCard(bear).PlusOneCounters, Nu.EqualTo(1));
	}

	[Test]
	public void SylviaTilly_AnEmptyGraveyard_NotAsked()
	{
		Field(Trk("Sylvia Tilly, Joy of Discovery"), me);
		foreach (var c in s.CardsIn(me, ZoneType.Graveyard).ToList())
			s = s.MoveCard(c.Id, s.ZoneId(me, ZoneType.Exile));
		var asked = false;
		s = TestGames.PassUntil(
			s,
			x =>
				(asked |= x.GetPendingChoice() is YesNoChoice)
				|| x.GetPendingChoice() is DeclareAttackerChoice
				|| x.GetGame().Step == Step.PostCombatMain
		);
		Assert.That(asked, Nu.False);
	}

	// ===== Dominion Saboteur: a copy of an artifact or creature, not legendary, with its counters =====

	[Test]
	public void DominionSaboteur_CopiesALegendWithCounters_NotLegendary()
	{
		var legend = Field(TestCards.Typed(Bear, "Legendary Creature — Bear"), me);
		s = s.AddPlusOneCounters(legend, 2);
		s = s.PutCounters(legend, CounterKinds.Quest, 1);
		var saboteur = Hand(Trk("Dominion Saboteur"), me);
		CastAndResolve(saboteur, Picking(legend));
		var copy = s.GetCard(saboteur);
		Assert.That(copy.Name, Nu.EqualTo("Bear"));
		Assert.That(copy.Is(Supertype.Legendary), Nu.False, "except it isn't legendary");
		Assert.That(Alive(legend) && Alive(saboteur), Nu.True, "no legend rule");
		Assert.That(copy.PlusOneCounters, Nu.EqualTo(2), "the same number and kinds of counters");
		Assert.That(copy.CountersOf(CounterKinds.Quest), Nu.EqualTo(1));
	}

	[Test]
	public void DominionSaboteur_MayCopyAnArtifact_OrNothing()
	{
		var trinket = Field(TestCards.Trinket, them);
		var saboteur = Hand(Trk("Dominion Saboteur"), me);
		s = s.Act(Casts(saboteur).First());
		s = TestGames.PassUntil(s, x => x.GetPendingChoice() is ChooseCreatureToCopyChoice);
		var choice = (ChooseCreatureToCopyChoice)s.GetPendingChoice()!;
		Assert.That(choice.Options.Select(o => o.Id), Has.Member(trinket), "any artifact");
		Assert.That(choice.MinChoices, Nu.EqualTo(0), "you may");
		s = s.Choose([]);
		Assert.That(s.GetCard(saboteur).Name, Nu.EqualTo("Dominion Saboteur"));
	}

	// ===== Zero, Incorporeal Sensate: exile a nonland card from their hand; its owner may play it for {2} more =====

	[Test]
	public void Zero_ExilesTheirCard_TheyMayCastItForTwoMore()
	{
		var spell = Hand(Sorcery("{1}"), them);
		var zero = Hand(Trk("Zero, Incorporeal Sensate"), me);
		CastAndResolve(zero, Picking(them, spell));
		Assert.That(InExile(spell), Nu.True);
		Assert.That(
			Costs.Of(s, them, s.GetCard(spell)).ManaValue,
			Nu.EqualTo(3),
			"a spell cast this way costs {2} more"
		);
		Assert.That(CanPlay(spell, me), Nu.False, "its owner may, not you");
		s = TestGames.PassUntil(
			s,
			x => x.GetGame().ActivePlayerId == them && x.GetGame().Step == Step.PreCombatMain
		);
		Assert.That(CanPlay(spell, them), Nu.True);
	}

	// ===== Ensign Pavel Chekov: their top X exiled; a land among them you may play =====

	[Test]
	public void Chekov_ExilesTheirTopX_IMayPlayAChosenLand()
	{
		var land = OnTop(TestGames.Card("Forest"), them);
		var spell = OnTop(Sorcery("{1}"), them);
		var library = s.CardsIn(them, ZoneType.Library).Count();
		var chekov = Hand(Trk("Ensign Pavel Chekov"), me);
		CastAndResolve(chekov, Picking(land));
		Assert.That(
			s.CardsIn(them, ZoneType.Library).Count(),
			Nu.EqualTo(library - 5),
			"X: five differently named lands"
		);
		Assert.That(InExile(spell) && InExile(land), Nu.True);
		Assert.That(CanPlay(spell, me), Nu.False, "only the chosen land");
		Assert.That(CanPlay(land, me), Nu.True);
		s = s.Act(new PlayLandAction { PlayerId = me, CardId = land });
		Assert.That(s.IsIn(land, me, ZoneType.Battlefield), Nu.True, "under my control");
	}

	[Test]
	public void Chekov_NoLandExiled_NothingToChoose()
	{
		for (var i = 0; i < 5; i++)
			OnTop(Sorcery("{1}"), them);
		var asked = false;
		CastAndResolve(
			Hand(Trk("Ensign Pavel Chekov"), me),
			c =>
			{
				asked |= c is ChooseAmongChoice;
				return c.Options.Take(c.MinChoices).Select(o => o.Id);
			}
		);
		Assert.That(asked, Nu.False, "no land among them: no empty prompt");
	}

	// ===== Vulcan Mind-Meld: each exiles the other's top cards face down, playable with any type of mana =====

	[Test]
	public void VulcanMindMeld_EachMayPlayWhatTheyExiled_FaceDownToTheOther()
	{
		var mine = OnTop(Sorcery("{1}"), me);
		var theirs = OnTop(Sorcery("{G}{G}"), them);
		Cast(Hand(Trk("Vulcan Mind-Meld"), me), [], them);
		Assert.That(InExile(mine) && InExile(theirs), Nu.True);
		Assert.That(
			s.CardsIn(them, ZoneType.Exile).Count(c => c.IsHiddenFrom(them)),
			Nu.EqualTo(2),
			"their top two, hidden from them"
		);
		Assert.That(s.GetCard(mine).IsHiddenFrom(me), Nu.True, "my top card: I never saw it");
		Assert.That(s.GetCard(mine).IsHiddenFrom(them), Nu.False, "they looked at it");
		Assert.That(s.GetCard(theirs).IsHiddenFrom(me), Nu.False);
		Assert.That(CanPlay(theirs, me), Nu.True);
		Assert.That(CanPlay(mine, me), Nu.False, "the player who exiled it may play it");
		Assert.That(
			Costs.Of(s, me, s.GetCard(theirs)).ToString(),
			Nu.EqualTo(ManaCost.Parse("{2}").ToString()),
			"mana of any type"
		);
		CastAndResolve(theirs);
		Assert.That(s.GetCard(theirs).IsHiddenFrom(them), Nu.False, "cast, it is public");
	}

	// ===== Combadge: "When you cast this spell, copy it." + an unblockable attacker beside another Communicator =====

	[Test]
	public void Combadge_CastIsCopied_TheCopyBecomesAToken()
	{
		CastAndResolve(Hand(Trk("Combadge"), me));
		var badges = s.CardsIn(me, ZoneType.Battlefield).Where(c => c.Name == "Combadge").ToList();
		Assert.That(badges, Has.Count.EqualTo(2));
		Assert.That(badges.Count(c => c.IsToken), Nu.EqualTo(1));
	}

	[Test]
	public void Combadge_AttackingWhileAnotherCreatureHasOne_CantBeBlocked()
	{
		var bear = Field(Bear, me);
		var other = Field(Bear, me);
		var badge = Field(Trk("Combadge"), me);
		s = Attachments.Attach(s, badge, bear);
		Declare(bear);
		Resolve();
		Assert.That(
			s.Has(s.GetCard(bear), Combat.CantBeBlocked),
			Nu.False,
			"no other Communicator"
		);
	}

	[Test]
	public void Combadge_TwoEquipped_TheAttackerCantBeBlocked()
	{
		var bear = Field(Bear, me);
		var other = Field(Bear, me);
		var one = Field(Trk("Combadge"), me);
		var two = Field(Trk("Combadge"), me);
		s = Attachments.Attach(s, one, bear);
		s = Attachments.Attach(s, two, other);
		Declare(bear);
		Resolve();
		Assert.That(s.Has(s.GetCard(bear), Combat.CantBeBlocked), Nu.True);
	}

	// ===== Zefram Cochrane: look at the top, cast an artifact from it once each turn, Spacecraft +2 charge =====

	[Test]
	public void Zefram_CastsAnArtifactFromTheTop_OnceEachTurn()
	{
		Field(Trk("Zefram Cochrane, Warp Pioneer"), me);
		var sorcery = OnTop(Sorcery("{1}"), me);
		Assert.That(CanPlay(sorcery, me), Nu.False, "an artifact spell only");
		s = s.MoveCard(sorcery, s.ZoneId(me, ZoneType.Graveyard));
		var first = OnTop(TestCards.Trinket, me);
		Assert.That(CanPlay(first, me), Nu.True);
		CastAndResolve(first);
		Assert.That(s.IsIn(first, me, ZoneType.Battlefield), Nu.True);
		var second = OnTop(TestCards.Trinket, me);
		Assert.That(CanPlay(second, me), Nu.False, "once during each of your turns");
	}

	[Test]
	public void Zefram_NotDuringTheirTurn()
	{
		Field(Trk("Zefram Cochrane, Warp Pioneer"), me);
		var flash = OnTop(TestGames.Inline("Flash Trinket", "{1}", "Artifact", ["Flash"]), me);
		s = TestGames.PassUntil(
			s,
			x => x.GetGame().ActivePlayerId == them && x.GetGame().Step == Step.PreCombatMain
		);
		s = s.Act(new PassPriorityAction { PlayerId = them });
		Assert.That(CanPlay(flash, me), Nu.False, "during each of YOUR turns");
	}

	[Test]
	public void Zefram_SpacecraftEnterWithTwoMoreChargeCounters()
	{
		Field(Trk("Zefram Cochrane, Warp Pioneer"), me);
		var ship = Hand(TestGames.Inline("Shuttle", "{1}", "Artifact — Spacecraft"), me);
		var trinket = Hand(TestCards.Trinket, me);
		Cast(ship, []);
		Cast(trinket, []);
		Assert.That(s.GetCard(ship).ChargeCounters, Nu.EqualTo(2));
		Assert.That(s.GetCard(trinket).ChargeCounters, Nu.EqualTo(0), "Spacecraft only");
	}

	[Test]
	public void Zefram_TheSearchKnowsMyTopCard()
	{
		var top = s.CardsIn(me, ZoneType.Library).First().Id;
		Field(Trk("Zefram Cochrane, Warp Pioneer"), me);
		var sample = Hidden.Sample(s with { RngSeed = 7 }, me, Hidden.Library.ShuffleUnknown);
		Assert.That(sample.CardsIn(me, ZoneType.Library).First().Id, Nu.EqualTo(top));
	}
}
