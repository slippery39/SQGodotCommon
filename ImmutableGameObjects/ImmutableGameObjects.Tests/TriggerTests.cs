using ImmutableGameObjects;
using NUnit.Framework;

namespace ImmutableGameObjects.Tests;

/// <summary>
/// The engine's trigger loop, <see cref="Triggers.FireTriggers{T}"/>, with no game-specific types.
/// MtgCore's triggered abilities run on it; these pin the contract a new game builds against.
/// </summary>
[TestFixture]
public class TriggerTests
{
	private record Thing : GameObject;

	private record Ping : GameEvent
	{
		public string Kind { get; init; } = "";
	}

	private record Spawned : GameAction
	{
		public string Kind { get; init; } = "";

		public override ActionResult Execute(GameState gameState) => new(gameState);
	}

	/// A game's ability: fires on pings of its kind.
	private record OnPing : TriggeredAbility
	{
		public string Kind { get; init; } = "";
	}

	private static (GameState State, int Id) With(OnPing ability)
	{
		var (state, thing) = new GameState().AddObject(new Thing { Components = [ability] });
		return (state, thing.Id);
	}

	private static GameState Fire(GameState state, int id, params Ping[] events) =>
		state.FireTriggers<OnPing>(
			id,
			events,
			matches: (a, e, _) => e is Ping p && p.Kind == a.Kind,
			spawn: (a, e, _) => [new Spawned { Kind = a.Kind }]
		);

	[Test]
	public void AMatchingEventSpawnsTheAbilitysActions_AndANonMatchingOneDoesNot()
	{
		var (state, id) = With(new OnPing { Kind = "dies" });

		state = Fire(state, id, new Ping { Kind = "dies" }, new Ping { Kind = "enters" });

		Assert.That(state.SpawnQueue, Has.Count.EqualTo(1));
		Assert.That(((Spawned)state.SpawnQueue[0]).Kind, Is.EqualTo("dies"));
	}

	[Test]
	public void APerTurnCapHoldsWithinOneBatch_AndTheCountIsWrittenBack()
	{
		var (state, id) = With(new OnPing { Kind = "dies", MaxTriggersPerTurn = 1 });

		state = Fire(state, id, new Ping { Kind = "dies" }, new Ping { Kind = "dies" });

		var ability = state.GetObject(id).GetComponent<OnPing>()!;
		Assert.Multiple(() =>
		{
			Assert.That(state.SpawnQueue, Has.Count.EqualTo(1), "two matches, a once-a-turn cap");
			Assert.That(ability.TriggerCountThisTurn, Is.EqualTo(1));
			Assert.That(ability.CanTrigger, Is.False);
		});
	}

	[Test]
	public void ALifetimeCapIsNeverExceeded()
	{
		var (state, id) = With(
			new OnPing
			{
				Kind = "dies",
				MaxTriggers = 1,
				TriggerCountTotal = 1,
			}
		);

		state = Fire(state, id, new Ping { Kind = "dies" });

		Assert.That(state.SpawnQueue, Is.Empty, "already fired once ever");
	}

	/// <summary>
	/// The guard that keeps firing an uncapped ability from changing state that searches and loop
	/// fingerprints read — MtgCore's behaviour before the loop moved here, and its tests depend on it.
	/// </summary>
	[Test]
	public void AnUncappedAbilityFires_ButLeavesTheObjectUntouched()
	{
		var (state, id) = With(new OnPing { Kind = "dies" });
		var before = state.GetObject(id);

		state = Fire(state, id, new Ping { Kind = "dies" }, new Ping { Kind = "dies" });

		Assert.That(state.SpawnQueue, Has.Count.EqualTo(2));
		Assert.That(state.GetObject(id), Is.SameAs(before));
	}

	[Test]
	public void ConditionsSeeTheStateFromBeforeThisObjectsOwnSpawns()
	{
		var (state, id) = With(new OnPing { Kind = "dies" });
		var queueSeen = new List<int>();

		state.FireTriggers<OnPing>(
			id,
			[new Ping { Kind = "dies" }, new Ping { Kind = "dies" }],
			matches: (a, e, s) =>
			{
				queueSeen.Add(s.SpawnQueue.Count);
				return true;
			},
			spawn: (a, e, _) => [new Spawned()]
		);

		Assert.That(queueSeen, Is.EqualTo(new[] { 0, 0 }));
	}
}
