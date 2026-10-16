using ImmutableGameObjects;
using MagicCore.Ai;

namespace MagicCore.Tests.Ai;

/// <summary>How a held card is priced (AI-1): an inline table, so the rules are tested, not FRA's numbers.</summary>
public class HeldValuesTests
{
	static readonly HeldValues Held =
		new(
			new Dictionary<string, CardValues.Entry>
			{
				["Bomb"] = new("Bomb", 10),
				["Weak"] = new("Weak", 1),
				["Odd"] = new("Odd", 0, "can't be cast in the fixture"),
			},
			fraction: 0.5
		);

	[Test]
	public void OurCard_IsAFractionOfItsMeasuredValue() =>
		Assert.That(Held.Of("Bomb"), Is.EqualTo(0.5 * 10));

	// At 0 the search, whose ties go to acting, would throw a zero-valued trick away.
	[Test]
	public void OurCard_IsNeverBelowTheFlatPrice()
	{
		Assert.That(Held.Of("Weak"), Is.EqualTo(Evaluator.FlatHandCard));
		Assert.That(Held.Of("Odd"), Is.EqualTo(Evaluator.FlatHandCard));
		Assert.That(Held.Of("Not in the table"), Is.EqualTo(Evaluator.FlatHandCard));
	}

	// The evaluator reads their hand blind: a land there weighs exactly what a spell does.
	[Test]
	public void TheirHand_IsBlind()
	{
		var s = TestGames.EmptyHands(TestGames.InFirstMain());
		var me = s.GetGame().StartingPlayerId;
		var them = s.OpponentOf(me);

		var (withLand, _) = TestGames.Put(s, them, TestGames.Card("Plains"), ZoneType.Hand);
		var (withSpell, _) = TestGames.Put(
			s,
			them,
			TestGames.Creature("Spell", "1/1"),
			ZoneType.Hand
		);

		Assert.That(
			Evaluator.Evaluate(withLand, me, Held),
			Is.EqualTo(Evaluator.Evaluate(withSpell, me, Held))
		);
	}

	// Their hand is hidden: every card, land or spell, gets one blind price — a limited deck's share of
	// lands at the land price, the rest at the average held value of a measured card.
	[Test]
	public void TheirCard_IsOneBlindPrice() =>
		Assert.That(
			Held.Unknown,
			Is.EqualTo(
				HeldValues.LandShare * Evaluator.LandInHand
					+ (1 - HeldValues.LandShare) * (Held.Of("Bomb") + Held.Of("Weak")) / 2
			)
		);
}
