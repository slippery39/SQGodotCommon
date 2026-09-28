using ImmutableGameObjects;
using KinCore.Party;

namespace KinCore.Tests;

/// <summary>
/// **BOSSES AND ELITES — every mechanic FIRES** (`PartyBosses`; `KinFamiliesPlan.md`, round 3): the
/// wind-up, the Tongue, Shell, Enrage, a phase, and a minion that stays. Inline foes only.
/// </summary>
public partial class PartyTests
{
	private static Intent WindUp => new() { Name = "Wind Up", Kind = IntentType.WindUp };

	private static Intent Tongue => new() { Name = "Tongue", Kind = IntentType.Pull };

	[Test]
	public void AWindUpDoesNothingAndItsNextMoveLands()
	{
		var s = Deal([new(Mon("Wall", hp: 40), 0)], [], Foe(0, pattern: [WindUp, Hit(12)]));

		s = EndTurn(s);
		Assert.That(Named(s, "Wall").Hp, Is.EqualTo(40), "the wind-up turn is free");
		Assert.That(FoeIn(s, 0).Current.Amount, Is.EqualTo(12), "and the blow is telegraphed now");

		s = EndTurn(s);
		Assert.That(Named(s, "Wall").Hp, Is.EqualTo(40 - 12));
	}

	[Test]
	public void TheTongueDragsYourBackMonsterToTheFront()
	{
		var s = Deal(
			[new(Mon("Front"), 0), new(Mon("Mid"), 1), new(Mon("Back"), 2)],
			[],
			Foe(0, pattern: Tongue)
		);

		s = EndTurn(s);

		Assert.That(Line(s), Is.EqualTo("Back,Front,Mid"));
	}

	[Test]
	public void ShellIgnoresSmallHitsButNotBigOnes()
	{
		var shell = new Shell();
		var s = Deal(
			[new(Mon("Pike"), 0)],
			[Zap(shell.AtMost), Zap(shell.AtMost + 1)],
			Foe(0, hp: 30) with
			{
				Components = [shell],
			}
		);

		s = Play(s, "Zap", 0, foeRow: true);
		Assert.That(FoeIn(s, 0).Hp, Is.EqualTo(30), "a hit of Shell's size bounces");

		// The second Zap carries the first's Kindle too; whatever it deals, it gets through.
		s = Play(s, "Zap", 0, foeRow: true);
		Assert.That(FoeIn(s, 0).Hp, Is.LessThan(30));
	}

	[Test]
	public void EnrageSharpensEveryAttackEachRound()
	{
		var rage = new Enrage();
		var s = Deal(
			[new(Mon("Wall", hp: 90), 0)],
			[],
			Foe(0, pattern: Hit(5)) with
			{
				Components = [rage],
			}
		);

		s = EndTurn(s);
		s = EndTurn(s);

		Assert.That(Named(s, "Wall").Hp, Is.EqualTo(90 - 5 - (5 + rage.PerRound)));
		Assert.That(FoeIn(s, 0).Current.Amount, Is.EqualTo(5 + 2 * rage.PerRound), "telegraphed");
	}

	[Test]
	public void APhaseFiresOnceWhenAHitCrossesIt()
	{
		var phase = new Phase
		{
			Name = "Second Wind",
			Block = 20,
			Pattern = [Hit(30)],
		};
		var s = Deal(
			[new(Mon("Pike"), 0)],
			[Zap(26), Zap(1)],
			Foe(0, hp: 50, Idle) with
			{
				Components = [phase],
			}
		);

		s = Play(s, "Zap", 0, foeRow: true);

		var foe = FoeIn(s, 0);
		Assert.That(foe.Hp, Is.EqualTo(50 - 26));
		Assert.That(foe.Block, Is.EqualTo(phase.Block));
		Assert.That(foe.Current.Amount, Is.EqualTo(30), "its second pattern");
		Assert.That(foe.HasComponent<Phase>(), Is.False, "spent");
	}

	[Test]
	public void ABossesMinionStaysUntilItIsBeaten()
	{
		var band = new TokenTemplate(Mon("Goblin", hp: 8, moves: Hit(3)), FadesIn: 0);
		var call = new Intent
		{
			Name = "Call the Band",
			Kind = IntentType.Summon,
			Summons = band,
		};
		var s = Deal([new(Mon("Wall", hp: 90), 0)], [], Foe(0, pattern: [call, Idle]));

		s = EndTurn(s);
		s = EndTurn(s);
		s = EndTurn(s);

		Assert.That(
			s.LivingFoes().Count(f => f.Name == "Goblin"),
			Is.EqualTo(2),
			"two calls, and neither faded"
		);
	}
}
