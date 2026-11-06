using ImmutableGameObjects;
using MagicCore.Actions;
using MagicCore.Cards;
using MagicCore.Effects;
using MagicCore.Sets;
using Nu = NUnit.Framework.Is;

namespace MagicCore.Tests.Sets;

/// <summary>
/// MC-5.3 batch 6b: the re-tier's cheap ten. Tier 3 — Enterprise-D's "charge counters put on it for the
/// first time each turn", Dathon's "another counter of that kind" — and each tier-2 card's new option
/// against its PRINTED text, through real casting. Tier-1 cards (Sela, Antagonist Incorporator, Deanna
/// Troi) are checked by their card-sheet entry (TrkCardSheet.md).
/// </summary>
public class TrkBatch6bTests : FraBoardTest
{
	static CardDefinition Trk(string name) => CardRegistry.Get(name, "TRK");

	static CardDefinition Bear => TestGames.Creature("Bear", "2/2");

	static CardDefinition Forest => CardRegistry.Get("Forest");

	int OnTop(CardDefinition card)
	{
		int id;
		(s, id) = TestGames.Put(s, me, card, ZoneType.Library);
		s = s.MoveObjectToFront(id, s.ZoneId(me, ZoneType.Library)); // TEST SETUP: index 0 is the top
		return id;
	}

	bool InExile(int card) => s.IsIn(card, me, ZoneType.Exile);

	void Activate(ActivateAbilityAction action, Func<ChoiceAction, IEnumerable<int>>? answer = null)
	{
		s = s.Act(action);
		Resolve(answer);
	}

	ActivateAbilityAction Station(int ship, int tapping) =>
		Activations(ship).Single(a => a.TapId == tapping);

	// ===== Tier 3 =====

	[Test]
	public void EnterpriseD_ChargeCountersPut_ExilesTheTop_OnlyTheFirstTimeEachTurn()
	{
		var ship = Field(Trk("U.S.S. Enterprise-D, Galaxy-Class"), me);
		var bear = Field(Bear, me);
		var other = Field(Bear, me);
		var second = OnTop(TestCards.Fodder);
		var first = OnTop(TestCards.Fodder);
		s = s.PutCounters(ship, CounterKinds.Quest, 1); // TEST SETUP: another kind
		Cast(Hand(TestCards.Fodder, me), []); // someone gets priority: the triggers are collected
		Assert.That(InExile(first), Nu.False, "charge counters, not any kind");
		Activate(Station(ship, bear));
		Assert.That(InExile(first), Nu.True, "exile the top card of your library");
		Assert.That(Casts(first), Nu.Not.Empty, "You may play that card this turn.");
		Activate(Station(ship, other));
		Assert.That(s.GetCard(ship).ChargeCounters, Nu.EqualTo(4));
		Assert.That(InExile(second), Nu.False, "for the first time each turn");
	}

	[Test]
	public void Dathon_EnchantmentSpell_Draws_ASagaAlsoDeals2ToEachOpponent()
	{
		Field(Trk("Dathon, His Heart Open"), me);
		var hand = HandCount(me);
		var life = Life(them);
		Cast(Hand(TestGames.Inline("Charm", "{1}", "Enchantment"), me), []);
		Assert.That(
			HandCount(me),
			Nu.EqualTo(hand + 1),
			"Whenever you cast an enchantment spell, draw a card."
		);
		Assert.That(Life(them), Nu.EqualTo(life));
		Cast(Hand(TestGames.Inline("Tale", "{1}", "Enchantment — Saga"), me), []);
		Assert.That(
			Life(them),
			Nu.EqualTo(life - 2),
			"If it's a Saga spell, Dathon deals 2 damage to each opponent."
		);
	}

	[Test]
	public void Dathon_AnotherCounterOfThatKind_OneKindUnasked_SeveralChosen()
	{
		var dathon = Field(Trk("Dathon, His Heart Open"), me);
		var charm = Field(TestGames.Inline("Charm", "{1}", "Enchantment"), me);
		s = s.PutCounters(charm, CounterKinds.Quest, 1); // TEST SETUP
		Activate(
			Activations(dathon).Single(),
			c =>
				c switch
				{
					ChooseKeywordChoice => throw new AssertionException(
						"one kind: nothing to choose"
					),
					_ => [charm],
				}
		);
		Assert.That(s.GetCard(charm).CountersOf(CounterKinds.Quest), Nu.EqualTo(2));
		s = s.PutCounters(charm, CounterKinds.Charge, 1); // TEST SETUP
		Activate(
			Activations(dathon).Single(),
			c =>
				c switch
				{
					ChooseKeywordChoice k =>
					[
						k.Options.Single(o => o.DisplayText == CounterKinds.Quest).Id,
					],
					_ => [charm],
				}
		);
		Assert.That(
			s.GetCard(charm).CountersOf(CounterKinds.Quest),
			Nu.EqualTo(3),
			"Put another counter of that kind on it."
		);
		Assert.That(s.GetCard(charm).ChargeCounters, Nu.EqualTo(1));
	}

	[Test]
	public void MrSpock_DrawsTwoInstead()
	{
		Field(Trk("Mr. Spock, Science Officer"), me);
		var hand = HandCount(me);
		Cast(
			Hand(
				TestGames.Inline("Insight", "{1}", "Sorcery", null, new DrawCards(Ref.You, 1)),
				me
			),
			[]
		);
		Assert.That(
			HandCount(me),
			Nu.EqualTo(hand + 2),
			"If you would draw a card, draw two cards instead."
		); // counted before the spell was in hand
	}

	[Test]
	public void Georgiou_At0Life_DoesntLose_TenOfficers()
	{
		Field(Trk("Georgiou, Terran Emperor"), me);
		s = s.UpdateObject(me, s.GetPlayer(me) with { Life = 2 }); // TEST SETUP
		s = TestGames.PassUntil(s, x => x.GetGame().Step == Step.End);
		Resolve();
		Assert.That(Life(me), Nu.EqualTo(-1), "you lose 3 life");
		Assert.That(s.IsOver(), Nu.False, "You don't lose the game for having 0 or less life.");
		Assert.That(
			s.CardsIn(me, ZoneType.Battlefield).Count(c => c.Name == "Officer"),
			Nu.EqualTo(10),
			"Then if your life total is 0 or less, create ten 1/1 red Officer creature tokens."
		);
	}

	[Test]
	public void MilesOBrien_APermanentReturnedToYourHand_DrawsAtTheEndStep()
	{
		var bear = Field(Bear, me);
		CastWithTrigger(Hand(Trk("Miles O'Brien, Transporter Ops"), me), [], bear);
		Assert.That(s.IsIn(bear, me, ZoneType.Hand), Nu.True);
		var hand = HandCount(me);
		s = TestGames.PassUntil(s, x => x.GetGame().Step == Step.End);
		Resolve();
		Assert.That(
			HandCount(me),
			Nu.EqualTo(hand + 1),
			"if a permanent was returned to your hand from the battlefield this turn, draw a card"
		);
	}

	// ===== Tier 2: a new option each =====

	[Test]
	public void WillRiker_TwoLandsOntoTheBattlefield_SacrificedAtYourNextEndStep()
	{
		Field(Trk("Will Riker, First in the Field"), me);
		var second = OnTop(Forest);
		OnTop(TestCards.Fodder);
		var first = OnTop(Forest);
		s = TestGames.PassUntil(s, x => x.GetGame().Step == Step.BeginCombat);
		Resolve();
		Assert.That(Alive(first) && Alive(second), Nu.True, "until you reveal two land cards");
		var turn = s.GetGame().TurnNumber;
		s = TestGames.PassUntil(s, x => x.GetGame().TurnNumber == turn + 1);
		Assert.That(
			Alive(first) || Alive(second),
			Nu.False,
			"Sacrifice those lands at the beginning of your next end step."
		);
	}

	[Test]
	public void CaptainPike_PlaysALandFromTheTopOfYourLibrary()
	{
		Field(Trk("Captain Pike, Fullness of Time"), me);
		var forest = OnTop(Forest);
		s = s.Act(new PlayLandAction { PlayerId = me, CardId = forest });
		Assert.That(Alive(forest), Nu.True, "You may play lands from the top of your library.");
	}

	[Test]
	public void MBenga_CountersOnYourCreatureDoubled_AndItHasHexproof()
	{
		Field(Trk("Dr. Joseph M'Benga"), me);
		var bear = Field(Bear, me);
		s = s.PutCounters(bear, CounterKinds.PlusOne, 1);
		Assert.That(s.GetCard(bear).PlusOneCounters, Nu.EqualTo(2), "put twice that many");
		Assert.That(
			s.Has(s.GetCard(bear), "Hexproof"),
			Nu.True,
			"Other creatures you control with counters on them have hexproof."
		);
	}

	[Test]
	public void TheInnerLight_ChapterII_DrawsTheCombatDamageItDeals()
	{
		var bear = Field(Bear, me);
		var other = Field(Bear, me); // attacks too: only the target's damage draws
		Cast(Hand(Trk("The Inner Light"), me), []);
		var turn = s.GetGame().TurnNumber;
		s = TestGames.PassUntil(
			s,
			x => x.GetGame().TurnNumber == turn + 2 && x.GetGame().Step == Step.PreCombatMain
		);
		Resolve(c =>
			c switch
			{
				ChooseTargetChoice => [bear],
				_ => c.Options.Take(c.MinChoices).Select(o => o.Id),
			}
		);
		var hand = HandCount(me);
		s = TestGames.PassUntil(s, x => x.IsWaitingForChoice);
		while (s.GetPendingChoice() is DeclareAttackerChoice declare)
			s = s.Choose([them]);
		s = TestGames.PassUntil(s, x => x.GetGame().Step == Step.PostCombatMain);
		Assert.That(HandCount(me), Nu.EqualTo(hand + 2), "you draw that many cards");
	}

	[Test]
	public void DeepSpace9_EntersExilingFour_PermanentsOntoTheBattlefield_AnthemAt9()
	{
		var trinket = OnTop(TestCards.Trinket);
		var fodder = OnTop(TestCards.Fodder);
		var forest = OnTop(Forest);
		var bear = OnTop(Bear);
		var ds9 = Hand(Trk("Deep Space 9"), me);
		Cast(ds9, []);
		Assert.That(
			Alive(bear) && Alive(forest) && Alive(trinket),
			Nu.True,
			"Put all permanent cards exiled this way onto the battlefield."
		);
		Assert.That(InExile(fodder), Nu.True, "the rest stay exiled");
		s = s.UpdateObject(ds9, s.GetCard(ds9) with { ChargeCounters = 9 }); // TEST SETUP
		Assert.That(
			s.Power(s.GetCard(bear)),
			Nu.EqualTo(5),
			"9+ | Creatures you control get +3/+3."
		);
		Assert.That(s.GetCard(ds9).Is(CardType.Creature), Nu.False, "never a creature");
	}
}
