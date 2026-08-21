using System.Collections.Immutable;
using System.Linq;
using ImmutableGameObjects;
using MtgCore;
using MtgGame;
using NUnit.Framework;

namespace SQGodotCommon.Tests;

/// <summary>
/// Action deduplication is an AI search optimisation and must never reach the human.
///
/// It collapses strategically-identical attackers and defenders to one representative, which is
/// correct for a search and catastrophic for a UI: the player finds a creature that cannot be
/// clicked, or an enemy token that cannot be attacked, with nothing to explain why. There is no
/// error and no log line — the action simply does not exist.
///
/// Two dimensions, so two ways to hit it, and they fail in different places: attacker dedup makes
/// YOUR creature unusable, defender dedup makes THEIR creature untargetable.
/// </summary>
[TestFixture]
public class HumanNeverSeesDedupedActionsTests
{
	private static MtgGameManager StartedManager()
	{
		var filler = CoresetCube.Cards.Take(40).ToList();
		var manager = new MtgGameManager(
			new DeckSetupData(new DeckChoice.Drafted(filler), new DeckChoice.Drafted(filler))
		);
		manager.StartGame();
		return manager;
	}

	/// Puts N copies of one vanilla creature onto a player's battlefield, ready to attack.
	private static int[] AddIdenticalCreatures(MtgGameManager manager, int ownerId, int count)
	{
		var ids = new int[count];
		var battlefieldId = manager.State.GetPlayerZoneId(ownerId, ZoneType.Battlefield);
		var state = manager.State;

		for (var i = 0; i < count; i++)
		{
			var (newState, added) = state.AddObject(
				new Card
				{
					Name = "Goblin",
					OwnerId = ownerId,
					ControllerId = ownerId,
					Components = ImmutableArray.Create<GameComponent>(
						new CreatureComponent
						{
							Power = 1,
							Toughness = 1,
							HasSummoningSickness = false,
						}
					),
				},
				parentId: battlefieldId
			);
			state = newState;
			ids[i] = added.Id;
		}

		manager.DebugSetState(state);
		return ids;
	}

	[Test]
	public void EveryOneOfTheHumansIdenticalCreaturesCanAttack()
	{
		var manager = StartedManager();
		var mine = AddIdenticalCreatures(manager, manager.HumanPlayerId, 6);

		var attackers = manager
			.GetLegalActions(manager.HumanPlayerId)
			.OfType<AttackAction>()
			.Select(a => a.AttackerId)
			.ToHashSet();

		Assert.That(attackers, Is.SupersetOf(mine), "A human creature was deduplicated away");
	}

	[Test]
	public void EveryOneOfTheOpponentsIdenticalTokensCanBeAttacked()
	{
		var manager = StartedManager();
		var mine = AddIdenticalCreatures(manager, manager.HumanPlayerId, 1);
		var theirs = AddIdenticalCreatures(manager, manager.AiPlayerId, 6);

		var targets = manager.GetLegalAttackTargets(mine[0]);

		Assert.That(targets, Is.SupersetOf(theirs), "An enemy token was deduplicated away");
	}

	/// The AI side must still collapse them, or the optimisation is not doing anything.
	[Test]
	public void TheAiStillSeesTheCollapsedList()
	{
		var manager = StartedManager();
		AddIdenticalCreatures(manager, manager.AiPlayerId, 6);

		var aiAttacks = manager
			.GetLegalActions(manager.AiPlayerId)
			.OfType<AttackAction>()
			.Select(a => a.AttackerId)
			.Distinct()
			.Count();

		Assert.That(aiAttacks, Is.EqualTo(1), "AI dedup stopped working");
	}
}
