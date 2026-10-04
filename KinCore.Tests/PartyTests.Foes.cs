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
}
