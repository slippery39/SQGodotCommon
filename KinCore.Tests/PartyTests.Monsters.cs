using ImmutableGameObjects;
using KinCore.Party;

namespace KinCore.Tests;

/// <summary>
/// **MONSTERS act only through cards — every round-4 rule FIRES** (`KinFamiliesPlan.md`, round 4): the
/// FIRST-ATTACK bonus (first attack on each monster each turn), and SPELL POWER summed across the team.
/// Inline monsters only.
/// </summary>
public partial class PartyTests
{
	private static KinCard Strike(int amount = 3) =>
		Card("Strike", 0, new StrikeAction { Amount = amount });

	[Test]
	public void TheFirstAttackOnAMonsterEachTurnFiresItsBonusAndOnlyTheFirst()
	{
		var bonus = new FirstAttack { Damage = 4, Block = 5 };
		var s = Deal(
			[new(Mon("Pike") with { Abilities = [bonus] }, 0)],
			[Strike(), Strike()],
			Foe(0)
		);

		s = Play(s, "Strike", 0);
		Assert.That(FoeIn(s, 0).Hp, Is.EqualTo(50 - (3 + bonus.Damage)), "the bonus damage");
		Assert.That(Named(s, "Pike").Block, Is.EqualTo(bonus.Block), "and its Block");

		s = Play(s, "Strike", 0);
		Assert.That(FoeIn(s, 0).Hp, Is.EqualTo(50 - (3 + bonus.Damage) - 3), "the second: none");
	}

	[Test]
	public void EachMonsterHasItsOwnBonusAndItIsBackNextTurn()
	{
		var bonus = new FirstAttack { Damage = 4 };
		var s = Deal(
			[
				new(Mon("Front") with { Abilities = [bonus] }, 0),
				new(Mon("Back") with { Abilities = [bonus] }, 1),
			],
			[Strike(), Strike()],
			Foe(0, hp: 90)
		);

		s = Play(s, "Strike", 0);
		s = Play(s, "Strike", 1);
		Assert.That(FoeIn(s, 0).Hp, Is.EqualTo(90 - 2 * (3 + bonus.Damage)), "one each");

		s = EndTurn(s);
		Assert.That(PartyMonsters.BonusReady(Named(s, "Front")), Is.True, "ready again");
	}

	[Test]
	public void SpellPowerIsTheTeamsTotalAndAFirstAttackCanAddToItThisTurn()
	{
		var s = Deal(
			[
				new(Mon("Owl") with { SpellPower = 1 }, 0),
				new(
					Mon("Newt") with
					{
						SpellPower = 2,
						Abilities = [new FirstAttack { SpellPower = 3 }],
					},
					1
				),
			],
			[Zap(), Strike(0), Zap()],
			Foe(0, hp: 90)
		);

		s = Play(s, "Zap", 0, foeRow: true);
		Assert.That(FoeIn(s, 0).Hp, Is.EqualTo(90 - (4 + 1 + 2)), "the team's Spell Power");

		s = Play(s, "Strike", 1);
		s = Play(s, "Zap", 0, foeRow: true);
		Assert.That(FoeIn(s, 0).Hp, Is.EqualTo(90 - 7 - (4 + 1 + 2 + 3)), "and the bonus's");

		s = EndTurn(s);
		Assert.That(s.GetParty().TurnSpellPower, Is.Zero, "only for that turn");
	}
}
