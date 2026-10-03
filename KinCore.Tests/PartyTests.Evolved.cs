using KinCore.Party;

namespace KinCore.Tests;

/// <summary>
/// **The evolved forms' new passives FIRE** (`KinFamiliesPlan.md`, round 5). Inline monsters and
/// foes; amounts read from the components, never restated.
/// </summary>
public partial class PartyTests
{
	[Test]
	public void ReachHitsTheFrontTwo()
	{
		var s = Solo(With(Mon("L"), new Reach()), [Strike(5)], Foe(0), Foe(1));

		s = Play(s, "Strike", 0);

		Assert.That(FoeIn(s, 0).Hp, Is.EqualTo(50 - 5));
		Assert.That(FoeIn(s, 1).Hp, Is.EqualTo(50 - 5));
	}

	[Test]
	public void ShelteredGivesSpellPowerOnlyAwayFromTheFront()
	{
		var sheltered = new Sheltered();
		var back = Deal([new(Mon("A"), 0), new(With(Mon("F"), sheltered), 1)], [], Foe(0));
		var front = Deal([new(With(Mon("F"), sheltered), 0), new(Mon("A"), 1)], [], Foe(0));

		Assert.That(back.SpellBonus(), Is.EqualTo(sheltered.Amount));
		Assert.That(front.SpellBonus(), Is.Zero);
	}

	[Test]
	public void CinderfallPassesAFallenFoesBurnBack()
	{
		var burning = Foe(0, hp: 5) with { Burn = 4 };
		var s = Solo(With(Mon("C"), new Cinderfall()), [Strike(10)], burning, Foe(1));

		s = Play(s, "Strike", 0);

		Assert.That(s.LivingFoes().Single().Burn, Is.EqualTo(burning.Burn));
	}

	[Test]
	public void CadenceMakesTheNthSpellOfTheTurnFree()
	{
		var cadence = new Cadence();
		KinCard Spell(string name) => Card(name, 1, new GuardAction { Amount = 1 });
		var s = Solo(With(Mon("E"), cadence), [Spell("A"), Spell("B"), Spell("C")]);

		for (var i = 0; i < cadence.Nth - 1; i++)
		{
			Assert.That(s.CostOf(InHand(s, "C")), Is.EqualTo(1), $"spell {i + 1} is not free");
			s = Play(s, i == 0 ? "A" : "B", 0);
		}

		Assert.That(s.CostOf(InHand(s, "C")), Is.Zero);
	}

	[Test]
	public void GuardianSoftensBlowsOnTheMonstersBehindHerOnly()
	{
		var guardian = new Guardian();
		var s = Deal(
			[new(With(Mon("G"), guardian), 0), new(Mon("B"), 1)],
			[],
			Foe(0, pattern: Hit(5, Aim.Sweep))
		);

		s = EndTurn(s);

		Assert.That(Named(s, "B").Hp, Is.EqualTo(20 - (5 - guardian.Amount)), "behind her");
		Assert.That(Named(s, "G").Hp, Is.EqualTo(20 - 5), "not herself");
	}

	[Test]
	public void ShellstrikeAddsHalfItsBlockWithoutSpendingIt()
	{
		var guard = new GuardAction { Amount = 8 };
		var s = Solo(With(Mon("M"), new Shellstrike()), [Card("Shell Up", 0, guard), Strike(2)]);

		s = Play(Play(s, "Shell Up", 0), "Strike", 0);

		Assert.That(FoeIn(s, 0).Hp, Is.EqualTo(50 - (2 + guard.Amount / 2)));
		Assert.That(Named(s, "M").Block, Is.EqualTo(guard.Amount), "read, never spent");
	}

	[Test]
	public void SporeCloudStingsAFoeThatAttacksYourLineOnce()
	{
		var lord = With(Mon("S"), new SporeCloud()) with { Thorns = 3 };
		var atFront = Deal([new(Mon("A"), 0), new(lord, 1)], [], Foe(0, pattern: Hit(2)));
		var atLord = Deal([new(Mon("A"), 0), new(lord, 1)], [], Foe(0, pattern: Hit(2, Aim.Back)));

		atFront = EndTurn(atFront);
		atLord = EndTurn(atLord);

		Assert.That(FoeIn(atFront, 0).Hp, Is.EqualTo(50 - lord.Thorns), "a hit on the line");
		Assert.That(FoeIn(atLord, 0).Hp, Is.EqualTo(50 - lord.Thorns), "once, not twice");
	}

	[Test]
	public void SeedfallDrawsWhenATokenFalls()
	{
		var seedfall = new Seedfall();
		var s = Deal(
			[new(With(Mon("B"), seedfall), 0)],
			[Summon(Token(hp: 3)), Card("Offer", 0, new SacrificeTokenAction())],
			Foe(0)
		);
		s = Play(s, "Summon", 0);
		var hand = s.CardsIn(ZoneType.Hand).Count();

		s = Play(s, "Offer", 0);

		Assert.That(s.CardsIn(ZoneType.Hand).Count(), Is.EqualTo(hand - 1 + seedfall.Count));
	}

	[Test]
	public void HuntCallSendsTheTokensInOnItsFirstAttackOnly()
	{
		var tok = new TokenTemplate(Mon("Tok", hp: 9, power: 2), 3);
		var s = Deal(
			[new(With(Mon("H"), new HuntCall()), 0)],
			[Summon(tok), Strike(1), Card("Again", 0, new StrikeAction { Amount = 1 })],
			Foe(0)
		);
		s = Play(s, "Summon", 0);

		s = Play(s, "Strike", Named(s, "H").Position);
		Assert.That(FoeIn(s, 0).Hp, Is.EqualTo(50 - 1 - tok.Creature.Power));

		s = Play(s, "Again", Named(s, "H").Position);
		Assert.That(FoeIn(s, 0).Hp, Is.EqualTo(50 - 1 - tok.Creature.Power - 1), "first only");
	}

	[Test]
	public void AFirstAttackCanSummonSeveral()
	{
		var bonus = new FirstAttack { Summons = Token(), SummonCount = 2 };
		var s = Solo(With(Mon("B"), bonus), [Strike(1)]);

		s = Play(s, "Strike", 0);

		Assert.That(s.LivingAllies().Count(a => a.IsToken), Is.EqualTo(bonus.SummonCount));
	}
}
