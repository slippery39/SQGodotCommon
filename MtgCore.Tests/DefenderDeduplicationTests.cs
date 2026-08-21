using System.Collections.Immutable;
using ImmutableGameObjects;
using MtgCore;
using NUnit.Framework;

namespace MtgCore.Tests;

/// <summary>
/// Attack actions multiply across two dimensions — attacker x target — and collapsing only
/// the attacker half still leaves (signatures x every individual token) actions for the AI to
/// score. Attacking one of twenty identical Goblin tokens is the same decision twenty times.
///
/// The dedup must stay a search optimisation only: a human has to be able to click any
/// individual creature, which is what deduplicateAttackers: false is for.
/// </summary>
[TestFixture]
public class DefenderDeduplicationTests
{
	private GameState _state;
	private MtgGameIds _ids;

	[SetUp]
	public void Setup() => (_state, _ids) = MtgGameFactory.CreateForTesting();

	private static List<AttackAction> Attacks(GameState state, MtgGameIds ids, bool dedup = true) =>
		MtgActionGenerator
			.GetLegalActions(state, ids, ids.Player1Id, deduplicateAttackers: dedup)
			.OfType<AttackAction>()
			.ToList();

	private GameState AddCreature(
		GameState state,
		string name,
		int power,
		int toughness,
		int ownerId,
		int damage = 0,
		bool taunt = false
	)
	{
		var battlefieldId = state.GetPlayerZoneId(ownerId, ZoneType.Battlefield);
		var (newState, _) = state.AddObject(
			new Card
			{
				Name = name,
				OwnerId = ownerId,
				ControllerId = ownerId,
				Components = ImmutableArray.Create<GameComponent>(
					new CreatureComponent
					{
						Power = power,
						Toughness = toughness,
						Damage = damage,
						HasTaunt = taunt,
						HasSummoningSickness = false,
					}
				),
			},
			parentId: battlefieldId
		);
		return newState;
	}

	[Test]
	public void TwentyIdenticalTokensCollapseToOneTarget()
	{
		var state = AddCreature(_state, "Bear", 2, 2, _ids.Player1Id);
		for (var i = 0; i < 20; i++)
			state = AddCreature(state, "Goblin", 1, 1, _ids.Player2Id);

		// One attacker: the opponent player, plus a single Goblin representative.
		Assert.That(Attacks(state, _ids).Count, Is.EqualTo(2));
	}

	[Test]
	public void ADamagedTokenIsNotInterchangeable()
	{
		var state = AddCreature(_state, "Bear", 2, 2, _ids.Player1Id);
		for (var i = 0; i < 20; i++)
			state = AddCreature(state, "Goblin", 1, 1, _ids.Player2Id);
		state = AddCreature(state, "Goblin", 1, 1, _ids.Player2Id, damage: 1);

		// Player + undamaged Goblin + damaged Goblin: killing the damaged one is a different play.
		Assert.That(Attacks(state, _ids).Count, Is.EqualTo(3));
	}

	[Test]
	public void ATauntTokenIsNotInterchangeable()
	{
		var state = AddCreature(_state, "Bear", 2, 2, _ids.Player1Id);
		state = AddCreature(state, "Goblin", 1, 1, _ids.Player2Id);
		state = AddCreature(state, "Goblin", 1, 1, _ids.Player2Id);
		state = AddCreature(state, "Goblin", 1, 1, _ids.Player2Id, taunt: true);

		// Taunt forces the attack, so only the Taunt Goblin is a legal target at all.
		var attacks = Attacks(state, _ids);
		Assert.That(attacks.Count, Is.EqualTo(1));
	}

	[Test]
	public void DifferentNamesAreNeverCollapsed()
	{
		var state = AddCreature(_state, "Bear", 2, 2, _ids.Player1Id);
		state = AddCreature(state, "Goblin", 1, 1, _ids.Player2Id);
		state = AddCreature(state, "Spirit", 1, 1, _ids.Player2Id);

		Assert.That(Attacks(state, _ids).Count, Is.EqualTo(3));
	}

	[Test]
	public void HumanPathKeepsEveryIndividualTarget()
	{
		var state = AddCreature(_state, "Bear", 2, 2, _ids.Player1Id);
		for (var i = 0; i < 20; i++)
			state = AddCreature(state, "Goblin", 1, 1, _ids.Player2Id);

		// A human must be able to click any of the twenty.
		Assert.That(Attacks(state, _ids, dedup: false).Count, Is.EqualTo(21));
	}
}
