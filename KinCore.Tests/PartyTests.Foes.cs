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
}
