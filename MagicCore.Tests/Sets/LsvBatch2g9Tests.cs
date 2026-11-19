using ImmutableGameObjects;
using MagicCore.Actions;
using MagicCore.Cards;
using MagicCore.Effects;
using MagicCore.Rules;
using MagicCore.Sets;
using Nu = NUnit.Framework.Is;

namespace MagicCore.Tests.Sets;

/// <summary>
/// MC-4.4g9, LSVPower — warp: Quantum Riddler, FIRING against its printed text through real casting and the real stack.
/// </summary>
public class LsvBatch2g9Tests : FraBoardTest
{
	static CardDefinition Peek =>
		TestGames.Inline("Peek", "{1}", "Instant", null, new DrawCards(Ref.You, 1));

	/// <summary>I cast the Riddler from my hand — for its warp cost or its mana cost — and the stack resolves.</summary>
	int CastRiddler(bool warp)
	{
		var riddler = Hand(Lsv("Quantum Riddler"), me);
		s = s.Act(Casts(riddler).Single(c => c.WithoutPayingManaCost == warp));
		Resolve();
		return riddler;
	}

	/// <summary>TEST SETUP: my hand cut to its first n cards.</summary>
	void KeepInHand(int n)
	{
		foreach (var card in s.CardsIn(me, ZoneType.Hand).Skip(n).ToList())
			s = s.MoveCard(card.Id, s.ZoneId(me, ZoneType.Graveyard));
	}

	void ToMyNextMain()
	{
		var turn = s.GetGame().TurnNumber;
		s = TestGames.PassUntil(
			s,
			x =>
				x.GetGame().TurnNumber > turn
				&& x.GetGame().ActivePlayerId == me
				&& x.GetGame().Step == Step.PreCombatMain
		);
	}

	[Test]
	public void Warp_ForOneAndBlue_ExiledAtTheNextEndStep_ThenCastFromExileOnALaterTurn()
	{
		var lands = UntappedLands();
		var riddler = CastRiddler(warp: true);
		Assert.That(
			(Alive(riddler), lands - UntappedLands()),
			Nu.EqualTo((true, 2)),
			"cast for {1}{U}"
		);

		s = TestGames.PassUntil(s, x => x.GetGame().Step == Step.Cleanup || InExile(riddler));
		Assert.That(
			(InExile(riddler), Playable.IsImpulsePlayableBy(s, s.GetCard(riddler), me)),
			Nu.EqualTo((true, false)),
			"exiled at the end step — not castable this turn"
		);

		ToMyNextMain();
		Assert.That(
			Casts(riddler).Select(c => c.WithoutPayingManaCost),
			Nu.EqualTo(new[] { false }),
			"castable from exile on a later turn, for its mana cost only: warp is from the hand"
		);
	}

	[Test]
	public void Warp_ItsExileIsMine_EvenOnceTheyControlIt()
	{
		var riddler = CastRiddler(warp: true);
		s = s.GainControl(riddler, them); // TEST SETUP: stolen before the end step

		s = TestGames.PassUntil(
			s,
			x => x.GetChildren(x.GetWellKnownId(MagicKeys.Stack)).FirstOrDefault() is StackAbility
		);

		Assert.That(
			((StackAbility)s.GetChildren(s.GetWellKnownId(MagicKeys.Stack)).First()).ControllerId,
			Nu.EqualTo(me),
			"the delayed trigger is its creator's (CR 603.7d)"
		);
	}

	[Test]
	public void Warp_NotFromTheirHand_UnderSenTriplets()
	{
		Field(CardRegistry.Get("Sen Triplets"), me);
		ToMyNextMain();
		var riddler = Hand(Lsv("Quantum Riddler"), them);

		Assert.That(
			Casts(riddler).Select(c => c.WithoutPayingManaCost),
			Nu.EqualTo(new[] { false }),
			"cast from their hand, for its mana cost: warp is from YOUR hand"
		);
	}

	[Test]
	public void PaidItsManaCost_ItStays()
	{
		var riddler = CastRiddler(warp: false);

		ToMyNextMain();

		Assert.That(Alive(riddler), Nu.True);
	}

	[Test]
	public void OneOrFewerCardsInHand_ADrawIsThatManyPlusOne()
	{
		KeepInHand(0);
		CastRiddler(warp: false); // its own draw: an empty hand, two cards
		Assert.That(HandCount(me), Nu.EqualTo(2), "the enters draw, plus one");

		Cast(Hand(Peek, me), []);
		Assert.That(HandCount(me), Nu.EqualTo(3), "two cards in hand: one draw");

		KeepInHand(1);
		Cast(Hand(Peek, me), []);
		Assert.That(HandCount(me), Nu.EqualTo(3), "one card in hand: two draws");
	}
}
