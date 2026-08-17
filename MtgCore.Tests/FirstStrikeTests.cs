using System.Collections.Immutable;
using ImmutableGameObjects;
using MtgCore;
using NUnit.Framework;

namespace MtgCore.Tests;

/// <summary>
/// First strike in a no-blocker combat model. Cards are defined inline so card balance changes
/// cannot break these.
/// </summary>
[TestFixture]
public class FirstStrikeTests
{
	private GameState _state;
	private MtgGameIds _ids;

	[SetUp]
	public void Setup() => (_state, _ids) = MtgGameFactory.CreateForTesting();

	[Test]
	public void FirstStrike_KillsDefender_AttackerTakesNoDamage()
	{
		// 2/2 first striker into a 2/2: the defender dies before it can deal its damage back.
		var (s1, attacker) = AddCreature(
			_state,
			"Striker",
			2,
			2,
			_ids.Player1Id,
			firstStrike: true
		);
		var (s2, defender) = AddCreature(s1, "Bear", 2, 2, _ids.Player2Id);

		var (final, _) = s2.AddAction(Attack(attacker.Id, defender.Id)).ProcessAllActions();

		Assert.That(
			final.GetCardZone(defender.Id).ZoneType,
			Is.EqualTo(ZoneType.Graveyard),
			"Defender should die to first strike damage"
		);
		Assert.That(
			final.GetCardZone(attacker.Id).ZoneType,
			Is.EqualTo(ZoneType.Battlefield),
			"First striker should survive"
		);
		Assert.That(
			((Card)final.GetObject(attacker.Id)).GetComponent<CreatureComponent>()!.Damage,
			Is.EqualTo(0),
			"No damage should come back once the defender is dead"
		);
	}

	[Test]
	public void WithoutFirstStrike_SameMatchup_BothDie()
	{
		// The control for the test above — proves the difference is first strike, not the stats.
		var (s1, attacker) = AddCreature(_state, "Bear", 2, 2, _ids.Player1Id);
		var (s2, defender) = AddCreature(s1, "Bear", 2, 2, _ids.Player2Id);

		var (final, _) = s2.AddAction(Attack(attacker.Id, defender.Id)).ProcessAllActions();

		Assert.That(final.GetCardZone(attacker.Id).ZoneType, Is.EqualTo(ZoneType.Graveyard));
		Assert.That(final.GetCardZone(defender.Id).ZoneType, Is.EqualTo(ZoneType.Graveyard));
	}

	[Test]
	public void FirstStrike_DoesNotSaveAttacker_WhenDefenderSurvives()
	{
		// 2/2 first striker into a 2/5: the defender lives, so it strikes back normally.
		var (s1, attacker) = AddCreature(
			_state,
			"Striker",
			2,
			2,
			_ids.Player1Id,
			firstStrike: true
		);
		var (s2, defender) = AddCreature(s1, "Tank", 2, 5, _ids.Player2Id);

		var (final, _) = s2.AddAction(Attack(attacker.Id, defender.Id)).ProcessAllActions();

		Assert.That(
			final.GetCardZone(attacker.Id).ZoneType,
			Is.EqualTo(ZoneType.Graveyard),
			"A surviving defender still kills the first striker"
		);
	}

	[Test]
	public void DefendingFirstStriker_KillsAttackerBeforeItDealsDamage()
	{
		// First strike must be symmetric: attacking into it is punished.
		var (s1, attacker) = AddCreature(_state, "Bear", 2, 2, _ids.Player1Id);
		var (s2, defender) = AddCreature(s1, "Striker", 2, 2, _ids.Player2Id, firstStrike: true);

		var (final, _) = s2.AddAction(Attack(attacker.Id, defender.Id)).ProcessAllActions();

		Assert.That(final.GetCardZone(attacker.Id).ZoneType, Is.EqualTo(ZoneType.Graveyard));
		Assert.That(
			final.GetCardZone(defender.Id).ZoneType,
			Is.EqualTo(ZoneType.Battlefield),
			"Defending first striker should take no damage back"
		);
		Assert.That(
			((Card)final.GetObject(defender.Id)).GetComponent<CreatureComponent>()!.Damage,
			Is.EqualTo(0)
		);
	}

	[Test]
	public void BothFirstStrike_DamageIsSimultaneousAgain()
	{
		var (s1, attacker) = AddCreature(_state, "A", 2, 2, _ids.Player1Id, firstStrike: true);
		var (s2, defender) = AddCreature(s1, "B", 2, 2, _ids.Player2Id, firstStrike: true);

		var (final, _) = s2.AddAction(Attack(attacker.Id, defender.Id)).ProcessAllActions();

		Assert.That(
			final.GetCardZone(attacker.Id).ZoneType,
			Is.EqualTo(ZoneType.Graveyard),
			"Neither side gains an advantage when both strike first"
		);
		Assert.That(final.GetCardZone(defender.Id).ZoneType, Is.EqualTo(ZoneType.Graveyard));
	}

	[Test]
	public void DoubleStrike_ImpliesFirstStrike()
	{
		// A 2/2 double striker beats a 3/4: 4 damage over two strikes kills it, and because
		// double strike implies first strike the 3 power never comes back.
		var (s1, attacker) = AddCreature(
			_state,
			"Double",
			2,
			2,
			_ids.Player1Id,
			doubleStrike: true
		);
		var (s2, defender) = AddCreature(s1, "Giant", 3, 4, _ids.Player2Id);

		var (final, _) = s2.AddAction(Attack(attacker.Id, defender.Id)).ProcessAllActions();

		Assert.That(final.GetCardZone(defender.Id).ZoneType, Is.EqualTo(ZoneType.Graveyard));
		Assert.That(
			final.GetCardZone(attacker.Id).ZoneType,
			Is.EqualTo(ZoneType.Battlefield),
			"Double strike implies first strike, so the 3/4 deals no damage back"
		);
	}

	[Test]
	public void GrantedFirstStrike_IsReadThroughEffectiveStats()
	{
		// Regression: HasDoubleStrike used to be read off the raw component and was absent from
		// CreatureStats, so it could never be granted. Both are now grantable.
		var (s1, attacker) = AddCreature(_state, "Vanilla", 2, 2, _ids.Player1Id);
		var (s2, defender) = AddCreature(s1, "Bear", 2, 2, _ids.Player2Id);

		var card = (Card)s2.GetObject(attacker.Id);
		var granted = s2.UpdateObject(
			attacker.Id,
			card with
			{
				Components = card.Components.Add(
					new AppliedKeywordComponent { GrantsFirstStrike = true }
				),
			}
		);

		Assert.That(granted.GetEffectiveStats(attacker.Id).StrikesFirst, Is.True);

		var (final, _) = granted.AddAction(Attack(attacker.Id, defender.Id)).ProcessAllActions();

		Assert.That(
			final.GetCardZone(attacker.Id).ZoneType,
			Is.EqualTo(ZoneType.Battlefield),
			"A granted first strike must work in combat, not just read true"
		);
	}

	[Test]
	public void GrantedDoubleStrike_DoublesDamage()
	{
		var (s1, attacker) = AddCreature(_state, "Vanilla", 2, 2, _ids.Player1Id);

		var card = (Card)s1.GetObject(attacker.Id);
		var granted = s1.UpdateObject(
			attacker.Id,
			card with
			{
				Components = card.Components.Add(
					new AppliedKeywordComponent { GrantsDoubleStrike = true }
				),
			}
		);

		var (final, _) = granted.AddAction(Attack(attacker.Id, _ids.Player2Id)).ProcessAllActions();

		Assert.That(
			final.GetPlayer(_ids.Player2Id).Life,
			Is.EqualTo(16),
			"Granted double strike should deal 2 power twice"
		);
	}

	// ===== HELPERS =====

	private AttackAction Attack(int attackerId, int targetId) =>
		new()
		{
			AttackerId = attackerId,
			TargetId = targetId,
			AttackingPlayerId = _ids.Player1Id,
		};

	private static (GameState, Card) AddCreature(
		GameState state,
		string name,
		int power,
		int toughness,
		int ownerId,
		bool firstStrike = false,
		bool doubleStrike = false
	)
	{
		var battlefieldId = state.GetPlayerZoneId(ownerId, ZoneType.Battlefield);
		var card = new Card
		{
			Name = name,
			ManaCost = 2,
			OwnerId = ownerId,
			ControllerId = ownerId,
			Components = ImmutableArray.Create<GameComponent>(
				new PermanentComponent(),
				new CreatureComponent
				{
					Power = power,
					Toughness = toughness,
					HasSummoningSickness = false,
					HasFirstStrike = firstStrike,
					HasDoubleStrike = doubleStrike,
				}
			),
		};
		return state.AddObject(card, parentId: battlefieldId);
	}
}
