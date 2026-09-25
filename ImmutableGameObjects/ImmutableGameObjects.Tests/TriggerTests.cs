using ImmutableGameObjects;
using NUnit.Framework;

namespace ImmutableGameObjects.Tests;

/// <summary>
/// <see cref="Trigger"/> and <see cref="Triggers.FireTriggers"/>, with stub types only: an action
/// stages an event, the post-processor fires the matching abilities, and their effects resolve.
/// </summary>
[TestFixture]
public class TriggerTests
{
	private record Thing : GameObject;

	private record Pinged : GameEvent;

	private record PingAction : GameAction
	{
		public override ActionResult Execute(GameState s) => new(s.StageEvent(new Pinged()));
	}

	private record OnPing : TriggerRule
	{
		public override bool Matches(GameEvent e, GameState s, int sourceId) => e is Pinged;
	}

	/// <summary>Counts on the object that fired it — so it also proves the source was bound.</summary>
	private record CountAction : GameAction, ITriggerBound
	{
		public int SourceId { get; init; }

		public GameAction Bind(int sourceId, GameEvent trigger) =>
			this with
			{
				SourceId = sourceId,
			};

		public override ActionResult Execute(GameState s)
		{
			var o = s.GetObject(SourceId);
			return new(s.UpdateObject(SourceId, o.WithMeta("n", o.GetMeta<int>("n", 0) + 1)));
		}
	}

	/// <summary>Fires the triggers of every object whose id is in the list.</summary>
	private record FireAction : GameAction
	{
		public int[] Sources { get; init; } = [];

		public override bool IsPostProcessor => true;

		public override ActionResult Execute(GameState s) => new(s.FireTriggers(Sources));
	}

	private static (GameState, int Active, int Inactive) Setup(int maxPerTurn = 0)
	{
		var ability = new Trigger
		{
			When = new OnPing(),
			Effects = [new CountAction()],
			MaxPerTurn = maxPerTurn,
		};
		var (s, active) = new GameState().AddObject(new Thing().WithComponent(ability));
		(s, var inactive) = s.AddObject(new Thing().WithComponent(ability));
		return (
			s with
			{
				PostActionProcessor = new FireAction { Sources = [active.Id] },
			},
			active.Id,
			inactive.Id
		);
	}

	private static int Count(GameState s, int id) => s.GetObject(id).GetMeta<int>("n", 0);

	[Test]
	public void AStagedEventFiresTheAbilityOnAnActiveSourceOnly()
	{
		var (s, active, inactive) = Setup();

		s = s.AddAction(new PingAction()).AddAction(new PingAction()).ProcessAllActions().State;

		Assert.That(Count(s, active), Is.EqualTo(2));
		Assert.That(Count(s, inactive), Is.EqualTo(0), "the game chose it is not a source");
		Assert.That(s.PendingGameEvents, Is.Empty);
	}

	[Test]
	public void APerTurnCapHoldsUntilReset()
	{
		var (s, active, _) = Setup(maxPerTurn: 1);

		s = s.AddAction(new PingAction()).AddAction(new PingAction()).ProcessAllActions().State;
		Assert.That(Count(s, active), Is.EqualTo(1));

		s = s.ResetTriggers(active).AddAction(new PingAction()).ProcessAllActions().State;
		Assert.That(Count(s, active), Is.EqualTo(2));
	}
}
