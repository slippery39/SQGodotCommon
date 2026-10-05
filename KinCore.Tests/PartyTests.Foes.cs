using ImmutableGameObjects;
using KinCore.Party;

namespace KinCore.Tests;

/// <summary>The foes' own rules FIRE (`KinEnemiesPlan.md`). Inline foes; amounts read from the moves.</summary>
public partial class PartyTests
{
	[Test]
	public void ABlastHitsYourWholeLineAndThenItsMakerFalls()
	{
		var blast = Hit(3, Aim.Sweep) with { SelfDestructs = true };
		var s = Deal([new(Mon("A"), 0), new(Mon("B"), 1)], [], Foe(0, pattern: blast), Foe(1));

		s = EndTurn(s);

		Assert.That(Named(s, "A").Hp, Is.EqualTo(20 - blast.Amount));
		Assert.That(Named(s, "B").Hp, Is.EqualTo(20 - blast.Amount));
		Assert.That(s.LivingFoes().Count(), Is.EqualTo(1), "the blaster fell; the other stands");
	}

	// ===== DEBUFFS (`PartyDebuffs`) — on your monsters, turns counted down at the end of your turn

	private static GameState With(GameState s, string name, Func<Ally, Ally> change) =>
		s.UpdateObject(Named(s, name).Id, change(Named(s, name)));

	[Test]
	public void WeakTakesAQuarterOffItsAttacks()
	{
		var s = With(Solo(Mon("A"), [Strike(8)]), "A", a => a with { Weak = 1 });

		s = Play(s, "Strike", 0);

		Assert.That(FoeIn(s, 0).Hp, Is.EqualTo(50 - 8 * 3 / 4));
	}

	[Test]
	public void VulnerableTakesHalfAgain()
	{
		// 2 turns: ending your turn takes one off BEFORE the foes act, so 1 would be gone already.
		var s = With(
			Solo(Mon("A"), [], Foe(0, pattern: Hit(4))),
			"A",
			a => a with { Vulnerable = 2 }
		);

		s = EndTurn(s);

		Assert.That(Named(s, "A").Hp, Is.EqualTo(20 - 4 * 3 / 2));
	}

	[Test]
	public void SilenceTurnsTheFirstAttackBonusOff()
	{
		var bonus = new FirstAttack { Damage = 5 };
		var s = With(Solo(With(Mon("A"), bonus), [Strike(2)]), "A", a => a with { Silenced = 1 });

		s = Play(s, "Strike", 0);

		Assert.That(FoeIn(s, 0).Hp, Is.EqualTo(50 - 2), "no bonus damage");
	}

	[Test]
	public void ShakenRefusesAttackCardsOnIt()
	{
		var s = With(Solo(Mon("A"), [Strike(2)]), "A", a => a with { Shaken = 1 });

		Assert.That(CanPlay(s, "Strike", 0), Is.False);
	}

	[Test]
	public void AMovesRiderAfflictsWhomItHitsAndItCountsDownAfterYourTurn()
	{
		var hex = Hit(1) with { Inflicts = Debuff.Weak, InflictTurns = 2 };
		var s = Solo(Mon("A"), [], Foe(0, pattern: [hex, Idle]));

		s = EndTurn(s);
		Assert.That(Named(s, "A").Weak, Is.EqualTo(hex.InflictTurns), "on for your next turn");

		s = EndTurn(s);
		Assert.That(Named(s, "A").Weak, Is.EqualTo(hex.InflictTurns - 1), "a turn less");
	}

	[Test]
	public void AScreechShakesYourNewFront()
	{
		var screech = new Intent
		{
			Name = "Screech",
			Kind = IntentType.Shove,
			Inflicts = Debuff.Shaken,
			InflictTurns = 1,
		};
		var s = Deal([new(Mon("A"), 0), new(Mon("B"), 1)], [], Foe(0, pattern: screech));

		s = EndTurn(s);

		Assert.That(
			Named(s, "B").Shaken,
			Is.EqualTo(screech.InflictTurns),
			"B was swapped to the front"
		);
		Assert.That(Named(s, "A").Shaken, Is.Zero);
	}

	// ===== JUNK (`PartyJunk`) — added by a foe's move, for the fight only

	private static Intent Curse(Junk junk, int count = 1) =>
		new()
		{
			Name = "Curse",
			Kind = IntentType.Curse,
			AddsJunk = junk,
			JunkCount = count,
		};

	private static int Held(GameState s, string name) =>
		new[] { ZoneType.Draw, ZoneType.Hand, ZoneType.Discard }
			.SelectMany(z => s.CardsIn(z))
			.Count(c => c.Name == name);

	[Test]
	public void ACurseShufflesUnplayableMireIntoYourDeck()
	{
		var curse = Curse(Junk.Mire, 2);
		var s = Solo(Mon("A"), [], Foe(0, pattern: curse));

		s = EndTurn(s);

		Assert.That(Held(s, "Mire"), Is.EqualTo(curse.JunkCount));
		if (s.CardsIn(ZoneType.Hand).Any(c => c.Name == "Mire"))
			Assert.That(CanPlay(s, "Mire", 0), Is.False, "unplayable");
	}

	[Test]
	public void AWebHoldsYourLineUntilYouPayToClearItAndThenItIsGone()
	{
		var s = Deal(
			[new(Mon("A"), 0), new(Mon("B"), 1)],
			[Card("Swap", 0, new SwapAction())],
			Foe(0, pattern: [Curse(Junk.Web), Idle])
		);
		s = EndTurn(s);
		Assert.That(s.CardsIn(ZoneType.Hand).Any(c => c.Name == "Web"), Is.True, "into your hand");
		s = s.CardsIn(ZoneType.Hand).Any(c => c.Name == "Swap")
			? s
			: Do(s, new AddCardsAction { Card = Card("Swap", 0, new SwapAction()) });

		Assert.That(CanPlay(s, "Swap", 1), Is.False, "webbed");
		s = Play(s, "Web", 0);

		Assert.That(Held(s, "Web"), Is.Zero, "cleared for the fight");
		Assert.That(CanPlay(s, "Swap", 1), Is.True);
	}

	[Test]
	public void RotHurtsYourWeakestAsItIsDrawn()
	{
		var draw = new DrawAction { Count = 1 };
		var s = Deal(
			[new(Mon("A", hp: 20), 0), new(Mon("B", hp: 9), 1)],
			[Card("Draw", 0, draw)],
			Foe(0)
		);
		var rot = PartyJunk.CardOf(Junk.Rot);
		(s, var added) = s.AddObject(rot, s.ZoneId(ZoneType.Draw));
		s = s.MoveObjectToFront(added.Id, s.ZoneId(ZoneType.Draw));

		s = Play(s, "Draw", 0);

		Assert.That(Named(s, "B").Hp, Is.EqualTo(9 - rot.GetComponent<HurtsWhenDrawn>()!.Amount));
		Assert.That(Named(s, "A").Hp, Is.EqualTo(20));
	}

	[Test]
	public void AnUnplayedDoubtGoesAndCostsEnergyNextTurn()
	{
		var whisper = Hit(1) with { AddsJunk = Junk.Doubt, JunkCount = 1 };
		var s = Solo(Mon("A"), [], Foe(0, pattern: [whisper, Idle]));
		s = EndTurn(s);
		Assert.That(s.CardsIn(ZoneType.Hand).Count(c => c.Name == "Doubt"), Is.EqualTo(1));

		s = EndTurn(s);

		Assert.That(Held(s, "Doubt"), Is.Zero, "gone at the turn's end");
		Assert.That(
			s.GetParty().Energy,
			Is.EqualTo(
				s.GetParty().MaxEnergy - PartyJunk.CardOf(Junk.Doubt).GetComponent<Fleeting>()!.Debt
			)
		);
	}

	[Test]
	public void JunkIsNeverASpell()
	{
		foreach (var junk in new[] { Junk.Mire, Junk.Web, Junk.Rot, Junk.Doubt })
			Assert.That(PartyJunk.CardOf(junk).IsSpell(), Is.False, junk.ToString());
	}

	[Test]
	public void ASkeletonReassemblesOnceAtHalfItsHp()
	{
		var skeleton = Foe(0, hp: 10) with { Components = [new Reassembles()] };
		var s = Solo(
			Mon("A"),
			[Strike(20), Card("Again", 0, new StrikeAction { Amount = 20 })],
			skeleton,
			Foe(1)
		);

		s = Play(s, "Strike", 0);
		Assert.That(FoeIn(s, 0).Hp, Is.EqualTo(skeleton.MaxHp / 2), "up again, at half");

		s = Play(s, "Again", 0);
		Assert.That(s.LivingFoes().Count(), Is.EqualTo(1), "the second fall is the last");
	}
}
